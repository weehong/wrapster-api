# Local Setup

This guide starts the local infrastructure with `docker run` and then runs the Wrapsfer API with .NET.

## Prerequisites

- Docker
- .NET SDK `10.0.201` or a compatible latest feature SDK
- Optional: Entity Framework CLI tools for database migrations

```bash
dotnet tool install --global dotnet-ef
```

## 1. Create Docker Network

Create the shared Docker network before starting any containers.

```bash
docker network create workspace-network
```

If the network already exists, Docker will return an error. That is safe to ignore.

## 2. Start PostgreSQL

```bash
docker run \
  --name postgres \
  --network workspace-network \
  --env POSTGRES_USER=vernon \
  --env POSTGRES_PASSWORD=password \
  --publish 5432:5432 \
  --volume db_data:/var/lib/postgresql \
  --detach \
  --restart unless-stopped \
  postgres:latest
```

This creates a PostgreSQL server at `localhost:5432` with:

- Username: `vernon`
- Password: `password`
- Default database: `vernon`

The API expects a database named `wrapsfer`, so create it once after PostgreSQL starts:

```bash
docker exec postgres createdb -U vernon wrapsfer
```

## 3. Start Keycloak

```bash
docker run -d -p 8180:8080 \
  --name keycloak \
  --network workspace-network \
  -e KC_BOOTSTRAP_ADMIN_USERNAME=admin \
  -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
  -e KC_DB=postgres \
  -e KC_DB_URL=jdbc:postgresql://postgres:5432/vernon \
  -e KC_DB_USERNAME=vernon \
  -e KC_DB_PASSWORD=password \
  quay.io/keycloak/keycloak:latest \
  start-dev
```

Keycloak admin console:

- URL: `http://localhost:8180/admin`
- Username: `admin`
- Password: `admin`

This command starts a blank Keycloak instance. Configure or import the required realms and clients before making authenticated API requests.

## 4. Start Redis

```bash
docker run \
  --name redis \
  --network workspace-network \
  --publish 6379:6379 \
  --detach \
  --restart unless-stopped \
  --memory="512m" \
  --volume $HOME/docker/redis-data:/data \
  redis:latest \
  redis-server --requirepass password --appendonly yes
```

Redis is available at `localhost:6379` with password `password`.

## 5. Start RabbitMQ

```bash
docker run \
  --name rabbitmq \
  --network workspace-network \
  --env RABBITMQ_DEFAULT_USER=vernon \
  --env RABBITMQ_DEFAULT_PASS=password \
  --publish 5672:5672 \
  --publish 15672:15672 \
  --volume $HOME/docker/log/rabbitmq:/var/log/rabbitmq \
  --detach \
  --restart unless-stopped \
  rabbitmq:management
```

RabbitMQ management UI:

- URL: `http://localhost:15672`
- Username: `vernon`
- Password: `password`

## 6. Configure The API

Use environment variables or .NET user secrets for local overrides. User secrets are preferred for passwords because they are not committed to the repository.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=wrapsfer;Username=vernon;Password=password" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "Keycloak:BaseUrl" "http://localhost:8180" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "Keycloak:RequireHttpsMetadata" "false" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "RabbitMq:HostName" "localhost" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "RabbitMq:Port" "5672" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "RabbitMq:UserName" "vernon" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "RabbitMq:Password" "password" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
dotnet user-secrets set "RabbitMq:VirtualHost" "/" --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

## 7. Restore And Build

```bash
make restore
make build
```

Equivalent commands:

```bash
dotnet restore
dotnet build --no-restore
```

## 8. Apply Database Migrations

Set the design-time connection string so EF uses the local PostgreSQL container.

```bash
export WRAPSFER_DESIGN_CONNECTION="Host=localhost;Port=5432;Database=wrapsfer;Username=vernon;Password=password"
dotnet ef database update \
  --project src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj \
  --startup-project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

## 9. Run The API

```bash
dotnet run --project src/Wrapsfer.Api/Wrapsfer.Api.csproj
```

Useful local endpoints:

- Health check: `http://localhost:5222/health` or `https://localhost:7065/health`
- OpenAPI document in Development: `http://localhost:5222/openapi/v1.json` or `https://localhost:7065/openapi/v1.json`

Use the port printed by `dotnet run` if it differs from the launch profile defaults.

## 10. Run Tests

```bash
make test
```

Equivalent command:

```bash
dotnet test --no-restore --no-build
```

## Useful Container Commands

Stop the local containers:

```bash
docker stop postgres keycloak redis rabbitmq
```

Start existing containers again:

```bash
docker start postgres keycloak redis rabbitmq
```

Remove the containers:

```bash
docker rm -f postgres keycloak redis rabbitmq
```

Remove the shared network:

```bash
docker network rm workspace-network
```

## Notes

- The tracked `appsettings.Development.json` uses different local ports and credentials than this guide, so keep local overrides in user secrets or environment variables.
- Keycloak must have the expected realms, client, roles, and users configured before authenticated endpoints can be exercised.
- If Infisical is not configured, the API logs that it is skipping secret fetch and continues with local configuration.
