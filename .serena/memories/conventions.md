# Conventions
- Gameplay rules/mutations belong in `Konqvist.Data`; web map objects/layers project `*Data` models and refresh after hub notifications.
- `MapDataStore` owns mutable state and serializes access via `ProtectedInvoke`/`SemaphoreSlim`; startup initializes this singleton from `IMapDataLoader`.
- SignalR actions span `IGameHubServer` → `GameHubServer` mutation/broadcast → `IGameHubClient` notification → `GameHubClient`/`IBindableHubClient` callback. Hub method names use `nameof`.
- Razor components subscribe to `IBindableHubClient.On...`; callbacks that redraw use `InvokeAsync(StateHasChanged)`.
- New pages/routes require `GameModeRoutingService._routingRules` updates plus `[Authorize]` or `[AuthorizeGameRoles(...)]`.
- Models/contracts favor shared `Empty` sentinels, collection expressions, records for value contracts, and static `RoundData` factories.
- JSON uses camel-case, comments/trailing commas, OpenLayers coordinate converters; `Konqvist.Data\Data\*.json` is copied to build/publish output. Rounds are currently defined by `MapDataLoader` factories.
- Tests use xUnit `[Fact]`, underscore-separated behavior names, and `IAsyncLifetime`; `GameDataLoader` supplies in-memory scenarios.