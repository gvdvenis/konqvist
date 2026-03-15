# KONQVIST — Copilot Instructions

## Project
Real-time, location-based cycling game. Teams of cyclists claim districts on a map, then vote
on outcomes each round. Blazor WASM client (players + GM), Blazor SSR admin, ASP.NET Core
server, SignalR, Fluxor, SQLite. Full spec: `/docs/PRD.md` and `/docs/BUILD_PLAN.md`.

## Hard rules — never violate these
- **Vertical Slice Architecture:** one feature = one self-contained folder. Never horizontal layers.
- **No .razor file exceeds ~100 lines of markup.** Extract components aggressively.
- **All JSON serialization uses System.Text.Json source generators.** Never reflection-based serialization.
- **Never use dynamic types, `Assembly.LoadFile`, or `Reflection.Emit`** (AOT compatibility).
- **MudBlazor for all standard UI controls.** Do not hand-roll buttons, dialogs, or inputs.
- **All game rule changes go through `GameAggregate`.** Never mutate `DbContext` directly from a controller.
- **Only these 8 events are persisted** to the `GameEvent` WAL table: `DistrictClaimed`, `VoteCast`,
  `VotingOpened`, `VotingClosed`, `RoundAdvanced`, `RunnerLogin`, `RunnerLogout`, `GamePhaseChanged`.
- **Every EF Core schema change requires a new migration.** Never hand-edit the database.
- **Migration naming:** `{MilestoneNumber}_{SliceNumber}_{Description}` e.g. `1_2_InitialSchema`

## Build & test commands
```
dotnet build Konqvist.slnx
dotnet run --project src/Konqvist.Server
dotnet test
dotnet test --filter "FullyQualifiedName~ClassName"   # run a single test class
dotnet test --filter "DisplayName~TestMethodName"      # run a single test method
dotnet publish -c Release                              # AOT build — only when explicitly requested
```
Always run `dotnet test` after implementing server-side logic.

## Stack
- .NET 10, Blazor WASM (`Konqvist.Client`), Blazor SSR (`Konqvist.Admin`), ASP.NET Core (`Konqvist.Server`)
- EF Core + SQLite, Fluent API, one `IEntityTypeConfiguration<T>` per entity
- Fluxor for client state, SignalR for real-time, MudBlazor for UI components
- Tailwind CSS for custom styling on top of MudBlazor
- `Mediator.SourceGenerator` (not MediatR — it has commercial licensing), Serilog, NetTopologySuite, SharpKml, FluentValidation
- OpenLayers.Blazor for map rendering with OpenStreetMap tiles

## Solution structure
```
Konqvist.sln
├── src/
│   ├── Konqvist.Client/        # Blazor WASM — Player PWA + GM interface (/gm/* routes)
│   │   ├── Features/           # One folder per feature (Map, Voting, Scores, Login, GM, Waiting)
│   │   │   └── {Feature}/
│   │   │       ├── {Feature}Page.razor
│   │   │       ├── Store/      # Fluxor State, Actions, Reducers, Effects
│   │   │       └── Components/ # Feature-scoped components
│   │   ├── Core/
│   │   │   ├── SignalR/        # GameHubService — connects hub, dispatches Fluxor actions
│   │   │   ├── State/          # Root Fluxor app state
│   │   │   ├── Auth/           # AuthenticationStateProvider, /api/auth/me
│   │   │   └── Geolocation/    # watchPosition wrapper + throttle
│   │   └── Shared/             # Reusable components (TeamColorBadge, CountdownTimer, OfflineBanner…)
│   ├── Konqvist.Admin/         # Blazor SSR — admin UI (templates, districts, session mgmt)
│   │   └── Features/           # GameTemplates, Districts, Teams, Rounds, Session
│   ├── Konqvist.Server/        # ASP.NET Core — API, SignalR hub, Game Aggregate, WAL
│   │   ├── Features/           # Vertical slices: Auth, Game, Districts, Voting, Resources, Location…
│   │   ├── Domain/
│   │   │   ├── Aggregates/     # GameAggregate.cs — singleton, all gameplay mutations
│   │   │   └── Events/         # Domain event definitions
│   │   └── Hubs/               # GameHub.cs at /hubs/game
│   └── Konqvist.Infrastructure/ # EF Core DbContext, entities, migrations, configs
│       ├── Entities/
│       │   ├── Template/       # GameTemplate, TeamTemplate, PlayerTemplate, DistrictTemplate, RoundTemplate
│       │   └── Session/        # GameSession, TeamSession, PlayerSession, DistrictSession, RoundSession, Vote, GameEvent, snapshots
│       ├── Persistence/        # KonqvistDbContext, Migrations/, Configurations/
│       └── Repositories/
└── tests/
    ├── Konqvist.Server.Tests/
    └── Konqvist.Infrastructure.Tests/
```

