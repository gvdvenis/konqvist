# Repository instructions

## Build and test

Run commands from the repository root. The solution is under `src`, while its test project is under `tests`.

```powershell
dotnet restore .\src\Konqvist.sln
dotnet build .\src\Konqvist.sln --configuration Release
dotnet test .\src\Konqvist.sln --configuration Release
```

Run one xUnit test by fully qualified name:

```powershell
dotnet test .\tests\Konqvist.Data.Tests\Konqvist.Data.Tests.csproj --filter "FullyQualifiedName=Konqvist.Data.Tests.MapDataStoreTests.Claiming_Districts_Should_Only_Be_Allowed_Once_Per_Round"
```

Run the Blazor app with:

```powershell
dotnet run --project .\src\Konqvist.Web\Konqvist.Web.csproj
```

Local HTTPS on other devices requires an elevated terminal and `src\setup-local-ssl.cmd`; it generates certificates in `src\.certs`. The project targets .NET 9.

## Architecture

- `Konqvist.Data` is the game engine and in-memory state layer. `MapDataLoader` reads `map.json` and `teams.json` copied beside the built assembly and supplies the hard-coded round sequence. `MapDataStore` is the singleton aggregate for teams, districts, rounds, claims, votes, resources, scores, logins, and positions; its mutable operations are serialized through a `SemaphoreSlim`.
- `Konqvist.Web` is a Blazor Interactive Server application. Razor pages represent role- and round-specific views; Fluent UI supplies controls, and OpenLayers.Blazor plus `wwwroot/js/mapInterop.js` handle the map and browser interop.
- SignalR synchronizes every browser. UI code calls the scoped `IBindableHubClient`; `GameHubClient` sends strongly named server methods and exposes callback properties; `GameHubServer` mutates the singleton `MapDataStore` before broadcasting the corresponding `IGameHubClient` event to all clients.
- Cookie authentication creates a `UserSession` with one of `Anonymous`, `GameMaster`, `TeamLeader`, or `Runner`. `GameModeRoutingService` combines the role with the current `RoundKind` to force navigation among management, map, voting, waiting, and game-over pages.
- `Konqvist.Data.Tests` exercises complete gameplay sequences against `MapDataStore` with an in-memory `GameDataLoader`, not the production JSON files.

## Repository conventions

- Communicate in plain, direct language. Avoid unnecessary jargon and technically bloated terminology; explain unfamiliar technical choices simply when they are needed.
- Keep authoritative gameplay rules and mutations in `Konqvist.Data`; web map models/layers are projections of `*Data` models and refresh from the store after hub events.
- When adding a synchronized action, update the complete SignalR contract: `IGameHubServer` for browser-to-server calls, `GameHubServer` for mutation/broadcast, `IGameHubClient` for server-to-browser notifications, and `GameHubClient`/`IBindableHubClient` for handler registration and component callbacks. Use `nameof(...)` for SignalR method names.
- Components subscribe to `IBindableHubClient.On...` callbacks during initialization and marshal UI refreshes through `InvokeAsync(StateHasChanged)`.
- New routes must be reflected in `GameModeRoutingService._routingRules`; access is additionally declared on pages with `[Authorize]` or `[AuthorizeGameRoles(...)]`.
- Data models and contracts commonly use shared `Empty` sentinels instead of returning `null`, collection expressions (`[]`, `[.. values]`), records for value-like contracts, and static factories for valid `RoundData` variants.
- JSON loading uses camel-case property names, permits comments and trailing commas, and registers OpenLayers coordinate converters. Keep `Konqvist.Data\Data\*.json` as content copied to build and publish output.
- Tests are xUnit `[Fact]` methods named in underscore-separated behavior form. `MapDataStoreTests` implements `IAsyncLifetime` so each test instance initializes a fresh store backed by `GameDataLoader`.
