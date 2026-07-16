# Konqvist core
- .NET 9 Blazor Interactive Server game app; solution: `src/Konqvist.sln`.
- `Konqvist.Data`: authoritative in-memory game state/rules. `MapDataStore` singleton aggregates teams, districts, rounds, claims, voting, scores, login state, and positions.
- `Konqvist.Web`: role/round-specific Razor UI, cookie auth, OpenLayers map, Fluent UI, SignalR synchronization.
- `Konqvist.Data.Tests`: gameplay-sequence tests against an in-memory loader.
- For framework/package/version details, read `mem:tech_stack`.
- For runnable workflows, read `mem:suggested_commands`; completion gates are in `mem:task_completion`.
- For state, SignalR, routing, and test patterns, read `mem:conventions`.