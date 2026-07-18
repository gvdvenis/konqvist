# Store gameplay state as a buffered aggregate in Azure SQL

- **Status**: Accepted
- **Date**: 2026-07-18
- **Related issues**: #15 (parent spec), #13 (alignment spec), #9, #10, #11, #12
- **Companion ADR**: [Reset on game-definition drift](0001-reset-on-game-definition-drift.md)

## Context

Konqvist keeps an in-memory aggregate of the current match while a game is running. That aggregate changes often (district ownership, votes, scores, team positions, login state, round progress), and we need to persist it so an app recovery event (`restart`) can restore the latest gameplay state without replaying the whole match. We also need a clean separation between the immutable game definition (seeded map, districts, teams, round sequence) and the mutable gameplay state, so a user-invoked `reset` can start a new game from the current definition without losing the definition itself.

Earlier persistence work (#9-#12) aligned vocabulary and scope. Spec #15 sets the concrete storage choices. This ADR records those choices in one place so future work does not relitigate them.

Operating constraints that shaped the decision:

- The app runs as **one active instance**. There is no need to coordinate writes across instances yet.
- Gameplay state changes far more often than it needs to reach disk. A write per change would overload the database for no benefit.
- Operators host the app in every environment (dev, staging, production) the same way, so the store should be the same in every environment.
- The team already works with EF Core and prefers migrations over hand-run SQL.

## Decision

Use **Azure SQL Database** as the durable store for gameplay state, accessed through **EF Core** with migrations applied at startup.

### Project and file layout

- EF Core files live under `Konqvist.Data/Infrastructure`.
- `Konqvist.Web` is the EF startup project. We do **not** add a design-time factory; migrations run from the web host at startup.

### Schema

- A single table `GameplayStates` holds gameplay state.
- There is **one row per `(Slot, GameDefinitionId)`**. `Slot` identifies the configured game slot; `GameDefinitionId` identifies the immutable game definition the row belongs to.
- The payload column is `nvarchar(max)` with a `CHECK` constraint using `ISJSON` so only valid JSON can be written.
- We **retain rows for old game definitions**. When the game definition changes, a new row is written for the new `GameDefinitionId`; the old row is kept, not updated or deleted.

### Connection and configuration

- The connection string is held in the `GameplayStateDatabase` secret and is never logged.
- Save behavior is governed by `IOptions<GameplayStatePersistenceOptions>`. The save interval **defaults to 1 second** and is bounded to **[1, 60] seconds**. Options are read once at startup; there is **no live reload**.

### Buffered, coalesced writes

- Gameplay state is written through a buffered path, not directly on every change.
- The **first real change** starts one delayed save (a single timer). Later changes **replace the pending state without restarting the timer**. This coalesces a burst of changes into one write.
- Dirty and scheduler signals use `Interlocked` operations so the writer and the UI thread do not race.
- A **single-writer gate** ensures only one save is in flight at a time.

### Reset and restart

- A user-invoked `reset` follows the same buffered path: it produces a new gameplay state that is written through the buffer, rather than issuing a direct database call.
- An app `restart` reads the row for the current `(Slot, GameDefinitionId)` and restores gameplay state from the stored JSON.

### Failure handling

- If a save fails, the writer **retries the latest pending state** (not the failed attempt). Earlier coalesced states are already superseded and are not retried.
- On shutdown, the writer performs a **5-second flush** so the most recent gameplay state is not lost on a normal stop.

### Logging

- Logging is **transition-based**: we log when the system moves from healthy to failing and from failing back to healthy, not on every attempt.
- Only **allowlisted fields** appear in logs: server, database, and encryption indicators. Secrets (notably the connection string) are never logged.

## Consequences

### Positive

- One store in every environment keeps dev, staging, and production behavior identical and removes a class of "works on my machine" gaps.
- EF Core migrations at startup mean the schema is applied and versioned with the code; no manual SQL steps for operators.
- Buffered coalesced writes turn many small changes into one write per interval, protecting the database from per-move write storms.
- Keeping old-definition rows makes a `reset` after a definition change cheap and non-destructive: the new definition gets a fresh row and the old data stays for audit or rollback.
- `ISJSON` validation catches malformed payloads at the database boundary.
- `Interlocked` signals plus a single-writer gate keep the buffer correct without heavier locking.

### Negative

- One active instance is a hard assumption. Multi-instance deployment would need write coordination (e.g., leasing or a queue) that we have not built.
- `nvarchar(max)` stores JSON as text; queries into the payload are limited compared with a native JSON type or normalized columns.
- Retaining old-definition rows means the table grows with definition changes; cleanup is a manual/operator concern for now.
- No live reload of options means a save-interval change needs an app restart.

### Neutral

- Gameplay state is stored as an opaque validated JSON blob. The schema validates shape, not meaning, so the in-memory aggregate remains the source of truth for structure.
- Migrations at startup add a small startup cost and require the startup identity to have DDL permissions.

## Alternatives Considered

- **Normalized gameplay tables (one column per field).** Rejected: it couples the schema to the in-memory model and forces a migration for every gameplay-state field change. The non-goals of #15 rule this out.
- **Native JSON column type.** Rejected for now: Azure SQL JSON support is adequate over `nvarchar(max)` with `ISJSON`, and a native type would add coupling for little gain at the current scale.
- **Direct write on every change.** Rejected: gameplay state changes far more often than it must reach disk. The buffered path protects the database and still loses no more than the last interval on a crash.
- **Multi-instance write coordination (leasing/queue).** Rejected as a non-goal: the app runs as one active instance. Adding coordination now would be speculative.
- **Testcontainers for integration tests.** Rejected as a non-goal of #15; tests use lighter arrangements.
- **Payload versioning inside the JSON.** Rejected: the in-memory aggregate is the source of truth and old-definition rows are retained separately, so in-payload versioning adds complexity without a current need.
- **Live reload of persistence options.** Rejected: options are read once at startup for predictable behavior and simpler reasoning about the buffer.
- **Automatic cleanup of old-definition rows.** Rejected: retention is intentional (audit/rollback), and auto-cleanup would work against that until an explicit policy is needed.

## Non-goals

Per spec #15, the following are explicitly **out of scope**:

- Multi-instance write coordination.
- Normalized gameplay tables.
- Native JSON column type.
- Payload versioning inside the JSON.
- Live reload of `GameplayStatePersistenceOptions`.
- Testcontainers-based integration tests.
- Automatic cleanup of old-definition rows.
- Event sourcing.

## References

- #15 - Spec: Azure SQL Persistence for Gameplay State (parent)
- #13 - Spec: Incremental gameplay-state persistence alignment (#9-#12)
- #9, #10, #11, #12 - Earlier incremental alignment work
- [Reset on game-definition drift](0001-reset-on-game-definition-drift.md) - companion ADR on discarding gameplay state when the definition hash changes
- `CONTEXT.md` - domain vocabulary (gameplay state, game definition, reset, restart)
