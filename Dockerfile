# Multi-stage build for the Nexus FMS API (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore (layer-cached on csproj changes only)
COPY Nexus.Fms.sln ./
COPY src/Nexus.Fms.Core/Nexus.Fms.Core.csproj src/Nexus.Fms.Core/
COPY src/Nexus.Fms.Infrastructure/Nexus.Fms.Infrastructure.csproj src/Nexus.Fms.Infrastructure/
COPY src/Nexus.Fms.Api/Nexus.Fms.Api.csproj src/Nexus.Fms.Api/
RUN dotnet restore Nexus.Fms.sln

# Build + publish
COPY . .
RUN dotnet publish src/Nexus.Fms.Api/Nexus.Fms.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Listen on 8080 inside the container (no HTTPS redirect needed behind compose)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Nexus.Fms.Api.dll"]
