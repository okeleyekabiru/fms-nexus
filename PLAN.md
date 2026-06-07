# FMS Implementation Plan — Remaining Features

**Project:** Nexus Fraud Management Service — Advans Lafayette MFB  
**Spec ref:** CIP/ADV/FMS/2026/001 v1.0  
**Status:** Core screening pipeline ✅ complete. Six milestones remaining before go-live.

---

## Milestone overview

| # | Milestone | Blocking go-live? | Est. effort |
|---|---|---|---|
| M1 | Case Management API | Yes | ~5 days |
| M2 | Fraud Response Actions (SAR + auto-blacklist + list API) | Yes | ~3 days |
| M3 | Async Rule Evaluation | Yes | ~2 days |
| M4 | Rule Administration + Audit Trail | Yes | ~4 days |
| M5 | Reporting & Dashboard API | No (post-launch) | ~4 days |
| M6 | Production Readiness (auth, caching, retention, tests) | Yes | ~5 days |

Work order: **M6 auth scaffold first** (so M1–M4 endpoints can be protected as they're built), then M1 → M2 → M3 → M4 in parallel, then M5.

---

## M1 — Case Management API
**FRs:** FR-19, FR-20, FR-21 · **Workflow:** 3

The entire analyst-facing case workflow is unbuilt. Cases are created by `ScreeningService` but there is no way to view, work, or resolve them.

### Tasks

#### M1-1 · Fix `FraudAlert.ShadowOnly` not being set (bug)
`ScreeningService.ScreenCoreAsync` never sets `FraudAlert.ShadowOnly`. When all triggered rules are shadow-mode rules, the alert should be flagged accordingly so reporting can distinguish shadow from enforced alerts.

**Change:** In `ScreeningService`, after scoring, set:
```csharp
ShadowOnly = result.EffectiveRules.Count == 0 && result.ShadowRules.Count > 0
```

#### M1-2 · Case query repository
Add to `IAlertStore` (or a new `ICaseRepository`):
```csharp
Task<IReadOnlyList<FraudCase>> GetCasesAsync(CaseStatus? status, int skip, int take, CancellationToken ct);
Task<FraudCase?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
Task<FraudCase> UpdateCaseAsync(FraudCase fraudCase, CancellationToken ct);
Task<FraudAlert?> GetAlertByIdAsync(Guid alertId, CancellationToken ct);
```

Implement in `Infrastructure/Persistence/Repositories.cs`. Add `IAlertRepository` (rename `IAlertStore` or extend it) and wire up in `DependencyInjection`.

#### M1-3 · Case management service
New class `Nexus.Fms.Core.Services.CaseManagementService : ICaseManagementService`:

```csharp
Task<FraudCase> AssignCaseAsync(Guid caseId, string analystId, CancellationToken ct);
Task<FraudCase> AddNoteAsync(Guid caseId, string note, string authorId, CancellationToken ct);
Task<FraudCase> EscalateCaseAsync(Guid caseId, string escalatedBy, CancellationToken ct);
Task<FraudCase> ResolveCaseAsync(Guid caseId, CaseResolution resolution, string resolvedBy, CancellationToken ct);
Task<FraudCase> OverrideVerdictAsync(Guid caseId, Verdict newVerdict, string overriddenBy, CancellationToken ct);
```

Notes field is **append-only** per FR-20 — each `AddNote` appends `[ISO timestamp · author]: text\n` rather than replacing.

`ResolveCaseAsync` sets `ResolvedAt = UtcNow` and fires `ICaseSideEffectsHandler` (see M2) for confirmed-fraud actions.

#### M1-4 · Cases controller
New `CasesController` at `/api/cases`:

| Method | Route | Description | FR |
|--------|-------|-------------|---|
| `GET` | `/api/cases` | List cases; query params: `status`, `assignedTo`, `page`, `pageSize` | FR-19 |
| `GET` | `/api/cases/{id}` | Case detail with alert + triggered rules | FR-19 |
| `POST` | `/api/cases/{id}/assign` | `{ analystId }` | FR-20 |
| `POST` | `/api/cases/{id}/notes` | `{ text }` | FR-20 |
| `POST` | `/api/cases/{id}/escalate` | — | FR-20 |
| `POST` | `/api/cases/{id}/resolve` | `{ resolution, verdictOverride? }` | FR-20 |
| `GET` | `/api/alerts/{id}` | Alert detail | FR-18 |

Response DTOs live in `Nexus.Fms.Api/Contracts/CaseContracts.cs`.

#### M1-5 · 24h auto-escalation background job (FR-21)
Add `Nexus.Fms.Infrastructure.Jobs.CaseEscalationJob` using `IHostedService` + a timer (or `BackgroundService`).

Runs every 15 minutes. Query:
```sql
SELECT * FROM fraud_cases
WHERE status IN ('New', 'UnderInvestigation')
  AND created_at < NOW() - INTERVAL '24 hours'
  AND (last_escalated_at IS NULL OR last_escalated_at < NOW() - INTERVAL '24 hours')
```

For each: set `Status = Escalated`, `LastEscalatedAt = UtcNow`, log + notify (stub notification for now).

For cases unresolved after 72h: log a `LogLevel.Critical` entry tagged `HeadOfOpsAlert` (wire to a real notification in M6).

Register in `DependencyInjection`:
```csharp
services.AddHostedService<CaseEscalationJob>();
```

---

## M2 — Fraud Response Actions
**FRs:** FR-17, FR-28 · **Workflow:** 5

#### M2-1 · SAR submission on confirmed fraud (FR-17, Workflow 5)
Create `ICaseSideEffectsHandler` interface and `CaseSideEffectsHandler` implementation, called from `CaseManagementService.ResolveCaseAsync` when resolution is `ConfirmedFraud`:

```csharp
public interface ICaseSideEffectsHandler
{
    Task OnConfirmedFraudAsync(FraudCase fraudCase, FraudAlert alert, CancellationToken ct);
}
```

`CaseSideEffectsHandler.OnConfirmedFraudAsync`:
1. Build `SarPayload` from the case's alert (pull `TransactionRef`, `Amount`, `CustomerId`/BVN from the alert's `NibssLookupResultJson`).
2. Call `INibssFraudBureauClient.SubmitSarAsync(payload)` → store reference in `FraudCase.SarReference`.
3. Add the sender BVN/account to the internal blacklist via `IListRepository.AddAsync` with `Source = Internal`, `Reason = $"Confirmed fraud — case {caseId}"`.
4. Persist both updates (`UpdateCaseAsync`).
5. Log the NIBSS reference.

