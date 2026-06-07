namespace Nexus.Fms.Api.Security;

/// <summary>Role constants used for [Authorize] decorators throughout the API.</summary>
public static class Roles
{
    /// <summary>Can view, assign, note, escalate, and resolve cases.</summary>
    public const string Analyst = "fraud-analyst";

    /// <summary>All analyst permissions + propose rules, manage whitelist/blacklist.</summary>
    public const string Admin = "fraud-admin";

    /// <summary>Can approve or reject rule proposals (maker-checker second role, FR-25).</summary>
    public const string Approver = "fraud-approver";
}
