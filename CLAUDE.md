# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Development Commands

```bash
make restore          # dotnet restore
make build            # dotnet build (requires restore)
make test             # dotnet test (requires restore + build)
make format-all       # run both whitespace formatting and code-style fixes
make clean            # dotnet clean
```

To run a single test:
```bash
dotnet test --no-restore --no-build --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

Local infrastructure (PostgreSQL, RabbitMQ, Keycloak):
```bash
docker compose up -d
```

## Code Conventions

- Every class, record, struct, enum, and interface must have its own dedicated file. Never define multiple types in a single file.
- **Never use `var`** - always use explicit types. The pre-commit hook rejects `var` usage and `dotnet format style` enforces this via `.editorconfig`.
- No consecutive blank lines (max 1). Enforced by pre-commit hook.
- `TreatWarningsAsErrors` is enabled globally - all warnings are build failures.
- Private/internal fields: `_camelCase`. Static fields: `s_camelCase`. Constants: `PascalCase`.
- Nullable reference types are enabled globally.

## Critical Rules

- **Tenant isolation**: Every repository method that reads or mutates data MUST accept and filter by `tenantId`. Never query by ID alone without tenant scoping.
- **Domain validation**: Domain entity factory methods and mutators that can fail MUST return `Result` or `Result<T>`. Never use `void` for operations that have invariants (e.g. stock mutations). Define all error constants in the entity's `Errors` class — no inline `new Error(...)`.
- **Concurrency control**: Any entity with fields subject to concurrent updates (e.g. stock quantities) MUST have a concurrency token (`xmin` for PostgreSQL) configured in its EF configuration.
- **No N+1 queries**: Never fetch related entities in a loop. Use batch methods (`GetByIdsAsync`) and project data in a single query.
- **No internal details in API responses**: Exception handlers must never leak internal messages (database errors, stack traces, configuration details) to the client. Log the details server-side, return a generic message.
- **No silent fallback credentials**: Infrastructure configuration (database, message queue, etc.) must fail fast with `throw` on missing config — never fall back to default credentials like `"guest"`.
- **Collection encapsulation**: Domain entity collection navigation properties must be `IReadOnlyCollection<T>` backed by a private `List<T>` field. Never expose mutable `ICollection<T>`.
- **Code review on new files**: Always run a CodeRabbit code review (via the `coderabbit:review` skill or `coderabbit:code-reviewer` agent) whenever new files are added to the project. Do this proactively before committing.

## Architecture

Clean Architecture with CQRS (MediatR) and DDD patterns. .NET 10.

**Layer dependency: Api -> Application -> Domain <- Infrastructure**

- **Wrapster.Domain** - Entities (aggregate roots), value objects (`Result<T>`, `Error`), domain events, repository interfaces. No external dependencies.
- **Wrapster.Application** - Commands/queries with MediatR handlers and FluentValidation validators. Pipeline behaviors auto-validate and log all requests.
- **Wrapster.Infrastructure** - EF Core + PostgreSQL persistence, Keycloak multi-tenant auth, RabbitMQ publishing, Resend email, Infisical secrets.
- **Wrapster.Api** - ASP.NET Core controllers, versioned routes (`api/v1/...`), request/response contracts.

### Key Patterns

- **Result<T> pattern**: Commands/queries return `Result<T>` or `Result`. Controllers use `ToActionResult()` / `ToCreatedResult()` from `ApiControllerBase` to map errors to HTTP status codes (NotFound->404, Validation->400, Conflict->409).
- **Multi-tenancy**: TenantId resolved from Keycloak JWT claims. All repository queries filter by TenantId.
- **Domain events**: Entities raise events (e.g. `LowStockDetectedEvent`), dispatched by `DomainEventInterceptor` on SaveChanges.
- **EF Interceptors**: `AuditableEntityInterceptor` (timestamps/user), `DomainEventInterceptor` (publishes events), `AuditLogInterceptor`.
- **Validation pipeline**: `ValidationBehavior<TRequest, TResponse>` automatically validates all MediatR requests before reaching handlers.

### Adding a New Feature

Follow existing Products module as reference:
1. **Domain**: Entity in `Domain/Products/`, repository interface in `Domain/Products/`
2. **Application**: Command/query + handler + validator in `Application/{Feature}/Commands/` or `Queries/`
3. **Infrastructure**: Repository in `Infrastructure/Persistence/Repositories/`, EF config in `Infrastructure/Persistence/Configurations/`
4. **Api**: Controller in `Api/Controllers/V1/`, contracts in `Api/Contracts/`
5. Register services in `Application/DependencyInjection.cs` and `Infrastructure/DependencyInjection.cs`

## Testing

- **Framework**: xUnit + Moq + FluentAssertions
- **Wrapster.Application.Tests**: Unit tests for handlers and validators (mocked repositories)
- **Wrapster.Domain.Tests**: Domain entity/value object tests
- **Wrapster.IntegrationTests**: WebApplicationFactory-based (scaffolded)