If NIBSS is unavailable, log the failure and queue a retry (store a `PendingSar` record — simple table, or use a `BackgroundService` retry loop).

#### M2-2 · Whitelist/Blacklist management API (FR-28)
Extend `IListRepository`:
```csharp
Task<IReadOnlyList<ListEntry>> GetEntriesAsync(ListType? type, int skip, int take, CancellationToken ct);
Task<bool> RemoveAsync(Guid entryId, CancellationToken ct);
```

New `ListManagementController` at `/api/lists`:

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/lists` | Query by `type` (blacklist/whitelist), paginated |
| `POST` | `/api/lists` | Add entry `{ bvn?, accountNumber?, listType, reason }` |
| `DELETE` | `/api/lists/{id}` | Soft-delete (or hard-delete with audit log entry) |

---

## M3 — Async Rule Evaluation
**FR:** FR-03 · Rules R11 (smurfing), R12 (round-number pattern)

Currently async rules (`IsSynchronous = false`) are skipped in the synchronous path and **never run**. Post-transaction evaluation requires a background job that re-evaluates after the core banking system confirms settlement.

#### M3-1 · Post-transaction evaluation queue
Two options — go with the simpler one for now:

**Option A (recommended for current scale): DB-backed polling job**  
After `ScreeningService` saves the alert for a transaction that passed (Allow/Flag/RequireMfa-passed), write a `PendingAsyncEvaluation` record to a new table:

```
pending_async_evaluations
  id          UUID PK
  transaction_ref VARCHAR
  context_json    JSONB   -- serialised TransactionContext
  created_at      TIMESTAMPTZ
  processed_at    TIMESTAMPTZ nullable
