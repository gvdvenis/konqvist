# KONQVIST — Copilot Instructions

## Project
Full-stack real-time cycling game. Blazor WASM client, ASP.NET Core server, SignalR, 
Fluxor, SQLite. See /docs/PRD.md and /docs/BUILD_PLAN.md for full specification.

## Hard rules — never violate these
- Vertical Slice Architecture: one feature = one self-contained folder. Never horizontal layers.
- No .razor file exceeds ~100 lines of markup. Extract components aggressively.
- All JSON serialization uses System.Text.Json source generators. Never reflection-based serialization.
- Never use dynamic types, Assembly.LoadFile, or Reflection.Emit (AOT compatibility).
- MudBlazor for all standard UI controls. Do not hand-roll buttons, dialogs, or inputs.
- All game rule changes go through GameAggregate. Never mutate DbContext directly from a controller.
- Only these 8 events are persisted to the GameEvent WAL table: DistrictClaimed, VoteCast, 
  VotingOpened, VotingClosed, RoundAdvanced, RunnerLogin, RunnerLogout, GamePhaseChanged.
- Every EF Core schema change requires a new migration. Never hand-edit the database.
- Migration naming: {MilestoneNumber}_{SliceNumber}_{Description} e.g. 1_2_InitialSchema

## Stack
- .NET 10, Blazor WASM (Client), Blazor SSR (Admin), ASP.NET Core (Server)
- EF Core + SQLite, Fluent API, one IEntityTypeConfiguration<T> per entity
- Fluxor for client state, SignalR for real-time, MudBlazor for UI components
- Tailwind CSS for custom styling on top of MudBlazor
- Mediator.SourceGenerator (not MediatR), Serilog, NetTopologySuite, SharpKml, FluentValidation

## Build commands
dotnet build Konqvist.sln
dotnet run --project src/Konqvist.Server
dotnet test
dotnet publish -c Release (AOT build — only run when explicitly requested)