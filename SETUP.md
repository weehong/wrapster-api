# Local Setup

This guide starts the local infrastructure with `compose.dev.yaml` and then runs the Wrapsfer API with .NET.

## Prerequisites

- Docker
- .NET SDK `10.0.201` or a compatible latest feature SDK
- Optional: Entity Framework CLI tools for database migrations

```bash
dotnet tool install --global dotnet-ef
```

## 1. Start Infrastructure

```bash
docker compose -f compose.dev.yaml up -d
```

This starts PostgreSQL, RabbitMQ, Keycloak, and the Keycloak admin-client bootstrap job.

Local endpoints:

- PostgreSQL: `localhost:15432`, database `wrapsfer`, username `wrapsfer`, password `wrapsfer`
- RabbitMQ: `localhost:5672`, username `wrapsfer`, password `wrapsfer`
- RabbitMQ management UI: `http://localhost:15672`, username `wrapsfer`, password `wrapsfer`
- Keycloak admin console: `http://localhost:18080/admin`, username `admin`, password `admin`

## 2. Keycloak Model

The dev compose file imports only `keycloak/realm-export.json`, which creates the owner realm `wrapsfer`.

Partner realms are created dynamically through the API with `POST /api/v1/partners`. Do not import partner realms during normal setup; this keeps the Keycloak realm and the application `PartnerTenant` database record in sync.

Static partner realm JSON files live under `keycloak/fixtures/` as examples only.

## 3. Local API Configuration

The tracked `appsettings.Development.json` is aligned with `compose.dev.yaml`:

- `Keycloak:BaseUrl` is `http://localhost:18080`
- `Keycloak:PartnerOnboardingEnabled` is `true`
- `Keycloak:AdminClientId` is `wrapsfer-admin`
- `Keycloak:AdminClientSecret` is `wrapsfer-admin-secret`
- `Keycloak:PartnerApiClientSecret` is `wrapsfer-secret`
- `PartnerIntegrations:ApiBaseUrl` is `http://localhost:5222`

`Keycloak:PartnerApiClientSecret` is the shared API OIDC client secret for the `wrapsfer` client in both the owner realm and every provisioned partner realm.

`compose.dev.yaml` starts local infrastructure only; the API is run with `dotnet run`, so API settings such as `PartnerIntegrations:ApiBaseUrl` come from `appsettings.Development.json` or user secrets rather than Compose environment variables.

Use .NET user secrets only when you need local overrides:

```bash
dotnet user-secrets set "Keycloak:AdminClientSecret" "your-local-secret" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "PartnerIntegrations:ApiBaseUrl" "https://api.wrapsfer.dev" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

## 4. Restore And Build

```bash
make restore
make build
```

Equivalent commands:

```bash
dotnet restore
dotnet build --no-restore
```

## 5. Apply Database Migrations

```bash
export WRAPSFER_DESIGN_CONNECTION="Host=localhost;Port=15432;Database=wrapsfer;Username=wrapsfer;Password=wrapsfer"
dotnet ef database update \
  --project src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj \
  --startup-project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

## 6. Run The API

```bash
dotnet run --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

Useful local endpoints:

- Health check: `http://localhost:5222/health` or `https://localhost:7065/health`
- OpenAPI document in Development: `http://localhost:5222/openapi/v1.json` or `https://localhost:7065/openapi/v1.json`

Use the port printed by `dotnet run` if it differs from the launch profile defaults.

## 7. Create Partner Realms

Log in as the owner admin first, then call `POST /api/v1/partners` with the owner token. The API creates the partner database record, provisions the matching Keycloak realm, creates the partner admin user, and marks the partner active.

The owner admin user is seeded in the `wrapsfer` realm:

- Username: `owneradmin`
- Password: value of `KEYCLOAK_OWNER_ADMIN_PASSWORD`, default `ChangeMe-12345!` in `compose.dev.yaml`

## 8. Run Tests

```bash
make test
```

Equivalent command:

```bash
dotnet test --no-restore --no-build
```

## Useful Container Commands

Stop the local stack:

```bash
docker compose -f compose.dev.yaml stop
```

Start it again:

```bash
docker compose -f compose.dev.yaml up -d
```

Remove the local stack and volumes:

```bash
docker compose -f compose.dev.yaml down -v
```

## Production Notes

- `KEYCLOAK_BASE_URL` is required in production and must be the public HTTPS Keycloak URL, for example `https://auth.example.com`.
- `KEYCLOAK_BASE_URL` must match the issuer emitted by Keycloak, which is derived from `KC_HOSTNAME`.
- The production API rejects internal HTTP Keycloak URLs such as `http://keycloak:8080`.
- Partner onboarding requires `KEYCLOAK_ADMIN_CLIENT_ID`, `KEYCLOAK_ADMIN_CLIENT_SECRET`, and `KEYCLOAK_PARTNER_API_CLIENT_SECRET`.