```

A `BackgroundService` polls every 30 seconds, picks up unprocessed rows, runs `RuleEngine.Evaluate(..., synchronousOnly: false)` against async rules only, and if any fire: updates the existing `FraudAlert` and creates a case if the new combined score crosses the P3 threshold.

**Option B (future):** Replace with a message queue (RabbitMQ / Azure Service Bus) when transaction volume justifies it.

#### M3-2 · Wire `TransactionContext` serialisation
`TransactionContext` needs `[JsonSerializable]` or a custom converter so it can be stored as JSONB in `context_json`. Add `System.Text.Json` serialisation support and test round-trip.

#### M3-3 · Update `ScreeningService` to enqueue
After persisting the alert for a non-blocked transaction, call:
```csharp
await _asyncQueue.EnqueueAsync(context, ct);
```
Where `IAsyncEvaluationQueue` is injected (DB-backed implementation in infrastructure).

---

## M4 — Rule Administration + Audit Trail
**FRs:** FR-25, FR-26, FR-27 · **Workflow:** 4

#### M4-1 · Audit log table
New entity `AuditLogEntry` and EF table `fms_audit_log`:

```
fms_audit_log
  log_id       UUID PK
  entity_type  VARCHAR  -- "FraudRule", "ThresholdBands", "ListEntry", etc.
  entity_id    VARCHAR
  action       VARCHAR  -- "Created", "Updated", "Approved", "Rejected", "Deleted"
  changed_by   VARCHAR
  changed_at   TIMESTAMPTZ
  before_json  JSONB nullable
  after_json   JSONB nullable
```

Add `IAuditLogger` interface with a single method `LogAsync(AuditLogEntry)`. Inject into any service that mutates rules, bands, or lists.

#### M4-2 · Rule proposal state (maker-checker, FR-25)
The current `FraudRule` model has `CreatedBy`/`ApprovedBy` but no pending/rejected state. Add:

```csharp
public enum RuleApprovalStatus { Draft, PendingApproval, Approved, Rejected }

