# Copilot Instructions

## Project shape

This is a .NET 10 task API used as a GitHub Copilot workshop. It follows a small Clean Architecture split:

- `SmartTask.Domain` owns the immutable `TaskItem` entity and its invariants.
- `SmartTask.Application` defines `ITaskRepository` and `ITaskService`, and implements task use cases in `TaskService`.
- `SmartTask.Infrastructure` implements the application repository contract with a thread-safe in-memory store.
- `SmartTask.Api` composes dependency injection in `Program.cs`, maps HTTP endpoints in `Endpoints/TaskEndpoints.cs`, and owns request contracts and HTTP validation.
- `SmartTask.Api.Tests` contains both API integration tests and domain/repository tests.

Keep dependencies pointing inward: the Domain has no project dependencies; Application depends on Domain; Infrastructure implements Application contracts; and the API wires Application to Infrastructure. The in-memory repository is intentional for this workshop; its data is shared within one application instance but is not persisted across restarts.

## Build and test

Run from the repository root:

```powershell
dotnet build SmartTask.slnx
dotnet test SmartTask.slnx
```

Run one test by filtering its fully qualified name:

```powershell
dotnet test SmartTask.Api.Tests\SmartTask.Api.Tests.csproj --filter "FullyQualifiedName~SmartTask.Api.Tests.TaskDomainAndRepositoryTests.Domain_enforces_limits_and_updates_without_mutating_the_original"
```

## Repository conventions

- Keep `TaskItem` immutable; create it with `TaskItem.Create` and change it with `TaskItem.Update`. Domain rules (required/nonblank trimmed title and length limits) belong in the entity, with request validation also expressed on API request contracts.
- Keep HTTP mapping in the minimal API endpoint group. Forward request cancellation tokens to the application layer, use typed results for explicit success/not-found outcomes, and retain the documented CRUD semantics: create returns `201` with a resource URL, delete returns `204`, and missing resources return `404`.
- Keep repository storage behind `ITaskRepository`. The current singleton in-memory implementation guards its dictionary with a lock, checks cancellation before each operation, and orders list results by creation time then ID; preserve these guarantees if changing the implementation.
- Use `WebApplicationFactory<Program>` for HTTP-level tests and direct entity/repository tests for lower-level behavior. Tests use xUnit and cover validation boundaries, resource semantics, cancellation, ordering, concurrency, and OpenAPI metadata.