**Project reference rules:** `Server → Infrastructure`, `Admin → Infrastructure`. `Client` has NO Infrastructure reference.

## Architecture: Command → Event → Broadcast flow

All gameplay mutations follow this server-side pipeline — never bypass it:

```
API / SignalR Hub
      ↓
GameCommand (e.g. CastVoteCommand)
      ↓
GameAggregate (validates + processes)
      ↓
GameEvent produced (e.g. VoteCastEvent)
      ↓
Append to WAL (persisted events only — see Hard Rules)
      ↓
Apply Event → Update in-memory GameAggregate state
      ↓
SignalR broadcast → all clients
      ↓
Fluxor reducers update client stores
```

`GameAggregate` is registered as a **singleton**. It holds authoritative in-memory state: `GamePhase`, current round, team scores, district ownership.

## Data model — two-layer pattern

All entities exist in pairs:
- **Template layer** (`GameTemplate`, `TeamTemplate`, `PlayerTemplate`, `DistrictTemplate`, `RoundTemplate`) — reusable, static blueprints defined in the admin UI
- **Session layer** (`GameSession`, `TeamSession`, `PlayerSession`, `DistrictSession`, `RoundSession`, `Vote`, `GameEvent`, `RoundSnapshot`, `DistrictOwnershipSnapshot`) — live game state for one playthrough

Snapshots are taken **twice per round**: `EndOfGathering` and `EndOfVoting`. Only one `GameSession` is active at a time.

## Client state management (Fluxor)

Each feature has its own Fluxor store under `Features/{Feature}/Store/`:
- `GameStore` — `GamePhase`, round number, session id
- `MapStore` — district ownership dict, runner positions
- `VotingStore` — votes per team, voting-enabled flag, timer
- `ScoresStore` — team scores and resource totals
- `PlayerStore` — own identity, team, role, online state

**`GameHubService`** (`Core/SignalR/`) connects to `/hubs/game`, receives every SignalR event, and dispatches the corresponding Fluxor action. On reconnect it calls `GET /api/session/state` and dispatches a `FullStateSyncAction` to all stores.

**Phase-driven navigation:** `GamePhaseChanged` events trigger a `PhaseNavigator` Fluxor effect that calls `NavigationManager.NavigateTo()` — UI never navigates directly on game logic.

## Game phases and routing

| Phase | Runners navigate to | Team Leaders | GM |
|---|---|---|---|
| `WaitingForPlayers` | `/waiting` | `/waiting` | `/gm` |
| `Gathering` | `/map` | `/map` (read-only) | stays on current page |
| `Voting` | `/vote` (spectator) | `/vote` | `/gm` |
| `RoundResolution` | `/vote` (results) | `/vote` (results) | `/gm` |
| `Finished` | `/finished` (no scores) | `/finished` (no scores) | `/finished` (full breakdown) |

GM interface lives at `/gm/*` within `Konqvist.Client` — role-based routing, not a separate app.

## C# code style
- Nullable reference types enabled
- File-scoped namespaces
- Primary constructors where appropriate (.NET 10)
- No `#region` blocks
- Enums stored as strings in the database

## Build plan context
The project is built milestone-by-milestone per `/docs/BUILD_PLAN.md`. **Never implement ahead of the current slice.** Each slice ends at a verifiable state. When starting a session, establish the current milestone/slice from the build plan before making changes.

## Git workflow
- Do not create git commits unless the developer explicitly asks for one.