// Add to FraudRule:
public RuleApprovalStatus ApprovalStatus { get; set; } = RuleApprovalStatus.Draft;
public string? RejectionReason { get; set; }
public DateTimeOffset? ApprovedAt { get; set; }
```

A rule is only deployed to the engine (`Mode = Shadow | Live`) after `ApprovalStatus == Approved`. The `RuleRepository` already filters by `Mode`, so this only affects the admin flow.

#### M4-3 · Rule admin API
New `RulesController` at `/api/rules`:

| Method | Route | Description | FR |
|--------|-------|-------------|---|
| `GET` | `/api/rules` | List rules; filter by `mode`, `category`, `approvalStatus` | FR-07 |
| `GET` | `/api/rules/{id}` | Rule detail with conditions JSON | — |
| `POST` | `/api/rules` | Propose new rule (sets `ApprovalStatus = PendingApproval`) | FR-25 |
| `PUT` | `/api/rules/{id}` | Propose modification (creates a draft copy; original stays live) | FR-25 |
| `POST` | `/api/rules/{id}/approve` | Approve + optionally set mode (`Shadow` or `Live`) | FR-25 |
| `POST` | `/api/rules/{id}/reject` | `{ reason }` | FR-25 |
| `POST` | `/api/rules/{id}/mode` | `{ mode: Disabled | Shadow | Live }` | FR-07 |
| `DELETE` | `/api/rules/{id}` | Soft-delete (set `Mode = Disabled`, log audit) | FR-07 |
| `POST` | `/api/rules/{id}/clone` | Clone rule as a draft template (FR-27) | FR-27 |

All mutating endpoints write an `AuditLogEntry` (M4-1).

#### M4-4 · Threshold bands admin API
New endpoint `PUT /api/config/threshold-bands` (admin only) accepting `{ p1Min, p2Min, p3Min, p4Min }`.  
Validates descending order, persists to a `fms_config` table (key-value), updates the in-process `ThresholdBands` instance, and writes an audit log entry (FR-26).

`ThresholdBands` currently comes from `IOptions<ThresholdBands>` (appsettings). For runtime mutability, change to `IOptionsMonitor<ThresholdBands>` or introduce a `IThresholdBandsProvider` that reads from DB with a short cache.

#### M4-5 · Rule template support (FR-27)
`POST /api/rules/{id}/clone` (added in M4-3) is the core of this. Optionally add a `IsTemplate` flag on `FraudRule` so admins can mark well-tuned rules as templates visible in a template library:
```csharp
public bool IsTemplate { get; set; }
```
`GET /api/rules?isTemplate=true` surfaces the library.

---

## M5 — Reporting & Dashboard API
**FRs:** FR-22, FR-23  
*Post-launch; can run in parallel with M6 production hardening.*

#### M5-1 · Dashboard endpoint (FR-23)
`GET /api/dashboard/summary` — returns a snapshot suitable for the Admin Console:

```json
{
  "liveAlertFeed": [ /* last 20 alerts */ ],
  "casesByStatus": { "New": 4, "UnderInvestigation": 2, "Escalated": 1, "Resolved": 120 },
  "scoreDistribution": { "P1": 3, "P2": 12, "P3": 45, "P4": 200, "Clean": 5000 },
  "kpis": {
    "blockRateLast30Days": 0.003,
    "falsePositiveRateLast30Days": 0.12,
    "meanTimeToResolutionHours": 6.4,
    "avgCompositeScore": 8.2
  }
}
```

Compute KPIs with raw SQL / EF projections. Cache the result for 60 seconds to avoid hammering the DB on every dashboard refresh.

#### M5-2 · Monthly fraud report (FR-22)
New `ReportingService` with `GenerateMonthlyReportAsync(int year, int month)` returning a structured DTO covering:
- Total transactions screened
- Score distribution histogram (bands)
- Alerts by risk level
- Blocked transactions (count + total value)
- False positive rate (resolved as FalsePositive / total resolved)
- Top 5 triggered rules by contribution count
- Avg resolution time
- Trend vs prior month

`GET /api/reports/monthly?year=2026&month=06` streams the result as JSON (or a CSV via `Accept: text/csv`).

Background job option: generate + cache the report on the 1st of each month for the prior month, store in a `fms_monthly_reports` table.

---

## M6 — Production Readiness
**NFRs:** NFR-01, NFR-05, NFR-06, NFR-08, NFR-09

#### M6-1 · Authentication & authorisation (required before M1–M4 endpoints go live)
Add JWT bearer auth. Three roles:

| Role | Permissions |
|------|-------------|
| `fraud-analyst` | Read cases/alerts, assign, note, resolve |
| `fraud-admin` | All analyst permissions + propose rules, manage lists |
| `fraud-approver` | Approve/reject rule proposals (maker-checker second role) |

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* options from config */);
builder.Services.AddAuthorization(/* policy per role */);
```

Decorate controllers with `[Authorize(Roles = "fraud-admin")]` etc.  
The screening endpoint (`/api/screening/evaluate`) uses an **API key** (middleware-to-FMS service auth), not JWT.

#### M6-2 · Rule caching (NFR-09)
`RuleRepository.GetActiveRulesAsync` currently hits the DB on every transaction. At 100 concurrent transactions this is significant DB load.

Add `CachedRuleRepository` decorator:
```csharp
public sealed class CachedRuleRepository : IRuleRepository
{
    // IMemoryCache with 30s absolute expiration.
    // Cache is invalidated explicitly when any rule changes (call Invalidate() from RulesController).
}
```

Register as the outer decorator over the DB repository. 30s TTL is short enough that shadow → live promotions take effect quickly without a restart.

#### M6-3 · EF Core migrations
Replace `db.Database.EnsureCreated()` in `Program.cs` with proper migrations:
```bash
dotnet ef migrations add Initial --project src/Nexus.Fms.Infrastructure --startup-project src/Nexus.Fms.Api
dotnet ef database update
```
Add the `pending_async_evaluations`, `fms_audit_log`, and `fms_config` tables introduced by M3/M4 in subsequent migration files.

#### M6-4 · Data retention (FR-24 / NFR-06)
7-year minimum. Two complementary approaches:

1. **PostgreSQL table partitioning** — partition `fraud_alerts` and `fraud_cases` by `created_at` (monthly or yearly partitions). Old partitions can be detached and archived to cold storage without locking.

2. **Retention job** — `BackgroundService` that runs monthly and soft-deletes (or archives to a `_archive` schema) records older than 7 years. For auditability, prefer archiving to deletion.

Add `IsArchived` flag to `FraudAlert` and `FraudCase`, and exclude archived records from live queries.

#### M6-5 · PII encryption at rest (NFR-05 / NDPR)
Sensitive fields: `SenderBvn`, `ReceiverBvn`, `SenderAccount`, `ReceiverAccount` in `FraudAlert` (stored via JSON), and `Bvn`/`AccountNumber` in `ListEntry`.

Options:
- **EF Core value converters** — encrypt/decrypt transparently on read/write using AES-256. Key from Azure Key Vault / AWS KMS / environment secret.
- **PostgreSQL `pgcrypto`** — encrypt at the column level in the DB.

Recommended: EF value converter (keeps logic in the app, easier to rotate keys).

#### M6-6 · Test project
Add `tests/Nexus.Fms.Tests` (xUnit). Minimum coverage for go-live:

| Test class | What it covers |
|---|---|
| `PredicateEvaluatorTests` | All 9 ops, AND/OR/NOT trees, edge cases (null facts, malformed) |
| `RuleEngineTests` | Shadow vs live exclusion, `synchronousOnly` filter, invalid JSON skip |
| `ScoringEngineTests` | All threshold bands, `CannotBeOffset` logic, floor at 0, score examples from §2.3 |
| `ScreeningServiceTests` | Fail-open, fail-closed, alert creation, case creation threshold, NIBSS unavailable compensating score |
| `CaseManagementServiceTests` | State transitions, note appending, auto-escalation trigger |
| `RuleSeederTests` | All 15 seeded rules evaluate correctly against known inputs |

Use `FmsDbContext` in-memory (SQLite) for integration tests of repositories.

---

---

## Gaps & clarifications vs the requirements spec

The following items were found during the cross-check. Each is either added to a milestone, noted as a middleware/Admin Console concern, or called out as a known spec ambiguity.

### ❶ FR-04 — Per-category fail-closed not implemented (add to M4)

The spec says: *"unless the administrator has configured fail-closed mode for **specific high-risk rule categories**."*

The current `ScreeningOptions.FailureMode` is a single global flag. This is not spec-compliant. Add to **M4-4** alongside the threshold bands API:

- Add `FailureModeOverrides` dictionary to `ScreeningOptions`: `Dictionary<RuleCategory, FailureMode>`
- `PUT /api/config/screening-options` endpoint (admin only) to update this at runtime
- `ScreeningService` checks the per-category override when a screening exception occurs; if any triggered rule's category is fail-closed, block the transaction

### ❷ Workflow 2 — Inbound BLOCK semantics (suspense hold, not rejection)

The spec states: *"For inflows, BLOCK means the credit is held in a suspense account pending investigation (not rejected to the sending bank)."*

The FMS returns `Verdict.Block` uniformly. The **middleware** is responsible for interpreting this differently based on direction (it knows the direction from the original request), so no FMS code change is needed. However:

- Add a note to `ScreeningResponse` XML docs clarifying that for `Direction = Inflow`, `Block` means "hold in suspense" — the middleware must not send a rejection to the originating bank
- The `GET /api/cases/{id}` response (M1-4) should surface the transaction direction so the analyst knows whether the account is locked (outbound block) or funds are in suspense (inbound block)

### ❸ FR-20 — "View customer's recent 30-day transaction history"

The spec says analysts can *"view the customer's recent 30-day transaction history."* The FMS does not own transaction history — this lives in core banking / the NEXUS middleware. The `GET /api/cases/{id}` endpoint (M1-4) should include a `customerTransactionHistoryUrl` field pointing to the middleware endpoint so the Admin Console can deep-link. The FMS itself does not need to proxy or store this data.

### ❹ FR-23 — "Transaction history heat map" in the dashboard

The spec mentions a heat map as part of the analyst case view (Workflow 3 step 1). This is a rendering concern for the Admin Console frontend, not an FMS API field. The FMS dashboard endpoint (M5-1) should expose the raw alert time-series data (alerts grouped by hour-of-day and day-of-week) so the frontend can render the heat map:
```json
"alertHeatmap": [ { "dayOfWeek": 1, "hourOfDay": 2, "count": 14 }, ... ]
```
Add this to the M5-1 dashboard response shape.

### ❺ 72-hour Head-of-Ops alert — needs a real notification channel

M1-5 stubs the 72h alert as a `LogLevel.Critical` log entry. For go-live, this needs to route to a real channel. Add to **M6**:

- Abstract `IFraudNotificationService` with methods `NotifyCustomer`, `NotifyFraudTeam`, `NotifyHeadOfOps`
- Default implementation: email via `SmtpClient` / SendGrid (config-driven)
- Wire into `CaseEscalationJob` (M1-5) and into `ScreeningService` for P1 blocks (Workflow 1, step 6b)
- The screening endpoint already returns `Verdict.Block` — the middleware sends the SMS/push for the customer (per the spec), but the **internal fraud team alert** for P1/P2 is an FMS responsibility and currently unimplemented

### ❻ §6 data model — `risk_level` and `action` on `fraud_rules`

The spec's data model table lists `risk_level ENUM` and `action ENUM` on the `fraud_rules` table. The code correctly omits these — FR-06 explicitly says *"The risk level is NOT assigned per rule — it is calculated by the engine from the aggregate score."* The Admin Console integration must not assume these columns exist; the verdict comes from the scoring engine, not per-rule fields. Document this in an API contract note when building the rule admin endpoints (M4-3).

---

## Dependency graph

```
M6-1 (auth scaffold)
  ↓
M1 (case management)  ←─────────┐
M2 (SAR + list API)   ←── M1-3  │
M3 (async eval)                  │
M4 (rule admin)                  │
  ↓                              │
M6-3 (migrations, picks up new tables from M3/M4)
M6-2 (rule cache, after M4 rule admin is wired)
M6-4 (retention)
M6-5 (PII encryption)
M6-6 (tests — can run throughout, but final pass after all features)
  ↓
M5 (reporting — post-launch)
```

---

## Files to create / modify per milestone

| Milestone | New files | Modified files |
|---|---|---|
| M1 | `Services/CaseManagementService.cs`, `Controllers/CasesController.cs`, `Contracts/CaseContracts.cs`, `Jobs/CaseEscalationJob.cs` | `Abstractions/Interfaces.cs`, `Persistence/Repositories.cs`, `DependencyInjection.cs`, `Services/ScreeningService.cs` (ShadowOnly fix) |
| M2 | `Services/CaseSideEffectsHandler.cs`, `Controllers/ListManagementController.cs`, `Contracts/ListContracts.cs` | `Abstractions/Interfaces.cs`, `Persistence/Repositories.cs` |
| M3 | `Domain/PendingAsyncEvaluation.cs`, `Persistence/AsyncEvaluationQueue.cs`, `Jobs/AsyncEvaluationJob.cs` | `FmsDbContext.cs`, `DependencyInjection.cs`, `Services/ScreeningService.cs` |
| M4 | `Domain/AuditLogEntry.cs`, `Services/RuleAdminService.cs`, `Controllers/RulesController.cs`, `Controllers/ConfigController.cs`, `Contracts/RuleContracts.cs` | `Domain/FraudRule.cs`, `FmsDbContext.cs`, `Persistence/Repositories.cs` |
| M5 | `Services/ReportingService.cs`, `Controllers/DashboardController.cs`, `Controllers/ReportsController.cs`, `Contracts/ReportContracts.cs` | — |
| M6 | `Security/ApiKeyMiddleware.cs`, `Persistence/CachedRuleRepository.cs`, `Jobs/RetentionJob.cs`, `tests/` (new project) | `Program.cs`, `DependencyInjection.cs`, `FmsDbContext.cs` |
