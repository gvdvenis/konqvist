# KONQVIST — Product Requirements Document
> Version 1.5 | March 2026

---

## Table of Contents
1. [Overview](#1-overview)
2. [Roles & Permissions](#2-roles--permissions)
3. [Authentication & Login](#3-authentication--login)
4. [Game Structure](#4-game-structure)
5. [Districts & Map](#5-districts--map)
6. [Resources & Scoring](#6-resources--scoring)
7. [Voting Phase](#7-voting-phase)
8. [Technical Architecture](#8-technical-architecture)
9. [Data Models](#9-data-models)
10. [Game Configuration & Setup](#10-game-configuration--setup)
11. [User Flows & Edge Cases](#11-user-flows--edge-cases)
12. [Non-Functional Requirements](#12-non-functional-requirements)
13. [Out of Scope](#13-out-of-scope)
14. [Solution Structure](#14-solution-structure)
15. [Database Schema](#15-database-schema)
16. [UI & Navigation](#16-ui--navigation)
17. [Offline Behaviour](#17-offline-behaviour)

---

## 1. Overview

KONQVIST is a real-time, location-based cycling game played simultaneously by multiple teams across a mapped area divided into districts. A GameMaster (GM) controls the flow of the game, advancing it through rounds. Players access the game via a Progressive Web App (PWA) on their mobile devices — no app store installation required.

The server follows a **Game Aggregate pattern** with an **event-driven Write-Ahead Log (WAL)** architecture, ensuring deterministic, replayable game state. Game state transitions are modeled via an explicit **GamePhase enum**. The client uses **Fluxor** for predictable state management driven by incoming SignalR events.

---

## 2. Roles & Permissions

### 2.1 Role Table

| Feature | Runner | Team Leader | Game Master |
|---|---|---|---|
| Map & districts | ✅ | ✅ | ✅ |
| Location tracking | ✅ | ❌ | ❌ |
| View voting screen | ✅ | ✅ | ✅ |
| Cast votes | ❌ | ✅ | ✅ |
| Scores overview | ❌ | ✅ | ✅ |
| Enable voting | ❌ | ❌ | ✅ |
| Advance rounds | ❌ | ❌ | ✅ |
| Edit resources | ❌ | ❌ | ✅ |
| Force-logout Runner | ❌ | ❌ | ✅ |
| Unrestricted navigation | ❌ | ❌ | ✅ |

### 2.2 Role Descriptions

**Runner (Cyclist)**
- Cycles around the play area claiming districts
- Has access to location tracking and navigation tools on the map
- Can view but not interact with the voting screen during voting phase
- Only one Runner per team may be logged in at any time

**Team Leader (Captain)**
- Stationary or non-cycling role focused on strategy and voting
- Has access to the Scores overview page
- Can cast votes during the voting phase
- Multiple Team Leaders per team are allowed simultaneously
- No location tracking available

**Game Master (GM)**
- Controls the pacing and progression of the entire game
- Has unrestricted access to all screens (Map, Voting, Scores) at all times, regardless of current game phase
- Can advance rounds, enable voting, edit resources, and force-logout Runners
- Multiple GMs may be logged in simultaneously
- GM interface lives inside the same Blazor application as the player app, accessible via the `/gm` route namespace, protected by role-based UI visibility

### 2.3 Runner Session Rules

- Only **one Runner per team** is allowed to be logged in at any time
- A second Runner login attempt for the same team is **blocked with an error message**
- This prevents teams from exploiting multiple simultaneous Runner sessions to claim districts from different locations
- The Runner slot is freed only by:
  - The Runner navigating to `/logout` (this route is intentionally hidden from the UI — considered impossible from the Runner's perspective)
  - The GM force-logging out the Runner via the management page

### 2.4 GM Force-Logout of Runner

- The GM can force-logout any Runner via a button on the team card on the Scores/Management page
- The button is only **enabled** when a Runner for that team is currently logged in
- When clicked, the Runner's session is terminated server-side
- If the Runner's client is currently online, they receive a `RunnerLoggedOut` SignalR event and are **immediately redirected** to the login page

---

## 3. Authentication & Login

### 3.1 Login Flow

- Players receive a **team-specific URL** shared via chat (WhatsApp, etc.), e.g. `/team/delta`
- Navigating to this URL presents a **login page** with two role buttons:
  - **Runner** — disabled if a Runner for this team is already logged in
  - **Team Captain** — always available; multiple logins permitted
- Game Masters navigate to their own reserved login URL, e.g. `/login/gm`
- Direct token-based login is also supported: `/login/{token}`

### 3.2 Login Token Format

Tokens follow the pattern `{prefix}{randomness}`:

| Role | Prefix Format | Example |
|---|---|---|
| Runner | `{TeamInitial}R` | `DR15ee` (Delta Runner) |
| Team Captain | `{TeamInitial}TC` | `ETC5y85` (Echo Team Captain) |
| Game Master | No prefix | `GM57t7` |

- **Team initials** are based on the NATO phonetic alphabet team names (Bravo, Charly, Delta, Echo, Foxtrot, etc.)
- **Randomness** is a 4-character alphanumeric string
- Tokens are non-guessable by design

### 3.3 Session Behaviour

- Sessions use **cookie-based authentication**
- Explicit logout is supported via `/logout`
- GM-forced logout is supported via the management page

---

## 4. Game Structure

### 4.1 Configuration

- A game consists of a **configurable number of rounds**, defined in the **GameTemplate** at setup time (default: 4)
- Only **one GameSession** can be active at a time
- A game is based on a reusable **GameTemplate** — define once, play many times
- All gameplay-relevant settings are stored in the `GameTemplate` and loaded into a `GameSettings` value object at game initialization. There is **no separate `appsettings.json` configuration** for game rules — all values live in the database template, ensuring each game instance is fully self-contained and reproducible.

| Setting | Description | Default | Scope |
|---|---|---|---|
| `TotalRounds` | Number of rounds per game | 4 | GameTemplate (DB) |
| `MinLocationUpdateIntervalSeconds` | Minimum interval between GPS updates sent from client | 5 | GameTemplate (DB) |
| `LocationUpdateIntervalSeconds` | Interval for broadcasting opponent locations | 30 | GameTemplate (DB) |
| `VotingDurationSeconds` | Duration of the voting window per round | 30 | GameTemplate (DB) |
| `PredictionBonusPoints` | Total bonus distributed to correct vote predictors | 150 | GameTemplate (DB) |
| `VoteTimeoutPenalty` | Point penalty applied to teams that fail to vote in time | configurable | GameTemplate (DB) |
| `DistrictCaptureRadiusMeters` | Default radius of trigger circles for claiming districts | 50 | GameTemplate (DB) |

### 4.2 Game Flow

```
1. GM creates a GameSession from a GameTemplate
2. Players navigate to their team URL → choose role → Waiting screen
3. GM starts the game → all clients transition to Gathering Phase (map view)
4. Runners cycle and claim districts → resources accumulate
5. GM advances to Voting Phase → all clients switch to voting screen
6. GM enables voting → countdown timer starts → Team Leaders cast votes
7. Voting closes (timer expires or GM ends it) → winner determined →
   perk/punishment awarded → prediction bonuses distributed →
   scores updated → snapshots taken
8. GM advances to next round → repeat from step 4
9. After final round → GameSession status set to Finished
   - GM sees full final scores/results
   - All other players see "Game Ended — Thanks for Playing" screen
```

### 4.4 Explicit Game Phase Model

Game state is modeled using a **`GamePhase` enum** that drives all major UI transitions client-side. The server emits a single `GamePhaseChanged` event whenever the phase changes, and clients react accordingly.

| Phase | Description |
|---|---|
| `WaitingForPlayers` | Game created but not yet started; players on waiting screen |
| `Gathering` | Active gathering phase; Runners claim districts |
| `Voting` | Active voting phase; Team Leaders cast votes |
| `RoundResolution` | Votes resolved, scores calculated, snapshots taken |
| `Finished` | All rounds complete; game ended |

**Benefits:**
- Simplified client UI logic — one event drives all major screen transitions
- Deterministic server state — no ambiguity about current phase
- Easier debugging and event log replay

### 4.3 Round Structure

Each round has two phases:

#### Phase 1 — Gathering Phase
- The map displays all districts with **trigger circles**
- Runners cycle to districts and claim them by entering a trigger circle
- Claimed districts fill with the team's **signature color** (semi-transparent overlay, map remains visible)
- Claiming adds that district's resources to the team's running totals
- The trigger circle is **removed** after claiming — no reclaiming within the same gathering phase
- At the start of each new gathering phase, all trigger circles **reactivate** — districts can be stolen or defended

#### Phase 2 — Voting Phase
- GM transitions all players to the voting screen
- GM **explicitly enables voting** before Team Leaders can interact
- A **countdown timer** (`VotingDurationSeconds`) starts when voting is enabled
- Each team casts votes based on their Voters resource total
- Team Leaders vote for **one other team** to win the stake
- Votes update in **real-time** on a bar chart visible to all players
- Once a Team Leader votes, **all vote buttons disable** — one vote per team per round
- If a team fails to vote before the timer expires:
  - A **random team is chosen** as their vote target
  - The non-voting team receives a **point penalty** (`VoteTimeoutPenalty`)
- The team with the most votes wins the **perk or punishment** described at the top of the screen
- After voting resolves, **prediction bonuses** are distributed (see Section 6)

---

## 5. Districts & Map

### 5.1 District Properties

Each district has:
- A **name**
- A **GeoJSON polygon** defining its boundaries
- A **trigger circle** (center point + configurable radius) for claiming
- **Resource values**: Gold, Voters, Likes, Oil
- A **current owner** (nullable — the team that last claimed it)
- A **claim timestamp** — recorded each time a district is claimed, enabling rollback, dispute resolution, and timeline reconstruction

### 5.2 District Behavior

- When claimed, a district fills with the claiming team's **signature color** (semi-transparent)
- The trigger circle is **removed** for the remainder of the current gathering phase
- District ownership **persists** between rounds — a team keeps a district until another team claims it
- At the start of each new gathering phase, all trigger circles reactivate, enabling stealing and defending

### 5.3 District Claim Validation

District claims follow strict deterministic rules to prevent duplicate events from GPS jitter, and are designed to be resilient to unstable network conditions.

#### Claim Trigger Rules
1. A claim is triggered **only once** when a Runner transitions from **outside** to **inside** the capture radius — continuous presence inside the circle does not re-trigger the claim
2. The server validates the claim and emits a `DistrictClaimed` event
3. Once claimed, the trigger circle is **disabled for that Runner** for the remainder of the gathering phase, preventing repeated events caused by GPS position fluctuation

#### SignalR Delivery and Reliability

SignalR **does not guarantee message delivery**. If a connection is unstable or briefly drops while a claim event is in flight from client to server, the message may be lost. SignalR does not buffer undelivered client→server messages on reconnection; it treats reconnection as a new connection.

**Implication for district claims:** A single fire-and-forget claim request is not reliable enough. If the connection drops at the exact moment of sending, the claim may never reach the server, leaving the Runner unaware.

**Chosen approach — Idempotent acknowledged claim with client-side retry:**

1. When a Runner's device detects entry into a trigger zone, the client sends a `ClaimDistrict` command to the server
2. The server responds with a confirmation (acknowledgement) — either a `DistrictClaimed` event broadcast (success) or an explicit rejection
3. If the client does **not receive a response within a short timeout**, it **re-sends the claim command**
4. The server processes the claim **idempotently** — if the district is already claimed by this team in this round, a duplicate command is silently discarded; a success acknowledgement is still sent
5. Re-sends are **throttled** (e.g. no more than once every 2–3 seconds) to avoid flooding the server
6. Once an acknowledgement is received, the client **stops retrying** and discards the queued claim

This ensures reliable delivery without requiring a formal message queue, while keeping the server's authoritative state deterministic.

### 5.4 District Info Popup

Tapping/clicking a district opens a popup showing:
- District name
- Resource values (Gold, Voters, Likes, Oil)
- Current owner (or `-` if unclaimed)
- The selected district is highlighted (Solid Outline, fill unchanged) in the popup

### 5.5 Runner Map Tools

| Button | Function |
|---|---|
| Tracking mode | Rotates/tilts the map in the direction of travel for navigation while cycling |
| Center on location | Recenters the map on the Runner's current position |
| Full map view | Zooms out to show the entire play area |
| Toggle other teams | Hides/shows other teams' positions on the map |

### 5.6 Team Colors & Signature Colors

- Each team has a unique **signature color**
- District overlays use a **semi-transparent fill** so the map remains legible beneath
- Team colors must be **visually distinguishable** from each other on the map

### 5.7 Static Map Initialization

- All static map data is loaded **once at session start** via `GET /api/map`
- This endpoint returns:
  - District polygon definitions (GeoJSON)
  - Capture circle positions and radii
  - District resource values
  - Team color definitions
  - Map metadata and game configuration
- Static data is **cached client-side** for the duration of the game session — it is never re-downloaded on reconnection or refresh
- This prevents repeated unnecessary server load

---

## 6. Resources & Scoring

### 6.1 Resource Types

Four resource types exist per district:

| Resource | Icon |
|---|---|
| Gold | 🪙 |
| Voters | 👥 |
| Likes | 👍 |
| Oil | 💧 |

### 6.2 Resource of Interest (ROI)

- Each round has a designated **Resource of Interest (ROI)**
- The ROI is defined per round in the GameTemplate (fixed at game creation)
- The ROI is **visually highlighted** in a distinct color across all screens
- The ROI resource **counts double** in the round's score calculation

### 6.3 Scoring Formula

```
Round Score = Gold + Voters + Likes + Oil + ROI_value
```
> The ROI resource value is added twice (once normally, once as the bonus).

- Scores are **cumulative** across all rounds

### 6.4 Prediction Bonus

After each voting round, a **prediction bonus** is distributed to teams that correctly predicted the winner:

1. Determine the **winning team** (most votes received)
2. Identify all teams that **voted for that team**
3. Distribute `PredictionBonusPoints` equally among correct predictors:

```
EachBonus = PredictionBonusPoints / CorrectPredictorCount
```

**Example** (`PredictionBonusPoints = 150`):

| Team | Voted For |
|---|---|
| Alpha | Delta |
| Bravo | Delta |
| Charlie | Alpha |

Winner: **Delta** — Alpha and Bravo each receive `150 / 2 = 75` bonus points.

### 6.5 GM Resource Editing

- The GM can tap any resource indicator on any team card on the Scores page
- A **Resource Bonus popup** allows entering a positive (bonus) or negative (penalty) integer
- Changes are applied immediately to the team's resource totals and broadcast via SignalR

---

## 7. Voting Phase

### 7.1 Voting Mechanics

- Each team's vote weight is determined by their **Voters resource** total
- Team Leaders cast votes for **one other team**
- Voting with 0 Voters is allowed — it expresses intent but adds nothing to the target's vote score
- Once a vote is cast, all vote buttons for that team are **disabled** — no revoting
- If a team does not vote before the timer expires, a **random vote** is cast on their behalf and a **penalty** is applied
- The randomly selected target **cannot be the voting team itself** — if only two teams exist, the other team is automatically chosen

### 7.2 Voting Timer

- The voting window is controlled by `VotingDurationSeconds` (configurable per GameTemplate)
- The countdown is visible to all players
- The voting end automatically if all teams have voted or the timer expires. 
- GM can also end voting early. In this case the current voting state freezes in time. Rewards get awarded based on the current voting state, no penalties apply for uncast votes.
- On timer expiry, uncast votes are auto-resolved with a random target and a penalty applied to the non-voting team

### 7.3 Voting UI

- All players see the voting screen during the voting phase (Runners as spectators)
- A **bar chart** shows live vote totals per team, updating in real-time
- Team bars are colored in each team's signature color
- The **stake** (perk or punishment for the winner) is shown at the top of the screen
- The **countdown timer** is prominently displayed
- Runners see a message indicating they cannot vote — only Team Leaders can cast votes

### 7.4 GM Voting Controls

- When the GM advances to the voting phase, all clients switch to the voting screen
- Voting does not begin immediately — the GM must **explicitly enable voting** via a dedicated control
- This gives the GM time to read out the stake or build anticipation before starting the timer
- The GM can end voting early once all teams have voted

### 7.5 Post-Voting Resolution

After voting closes:
1. Winner is determined by highest vote total
2. Perk/punishment is awarded to the winner
3. Prediction bonuses are calculated and distributed
4. Scores are updated for all teams
5. Round snapshots are taken (`EndOfVoting`)
6. All events are appended to the Write-Ahead Log

---

## 8. Technical Architecture

### 8.1 Hosting & Deployment

- Hosted on **Azure App Service** (Linux-based, single instance)
- Delivered as a **PWA (Progressive Web App)** — runs in mobile browsers, installable to home screen
- No native app store deployment required

### 8.2 Application Structure

The solution is a **single unified Blazor application** serving both player-facing and GM-facing UI. The GM interface is accessible via the `/gm` route namespace with role-based access control. This eliminates deployment complexity and allows full reuse of services, SignalR hubs, and state.

| Layer | Type | Route | Purpose |
|---|---|---|---|
| Player App | Blazor WASM | `/` | Client-side player-facing game interface |
| GM Interface | Blazor WASM | `/gm/*` | Game Master controls, embedded in same app |
| Admin App | Blazor SSR | `/admin` | Server-side game template configuration |
| API + Hub | ASP.NET Core | `/api/*`, `/hubs/*` | Game logic, SignalR, HTTP endpoints |

### 8.3 Frontend — Player & GM App

- **Blazor WASM** (WebAssembly) — runs entirely client-side in the browser
- **Target framework: .NET 10** — use all available .NET 10 Blazor improvements to reduce manual boilerplate
- **Fluxor** used for client-side state management:
  - Predictable, unidirectional state transitions via actions and reducers
  - Fluxor stores manage: player state, team scores, district ownership, voting state, map state
  - Incoming SignalR events dispatch Fluxor actions, which update the store via reducers
  - Clean separation between UI components and state logic
- Real-time UI updates via **SignalR**
- Map rendered using **OpenLayers.Blazor** with **OpenStreetMap** tile provider
- Styled with **Tailwind CSS** using a custom dark/fantasy-inspired theme
- Visual design language: dark backgrounds, gold/amber accents, subtle fantasy aesthetic (WoW-inspired)

#### UI Component Library — MudBlazor

**MudBlazor** is chosen as the UI component library for both the Player/GM app and the Admin app:
- MIT licensed and free for commercial use
- Written in pure C# with minimal JavaScript — well aligned with Blazor's model
- Confirmed to support **trimming** in production (partial trim mode, which is Blazor WASM's default)
- No known MudBlazor-specific AOT issues for Blazor WASM (browser target)
- Rich component set: dialogs, progress bars, charts, badges, tabs, navigation drawers, snackbars — all directly useful for game UI
- Strong community and active maintenance

MudBlazor replaces hand-rolled UI primitives for common controls (buttons, dialogs, badges, progress indicators, bar charts). Custom-styled variants using Tailwind/CSS variables can be layered on top for the game-specific visual identity.

#### Blazor Component Design Principles

**Component-first design is a hard requirement.** No large monolithic `.razor` files. Every identifiable UI unit — card, popup, bar, icon badge, map overlay, timer display, resource indicator, vote button — must be extracted into its own reusable component.

Design rules:
- **DRY (Don't Repeat Yourself):** If the same markup appears more than once, it becomes a component
- **Single responsibility:** Each component does one thing and accepts parameters for variation
- **Composability:** Pages are compositions of nested components, not self-contained blobs of markup
- **Parameters over conditionals:** Pass role or state as parameters, not if/else trees inside a component
- **Shared component library:** Common components (e.g. `TeamColorBadge`, `ResourceRow`, `CountdownTimer`, `OnlineIndicator`) live in `Konqvist.Client/Shared/` and are reused across all pages

Example component decomposition for the Map page:
```
MapPage.razor
 ├── MapContainer.razor          (OpenLayers map host)
 │    ├── DistrictOverlay.razor  (per-district colored polygon)
 │    └── RunnerMarker.razor     (per-runner position dot + label)
 ├── MapToolbar.razor            (tracking / center / zoom buttons)
 ├── DistrictPopup.razor         (tap-to-open district info)
 └── OfflineBanner.razor         (shown when disconnected)
```

#### .NET 10 Blazor Features — Required Usage

The following .NET 10 features must be leveraged wherever applicable to reduce manual code:

| Feature | Usage in KONQVIST |
|---|---|
| `[PersistentState]` attribute | Persist game session state (current phase, scores, districts) across reconnections and enhanced navigation without manual JSON serialization |
| `ReconnectModal` component | Use the built-in .NET 10 `ReconnectModal` (customized to match game UI) rather than implementing reconnection overlay from scratch |
| Enhanced navigation state | Static map and configuration data persisted with `[PersistentState(AllowUpdates = false)]` so it is never re-fetched after initial load |
| Static asset fingerprinting | Enable `<OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>` for the WASM project to benefit from automatic cache-busting |
| Source-generated validation | Use `.NET 10` source-generated validators (`builder.Services.AddValidation()`) in the Admin app for form validation without reflection |

#### AOT Compilation — Design Goal

**AOT (Ahead-of-Time) compilation is a target design goal** for the KONQVIST Client WASM app. AOT compiles .NET IL directly to WebAssembly, delivering faster runtime execution on mobile browsers — important for smooth map interaction during active gameplay.

**AOT is supported in .NET 10 WASM** via the `wasm-tools` workload:
```xml
<PropertyGroup>
  <RunAOTCompilation>true</RunAOTCompilation>
</PropertyGroup>
```

**AOT compatibility requirements that must be respected throughout implementation:**
- Avoid `System.Reflection.Emit` and dynamic code generation
- Avoid `Assembly.LoadFile` (dynamic assembly loading)
- Use `System.Text.Json` **source generators** for all JSON serialization instead of reflection-based serialization — this is critical for both AOT and trimming safety
- Prefer source-generated patterns (Mediator source generator, JSON source gen) over reflection-heavy alternatives
- MudBlazor is compatible with Blazor WASM trimming (partial trim mode) and has no known WASM AOT issues — safe to use
- AOT compilation only applies at publish time (Release configuration) — it does not affect development workflow
- Tradeoff: AOT-compiled WASM bundles are larger (~1.5–2×), but the app is cached after first load, so subsequent loads are fast; this is an acceptable tradeoff for a game app used repeatedly by the same players

### 8.4 Frontend — Admin App

- **Blazor SSR** (Server-Side Rendering) — server-rendered, no WASM overhead, but with selective interactive elements where applicable for smooth UX.
- Desktop-optimized; no mobile responsiveness requirement
- Styled with **Tailwind CSS** — clean, functional, neutral design language
- Protected by its own login, credentials configurable via `appsettings.json`

### 8.5 Backend — Server Architecture

- **.NET** server application
- Follows a **Game Aggregate pattern** — all gameplay mutations pass through a single domain aggregate:

```
GameAggregate
 ├── Teams
 ├── Players
 ├── Districts
 ├── VotingRound
 ├── ScoreState
 └── GameSettings
```

The aggregate is the single authoritative source for all state transitions, ensuring consistency and determinism.

**Example Commands processed by the aggregate:**

| Command | Description |
|---|---|
| `ClaimDistrict` | Runner enters a capture zone |
| `CastVote` | Team Leader submits a vote |
| `OpenVoting` | GM enables voting for the round |
| `CloseVoting` | Voting window ends (timer or GM) |
| `AdvanceRound` | GM moves game to next round |
| `ForceLogoutRunner` | GM terminates a Runner's session |

**Resulting Events emitted:**

| Event | Persisted |
|---|---|
| `DistrictClaimed` | ✅ |
| `VoteCast` | ✅ |
| `VotingOpened` | ✅ |
| `VotingClosed` | ✅ |
| `RoundAdvanced` | ✅ |
| `RunnerLoggedOut` | ✅ |
| `GamePhaseChanged` | ✅ |

- **Vertical Slice Architecture** — each feature is a self-contained slice (e.g. `ClaimDistrict`, `CastVote`, `AdvanceRound`)
- Slices may depend on shared core infrastructure but are **independent of each other**
- **SOLID principles** applied throughout

### 8.6 Command → Event Flow

All gameplay actions follow this server-side flow:

```
API / SignalR Hub
      │
      ▼
GameCommand (e.g. CastVoteCommand)
      │
      ▼
GameAggregate (validates + processes command)
      │
      ▼
GameEvent produced (e.g. VoteCastEvent)
      │
      ▼
Append to Write-Ahead Log (WAL)
      │
      ▼
Apply Event → Update In-Memory Game State
      │
      ▼
SignalR Broadcast → All clients
      │
      ▼
Fluxor reducers update client store
```

### 8.7 Write-Ahead Log (WAL) — Event Persistence

Instead of mutating state directly, all authoritative state changes are captured as **immutable events** appended to an event log. To avoid unnecessary database I/O, only **game-critical domain events** are persisted — high-frequency operational events remain in-memory only.

#### Persisted Events (stored in database)

These represent irreversible gameplay decisions or state transitions:

| Event | Description |
|---|---|
| `DistrictClaimed` | A Runner successfully claimed a district |
| `VoteCast` | A team cast their vote for a round |
| `VotingOpened` | GM enabled voting for the round |
| `VotingClosed` | Voting window closed (timer or GM) |
| `RoundAdvanced` | GM advanced the game to the next round |
| `RunnerLogin` | A Runner logged in to a team session |
| `RunnerLogout` | A Runner logged out (explicit or GM-forced) |
| `GamePhaseChanged` | The game transitioned to a new phase |

#### Non-Persisted Events (in-memory only)

These are high-frequency operational events that do not affect authoritative game state:

| Event | Reason |
|---|---|
| `RunnerLocationUpdated` | Very high frequency — not needed for reconstruction |
| `HeartbeatReceived` | Operational only — no gameplay significance |
| `ClientUIStateChanges` | Client-side only — not part of server state |

**Benefits:**
- Deterministic game reconstruction from persisted events alone
- Minimal database load during active gameplay
- Scalable real-time communication via in-memory events

### 8.8 Game State Storage Strategy

Two complementary storage strategies are used:

| Storage Type | What is Stored |
|---|---|
| **Persistent (SQLite)** | Player registrations, team definitions, district definitions, claim timestamps, WAL event log, final scores, completed rounds, snapshots |
| **In-Memory (server)** | Current round state, active voting, live district ownership, temporary scores during a round |

In-memory state avoids excessive database I/O during active gameplay. Full state is reconstructable from the WAL at any time.

### 8.9 API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/map` | Returns district definitions, static map data, team colors, and game configuration — loaded once at session start |
| `GET` | `/api/team/state` | Returns current team scores and resource totals |
| `POST` | `/api/location/update` | Submits a Runner's GPS location update |
| `POST` | `/api/district/claim` | Submits a district claim attempt |
| `POST` | `/api/vote` | Submits a team's vote for the current round |
| `GET` | `/api/vote/state` | Returns current voting totals for the active round |
| `POST` | `/api/session/logout` | Logs out the current player session |

### 8.10 Real-Time Architecture

All game state lives server-side. Clients are thin — they receive state updates pushed via SignalR. Communication follows **SignalR first, HTTP fallback**.

#### SignalR Events

| Event | Direction | Description |
|---|---|---|
| `GameStarted` | Server → All | Transitions all waiting players to map/gathering view |
| `DistrictClaimed` | Server → All | Updates district color and ownership on all clients |
| `DistrictOwnershipChanged` | Server → All | Broadcasts change in district owner |
| `PhaseChanged` | Server → All | Switches all clients to gathering or voting view |
| `VoteStarted` | Server → All | Voting enabled by GM; countdown timer begins |
| `VoteCast` | Server → All | Updates live vote bar chart for all clients |
| `VoteEnded` | Server → All | Voting closed; winner and bonuses announced |
| `ScoreUpdated` | Server → All | Broadcasts updated scores to all clients |
| `GameStateChanged` | Server → All | General game state change notification |
| `RoundEnded` | Server → All | Round complete; scores updated |
| `RunnerLoggedOut` | Server → Target | Forces targeted Runner client to redirect to login page |
| `LocationUpdated` | Server → Clients | Broadcasts runner location (subject to rules below) |
| `RunnerStateChanged` | Server → All | Broadcasts online/offline and login state of runners |

#### Location Update Rules — Two-Tier Strategy

Location updates follow a deliberate two-tier rate limiting strategy:

**Tier 1 — Runner → Server:**
- Runner devices send GPS updates no faster than `MinLocationUpdateIntervalSeconds` (default: 5s)
- This reduces battery drain and server load

**Tier 2 — Server → Other Clients:**
- The server broadcasts a Runner's location to **own teammates** in real-time
- The server broadcasts a Runner's location to **opposing teams** at `LocationUpdateIntervalSeconds` (default: 30s)
- The **Game Master** always receives all Runner location updates in real-time regardless of interval

This prevents excessive SignalR traffic while keeping gameplay meaningful.

#### Runner State Updates (periodic)

- **Online/Offline** — whether a Runner is currently connected; shown with a strikethrough or distinct icon on the map
- **Logged-in state** — only logged-in Runners are shown on the map

### 8.11 Map & Geolocation

- **OpenLayers.Blazor** handles map rendering and interaction
- **OpenStreetMap** provides map tiles (free, no API key required)
- Districts defined as **GeoJSON polygons** stored in the database (converted from KML on import)
- Trigger circles defined as **Points** in the KML file, matched to their parent district by point-in-polygon test
- Browser **Geolocation API** (`watchPosition`) used for Runner location tracking
- District claiming triggered when a Runner's GPS position falls within `DistrictCaptureRadiusMeters`

### 8.12 NuGet Package Dependencies

| Package | Purpose | License |
|---|---|---|
| `Mediator.SourceGenerator` + `Mediator.Abstractions` | Source-generated, zero-reflection mediator/CQRS dispatching | MIT |
| `Fluxor.Blazor.Web` | Client-side state management | MIT |
| `MudBlazor` | UI component library — dialogs, charts, badges, navigation, forms | MIT |
| `FluentValidation` | Input and game rule validation | Apache 2.0 |
| `NetTopologySuite` | Geospatial operations (point-in-polygon trigger detection) | BSD |
| `SharpKml` | KML/KMZ file parsing | BSD |
| `Microsoft.EntityFrameworkCore.Sqlite` | Data access and ORM | MIT |
| `Microsoft.AspNetCore.SignalR` | Real-time communication | MIT |
| `Serilog` | Structured logging | Apache 2.0 |

> All packages are free and open source. MediatR is explicitly avoided due to its commercial licensing model.

---

## 9. Data Models

The data model is split into two layers: a **static Template layer** (reusable blueprints) and a **dynamic Session layer** (live game state).

### 9.1 Template Layer — Static & Reusable

#### `GameTemplate`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `Name` | string | Display name (e.g. "Zutphen Edition") |
| `TotalRounds` | int | Number of rounds (default: 4) |
| `LocationUpdateIntervalSeconds` | int | Interval for opponent location broadcasts |
| `MinLocationUpdateIntervalSeconds` | int | Minimum client GPS update interval (default: 5) |
| `VotingDurationSeconds` | int | Duration of voting window per round |
| `PredictionBonusPoints` | int | Total bonus distributed to correct predictors |
| `VoteTimeoutPenalty` | int | Penalty applied to teams that miss the vote |
| `DistrictCaptureRadiusMeters` | double | Default radius of trigger circles |

#### `TeamTemplate`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameTemplateId` | int | FK to GameTemplate |
| `Name` | string | Team name (e.g. "Delta") — unique, NATO phonetic alphabet |
| `Color` | string | Hex color code for signature color |

#### `PlayerTemplate`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `TeamTemplateId` | int | FK to TeamTemplate |
| `LoginToken` | string | Unique token used in `/login/{token}` |
| `Role` | enum | `Runner`, `TeamLeader`, `GameMaster` |

#### `DistrictTemplate`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameTemplateId` | int | FK to GameTemplate |
| `Name` | string | District display name |
| `GeoJson` | string | GeoJSON polygon defining district boundaries |
| `TriggerLat` | double | Latitude of trigger circle center (from KML Point) |
| `TriggerLng` | double | Longitude of trigger circle center (from KML Point) |
| `TriggerRadiusMeters` | double? | Per-district radius override — falls back to GameTemplate default if null |
| `Gold` | int | Gold resource value |
| `Voters` | int | Voters resource value |
| `Likes` | int | Likes resource value |
| `Oil` | int | Oil resource value |

#### `RoundTemplate`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameTemplateId` | int | FK to GameTemplate |
| `RoundNumber` | int | Round sequence number (1–4) |
| `RoiResource` | enum | `Gold`, `Voters`, `Likes`, `Oil` |
| `Stake` | string | Description of voting round perk/punishment |

### 9.2 Session Layer — Dynamic & Ephemeral

#### `GameSession`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameTemplateId` | int | FK to GameTemplate |
| `Status` | enum | `Pending`, `Running`, `Finished` |
| `StartedAt` | datetime? | Timestamp when GM started the game |
| `FinishedAt` | datetime? | Timestamp when game concluded |
| `CurrentRoundSessionId` | int? | FK to active RoundSession |

#### `TeamSession`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameSessionId` | int | FK to GameSession |
| `TeamTemplateId` | int | FK to TeamTemplate |
| `TotalScore` | int | Running cumulative score |
| `TotalGold` | int | Running gold total |
| `TotalVoters` | int | Running voters total |
| `TotalLikes` | int | Running likes total |
| `TotalOil` | int | Running oil total |

#### `PlayerSession`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameSessionId` | int | FK to GameSession |
| `PlayerTemplateId` | int | FK to PlayerTemplate |
| `IsLoggedIn` | bool | Whether player is currently authenticated |
| `IsOnline` | bool | Whether player's SignalR connection is active |
| `LastSeen` | datetime? | Timestamp of last SignalR/heartbeat activity |
| `LocationLat` | double? | Last known latitude (Runners only) |
| `LocationLng` | double? | Last known longitude (Runners only) |
| `LocationUpdatedAt` | datetime? | Timestamp of last location update |

#### `DistrictSession`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameSessionId` | int | FK to GameSession |
| `DistrictTemplateId` | int | FK to DistrictTemplate |
| `CurrentOwnerTeamSessionId` | int? | FK to TeamSession currently owning this district |
| `IsClaimedThisRound` | bool | Whether trigger circle is currently inactive |
| `LastClaimedAt` | datetime? | Timestamp of most recent claim (for rollback/dispute resolution) |

#### `RoundSession`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameSessionId` | int | FK to GameSession |
| `RoundTemplateId` | int | FK to RoundTemplate |
| `Status` | enum | `Gathering`, `Voting`, `Completed` |
| `VotingEnabled` | bool | Whether GM has enabled voting |
| `VotingStartedAt` | datetime? | When the voting countdown timer started |
| `WinnerTeamSessionId` | int? | FK to TeamSession that won the voting round |

#### `Vote`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `RoundSessionId` | int | FK to RoundSession |
| `VotingTeamSessionId` | int | FK to TeamSession that cast the vote |
| `TargetTeamSessionId` | int | FK to TeamSession that received the vote |
| `VoteValue` | int | Number of votes cast (can be 0) |
| `IsAutocast` | bool | Whether this vote was auto-cast due to timeout |
| `CastAt` | datetime | Timestamp of vote |

#### `GameEvent` (Write-Ahead Log)
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `GameSessionId` | int | FK to GameSession |
| `RoundSessionId` | int? | FK to RoundSession (if applicable) |
| `EventType` | string | e.g. `DistrictClaimed`, `VoteCast`, `ScoreAwarded` |
| `Payload` | string | JSON-serialized event data |
| `OccurredAt` | datetime | Timestamp of event |
| `ActorPlayerSessionId` | int? | FK to PlayerSession that triggered the event (nullable) |

#### `RoundSnapshot`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `RoundSessionId` | int | FK to RoundSession |
| `TeamSessionId` | int | FK to TeamSession |
| `Phase` | enum | `EndOfGathering`, `EndOfVoting` |
| `Score` | int | Team score at snapshot time |
| `Gold` | int | Gold at snapshot time |
| `Voters` | int | Voters at snapshot time |
| `Likes` | int | Likes at snapshot time |
| `Oil` | int | Oil at snapshot time |
| `SnapshotTaken` | datetime | Timestamp of snapshot |

#### `DistrictOwnershipSnapshot`
| Field | Type | Description |
|---|---|---|
| `Id` | int | Primary key |
| `RoundSessionId` | int | FK to RoundSession |
| `DistrictSessionId` | int | FK to DistrictSession |
| `OwnerTeamSessionId` | int? | FK to TeamSession owning district at snapshot time |
| `Phase` | enum | `EndOfGathering`, `EndOfVoting` |
| `SnapshotTaken` | datetime | Timestamp of snapshot |

### 9.3 Entity Relationship Summary

- A **GameTemplate** defines teams, players, districts, and rounds — reusable across many **GameSessions**
- A **GameSession** is one live playthrough — it owns all runtime state
- Each template entity has a corresponding session entity holding its live state
- **Votes**, **GameEvents**, **RoundSnapshots**, and **DistrictOwnershipSnapshots** all belong to the session layer
- Snapshots are taken **twice per round**: at end of gathering phase and end of voting phase
- **GameEvent** records form the WAL — the authoritative, replayable history of the game

---

## 10. Game Configuration & Setup

### 10.1 Admin Area

- Available at `/admin` on the same server/domain as the player app
- Blazor SSR — server-side rendered, desktop-optimized
- Protected by its own login (credentials configured in `appsettings.json`)
- No mobile responsiveness required

### 10.2 GameTemplate Management

The admin UI allows:
- Creating and naming a GameTemplate
- Configuring all `GameSettings` values (rounds, intervals, bonus points, penalties, capture radius)
- Defining teams: name, signature color
- Managing player tokens — auto-generated based on the `{prefix}{randomness}` pattern
- Configuring rounds: ROI resource per round, stake description per round
- Importing districts from a KML/KMZ file
- Assigning and randomizing resource values per district
- Creating a new GameSession from a template
- Managing the active GameSession (start, reset)
- Force-logging out Runners per team

### 10.3 District Import (KML/KMZ)

- Districts are defined in a **Google KML or KMZ file** (e.g. created in Google My Maps)
- The admin UI provides a **file upload** for KML/KMZ
- A KML file contains two types of placemarks:
  - **Polygon placemarks** → become DistrictTemplate entries; district name taken from placemark name, boundaries stored as GeoJSON
  - **Point placemarks** → become trigger circles; matched to their parent district via point-in-polygon test
- Trigger circle **radius** defaults to `DistrictCaptureRadiusMeters` from GameSettings, adjustable per district after import
- After import, a **map preview** is shown alongside the district list, rendering exactly as it would appear to end users
- Selecting a district in the list **highlights it on the map preview** and vice versa

### 10.4 Resource Value Assignment

- Resource values can be **set manually** per district after import
- The admin UI provides a **"Randomize Resources"** button:
  - Admin sets a **min and max value** as the randomization range
  - All districts receive randomly assigned values for each resource within the range
  - Values can still be manually adjusted per district after randomization

### 10.5 GameSession Lifecycle

- Once a GameTemplate is fully configured, the GM creates a **GameSession** from it
- Only one GameSession can be active at a time
- The GM can:
  - **Start** a pending session → all waiting players transition to the map
  - **Reset** a finished session → clears all session state, ready to replay from the same template

---

## 11. User Flows & Edge Cases

### 11.1 Simultaneous District Claims

- If two Runners reach a trigger circle at the same time, **first claim wins**
- The trigger circle is immediately locked upon first valid claim — subsequent claims are rejected
- The `LastClaimedAt` timestamp is recorded for dispute resolution

### 11.2 Runner Reconnection

- If a Runner disconnects and reconnects, their client receives a **full resync of current game session state**
- Static template data (map, districts, team definitions) is **not re-downloaded** — served from `/api/map` cache
- The Runner's **location is broadcast** to all relevant clients as soon as they reconnect
- While offline, the Runner **cannot claim districts** — their team simply misses out

### 11.3 Runner Goes Offline During Gathering Phase

- The game **continues normally** — no pause or GM intervention required
- The offline Runner's team cannot claim districts until they reconnect
- The Runner is shown as **offline** on the map for all clients

### 11.4 No Team Leader Logged In During Voting Phase

- The voting phase **proceeds as normal**
- That team's vote is **auto-cast** when the timer expires, and the timeout penalty is applied

### 11.5 Voting With 0 Vote Points

- A Team Leader **is allowed to cast a vote** even if their team has 0 Voters
- The vote registers as an expression of intent but **adds nothing** to the target team's vote score

### 11.6 Vote Timeout

- If a team does not vote before `VotingDurationSeconds` expires:
  - A **random team is selected** as their vote target
  - The randomly selected team **cannot be the voting team itself** — if only two teams exist, the other team is automatically chosen
  - The `VoteTimeoutPenalty` is deducted from their score
  - The auto-cast vote is marked with `IsAutocast = true` in the Vote record

### 11.7 Runner Login Conflict

- A second Runner login attempt for the same team is **blocked with a clear error message**
- The slot is only freed by the Runner navigating to `/logout` or the GM force-logging out the Runner

### 11.8 End of Final Round

- After the final round concludes, `GameSession.Status` is set to `Finished`
- The **GM sees a final scores/results screen** with full breakdown
- All other players see a **"Game Ended — Thanks for Playing"** screen with no scores visible

### 11.9 GM Force-Logout of Runner

- The button on the team card is only **enabled** when a Runner is currently logged in
- On click, the Runner's session is terminated server-side
- If the Runner's client is online, a `RunnerLoggedOut` SignalR event immediately redirects them to the login page

### 11.10 Session Expiry (Tab Closure)

- Since tab closure cannot be reliably detected server-side, sessions are managed via:
  - Short-lived cookies renewed while active
  - Periodic heartbeat pings from client
  - Server-side expiration of sessions that miss heartbeat thresholds
- Expired sessions are treated the same as explicit logouts

---

## 12. Non-Functional Requirements

### 12.1 Performance

- Map and district overlays must render smoothly on **mid-range mobile devices**
- SignalR events must be reflected in the UI within **< 1 second** under normal network conditions
- Client-side GPS updates are throttled by `MinLocationUpdateIntervalSeconds` to reduce battery drain
- In-memory round state avoids excessive database I/O during active gameplay
- The app must remain responsive during peak simultaneous activity (e.g. all Runners claiming districts at once)

### 12.2 Scalability

- Designed for a **single concurrent game session**
- Expected player count: small to medium groups (~4 teams × 3–5 players = 12–20 concurrent users)
- Azure App Service single instance is sufficient for this load

### 12.3 Reliability & Resilience

- All game state is **server-side** — a client refresh or reconnect must fully restore current state
- SignalR disconnections handled gracefully with automatic reconnection
- Online/offline detection leverages **SignalR's built-in connection lifecycle events**
- The **WAL event log** ensures game state can always be reconstructed by replaying events

### 12.4 Real-Time Sync

- All game-critical events must be **broadcast to all connected clients immediately**
- Communication follows **SignalR first, HTTP fallback** for reliability
- Opponent location updates are **intentionally throttled** by `LocationUpdateIntervalSeconds`
- Own team and GM location updates are always **real-time**

### 12.5 Security

- Login tokens follow the `{prefix}{randomness}` pattern — non-guessable by design
- The `/logout` route must **not be discoverable** via the UI for Runners
- All game state changes are **validated server-side** through the Game Aggregate
- No sensitive personal data is stored — players are identified by token and role only

### 12.6 Offline & Background Behaviour

For full offline behaviour specification, see **Section 17 — Offline Behaviour**. In summary:
- The app requires an **active internet connection** for gameplay — full offline mode is not supported
- Static template data is **cached client-side** via `/api/map` and never re-downloaded on reconnect
- On reconnection, only **live session state** is resynced
- **Background geolocation** via `watchPosition` continues running during brief disconnections, and location is reflected locally on the Runner's map even while offline
- **Background geolocation** via `watchPosition` is attempted for Runners, with graceful degradation on iOS

### 12.7 Device & Browser Support

- Primary target: **mobile browsers** (Chrome on Android, Safari on iOS)
- Must be installable as a **PWA** on both Android and iOS home screens
- Layout must be fully **responsive and touch-friendly**

### 12.8 Accessibility & UX

- Sufficient **color contrast** for outdoor use in varying light conditions
- Team colors must be **visually distinguishable** from each other on the map
- **Sound effects** for key game events: district claimed, runner online/offline, vote cast, round/phase started
- **Light/Dark mode toggle** — activated by tapping the role icon top-right; persisted across sessions

---

## 13. Out of Scope

- No formal user account system — no registration, passwords, or email verification
- No OAuth or SSO integration
- No in-app KML/KMZ editor — districts are defined externally and imported
- No visual district boundary drawing within the app
- No support for multiple concurrent game sessions
- No in-app communication between players (no chat, no push notifications)
- No spectator mode — only registered players can view the game
- No replay UI for past sessions (WAL events are stored but no browse UI provided)
- No cross-session leaderboard
- No automatic round advancement — GM must always manually trigger phase and round changes
- No native iOS or Android app — PWA only
- No full offline mode
- No horizontal scaling — single Azure App Service instance only
- No external database — SQLite only
- No automated database backups
- No analytics or telemetry beyond Azure App Service built-in monitoring

---

## 14. Solution Structure

### 14.1 Solution Overview

**Solution:** `Konqvist.sln`

| Project | Type | Responsibility |
|---|---|---|
| `Konqvist.Client` | Blazor WASM | Player-facing PWA + GM interface — map, voting, scores, login, `/gm/*` routes |
| `Konqvist.Admin` | Blazor SSR | Admin UI — template setup, district import, session management |
| `Konqvist.Server` | ASP.NET Core | Game logic, SignalR hub, API endpoints, Game Aggregate, WAL |
| `Konqvist.Infrastructure` | Class Library | EF Core DbContext, entities, migrations, data access |

### 14.2 Key Design Decisions

- `Konqvist.Client` is a **single unified WASM app** serving both player and GM interfaces via role-based routing
- `Konqvist.Server` hosts the API and SignalR Hub and **serves** both the WASM and Admin apps
- `Konqvist.Infrastructure` is referenced by `Konqvist.Server` and `Konqvist.Admin` — never by `Konqvist.Client`
- Each feature in `Konqvist.Server` is a self-contained vertical slice
- `Konqvist.Client` uses **Fluxor** stores updated by incoming SignalR events
- The **Game Aggregate** is the single authoritative source of gameplay rule enforcement
- All state mutations produce **WAL events** before being applied

### 14.3 Folder Structure

```
Konqvist.sln
│
├── src/
│   │
│   ├── Konqvist.Client/                        # Blazor WASM — Player App + GM Interface
│   │   ├── Features/
│   │   │   ├── Map/
│   │   │   │   ├── MapPage.razor
│   │   │   │   ├── Store/                      # Fluxor store, actions, reducers
│   │   │   │   └── Components/
│   │   │   ├── Voting/
│   │   │   │   ├── VotingPage.razor
│   │   │   │   ├── Store/
│   │   │   │   └── Components/
│   │   │   ├── Scores/
│   │   │   │   ├── ScoresPage.razor
│   │   │   │   ├── Store/
│   │   │   │   └── Components/
│   │   │   ├── Login/
│   │   │   │   ├── LoginPage.razor
│   │   │   │   └── Store/
│   │   │   ├── Waiting/
│   │   │   │   └── WaitingPage.razor
│   │   │   └── GM/                             # GM-specific pages under /gm/*
│   │   │       ├── GMDashboard.razor
│   │   │       ├── GMScoresPage.razor
│   │   │       └── Components/
│   │   ├── Core/
│   │   │   ├── SignalR/                        # SignalR client + Fluxor action dispatching
│   │   │   ├── State/                          # Root Fluxor app state
│   │   │   ├── Auth/                           # Token/cookie-based auth handling
│   │   │   └── Geolocation/                    # watchPosition wrapper + throttle
│   │   ├── Shared/                             # Shared Razor components (layout, icons, sounds)
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   ├── sounds/                         # Game sound effects
│   │   │   └── manifest.json                   # PWA manifest
│   │   └── Program.cs
│   │
│   ├── Konqvist.Admin/                         # Blazor SSR — Admin App
│   │   ├── Features/
│   │   │   ├── GameTemplates/
│   │   │   ├── Districts/
│   │   │   ├── Teams/
│   │   │   ├── Rounds/
│   │   │   └── Session/
│   │   ├── Core/
│   │   │   └── Auth/
│   │   ├── Shared/
│   │   └── Program.cs
│   │
│   ├── Konqvist.Server/                        # ASP.NET Core — API + SignalR + Game Logic
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   ├── Game/
│   │   │   │   ├── StartGame.cs
│   │   │   │   ├── AdvanceRound.cs
│   │   │   │   └── EndGame.cs
│   │   │   ├── Districts/
│   │   │   │   ├── ClaimDistrict.cs
│   │   │   │   └── GetDistrictState.cs
│   │   │   ├── Voting/
│   │   │   │   ├── EnableVoting.cs
│   │   │   │   ├── CastVote.cs
│   │   │   │   ├── ResolveVote.cs
│   │   │   │   └── CloseVoting.cs
│   │   │   ├── Resources/
│   │   │   │   └── EditResources.cs
│   │   │   ├── Location/
│   │   │   │   └── UpdateLocation.cs
│   │   │   ├── Runners/
│   │   │   │   └── ForceLogoutRunner.cs
│   │   │   └── Snapshots/
│   │   │       └── TakeSnapshot.cs
│   │   ├── Domain/
│   │   │   ├── Aggregates/
│   │   │   │   └── GameAggregate.cs            # Central game aggregate
│   │   │   └── Events/                         # Domain event definitions (WAL)
│   │   ├── Hubs/
│   │   │   └── GameHub.cs                      # SignalR Hub
│   │   ├── Core/
│   │   │   ├── Validation/
│   │   │   ├── Geospatial/
│   │   │   └── Middleware/
│   │   └── Program.cs
│   │
│   └── Konqvist.Infrastructure/
│       ├── Persistence/
│       │   ├── KonqvistDbContext.cs
│       │   ├── Migrations/
│       │   └── Configurations/
│       ├── Entities/
│       │   ├── Template/
│       │   └── Session/
│       └── Repositories/
│
└── tests/
    ├── Konqvist.Server.Tests/
    └── Konqvist.Infrastructure.Tests/
```

---

## 15. Database Schema

### 15.1 EF Core Configuration Decisions

| Decision | Choice |
|---|---|
| ORM | Entity Framework Core |
| Database provider | SQLite |
| Entity mapping style | Fluent API — one `IEntityTypeConfiguration<T>` class per entity |
| Primary key type | `int` (auto-increment) |
| Nullable reference types | Enabled — optional fields use `?` notation |
| Enum storage | Stored as `string` for readability |
| Configuration discovery | `ApplyConfigurationsFromAssembly` — no manual registration |

### 15.2 Enumerations

| Enum | Values |
|---|---|
| `PlayerRole` | `Runner`, `TeamLeader`, `GameMaster` |
| `ResourceType` | `Gold`, `Voters`, `Likes`, `Oil` |
| `GameStatus` | `Pending`, `Running`, `Finished` |
| `GamePhase` | `WaitingForPlayers`, `Gathering`, `Voting`, `RoundResolution`, `Finished` |
| `RoundStatus` | `Gathering`, `Voting`, `Completed` |
| `SnapshotPhase` | `EndOfGathering`, `EndOfVoting` |

All enums are persisted as strings in the database.

### 15.3 Template Layer Entities

#### `GameTemplate`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `Name` | string(100) | Required |
| `TotalRounds` | int | Required, default: 4 |
| `LocationUpdateIntervalSeconds` | int | Required, default: 30 |
| `MinLocationUpdateIntervalSeconds` | int | Required, default: 5 |
| `VotingDurationSeconds` | int | Required, default: 30 |
| `PredictionBonusPoints` | int | Required, default: 150 |
| `VoteTimeoutPenalty` | int | Required |
| `DistrictCaptureRadiusMeters` | double | Required, default: 50 |

#### `TeamTemplate`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameTemplateId` | int | FK → GameTemplate (cascade delete) |
| `Name` | string(50) | Required |
| `Color` | string(7) | Required (e.g. `#C9A227`) |

Unique index on `(GameTemplateId, Name)`.

#### `PlayerTemplate`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `TeamTemplateId` | int | FK → TeamTemplate (cascade delete) |
| `LoginToken` | string(20) | Required, unique |
| `Role` | string | Required, stored as string enum |

Unique index on `LoginToken`.

#### `DistrictTemplate`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameTemplateId` | int | FK → GameTemplate (cascade delete) |
| `Name` | string(100) | Required |
| `GeoJson` | string | Required |
| `TriggerLat` | double | Required |
| `TriggerLng` | double | Required |
| `TriggerRadiusMeters` | double? | Nullable — falls back to GameTemplate default if null |
| `Gold` | int | Required |
| `Voters` | int | Required |
| `Likes` | int | Required |
| `Oil` | int | Required |

#### `RoundTemplate`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameTemplateId` | int | FK → GameTemplate (cascade delete) |
| `RoundNumber` | int | Required |
| `RoiResource` | string | Required, stored as string enum |
| `Stake` | string(500) | Required |

Unique index on `(GameTemplateId, RoundNumber)`.

### 15.4 Session Layer Entities

#### `GameSession`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameTemplateId` | int | FK → GameTemplate (restrict delete) |
| `Status` | string | Required, default: `Pending` |
| `CurrentPhase` | string | Required, stored as `GamePhase` enum, default: `WaitingForPlayers` |
| `StartedAt` | datetime? | Nullable |
| `FinishedAt` | datetime? | Nullable |
| `CurrentRoundSessionId` | int? | FK → RoundSession (set null), nullable |

#### `TeamSession`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameSessionId` | int | FK → GameSession (cascade delete) |
| `TeamTemplateId` | int | FK → TeamTemplate (restrict delete) |
| `TotalScore` | int | Required, default: 0 |
| `TotalGold` | int | Required, default: 0 |
| `TotalVoters` | int | Required, default: 0 |
| `TotalLikes` | int | Required, default: 0 |
| `TotalOil` | int | Required, default: 0 |

#### `PlayerSession`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameSessionId` | int | FK → GameSession (cascade delete) |
| `PlayerTemplateId` | int | FK → PlayerTemplate (restrict delete) |
| `IsLoggedIn` | bool | Required, default: false |
| `IsOnline` | bool | Required, default: false |
| `LastSeen` | datetime? | Nullable |
| `LocationLat` | double? | Nullable (Runners only) |
| `LocationLng` | double? | Nullable (Runners only) |
| `LocationUpdatedAt` | datetime? | Nullable (Runners only) |

#### `DistrictSession`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameSessionId` | int | FK → GameSession (cascade delete) |
| `DistrictTemplateId` | int | FK → DistrictTemplate (restrict delete) |
| `CurrentOwnerTeamSessionId` | int? | FK → TeamSession (set null), nullable |
| `IsClaimedThisRound` | bool | Required, default: false |
| `LastClaimedAt` | datetime? | Nullable — timestamp of most recent claim |

#### `RoundSession`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameSessionId` | int | FK → GameSession (cascade delete) |
| `RoundTemplateId` | int | FK → RoundTemplate (restrict delete) |
| `Status` | string | Required, default: `Gathering` |
| `VotingEnabled` | bool | Required, default: false |
| `VotingStartedAt` | datetime? | Nullable — when voting timer started |
| `WinnerTeamSessionId` | int? | FK → TeamSession (set null), nullable |

#### `Vote`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `RoundSessionId` | int | FK → RoundSession (cascade delete) |
| `VotingTeamSessionId` | int | FK → TeamSession (restrict delete) |
| `TargetTeamSessionId` | int | FK → TeamSession (restrict delete) |
| `VoteValue` | int | Required |
| `IsAutocast` | bool | Required, default: false |
| `CastAt` | datetime | Required |

Unique index on `(RoundSessionId, VotingTeamSessionId)`.

#### `GameEvent` (Write-Ahead Log)
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `GameSessionId` | int | FK → GameSession (cascade delete) |
| `RoundSessionId` | int? | FK → RoundSession, nullable |
| `EventType` | string(100) | Required |
| `Payload` | string | Required, JSON-serialized event data |
| `OccurredAt` | datetime | Required |
| `ActorPlayerSessionId` | int? | FK → PlayerSession (set null), nullable |

#### `RoundSnapshot`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `RoundSessionId` | int | FK → RoundSession (cascade delete) |
| `TeamSessionId` | int | FK → TeamSession (cascade delete) |
| `Phase` | string | Required, stored as string enum |
| `Score` | int | Required |
| `Gold` | int | Required |
| `Voters` | int | Required |
| `Likes` | int | Required |
| `Oil` | int | Required |
| `SnapshotTaken` | datetime | Required |

Unique index on `(RoundSessionId, TeamSessionId, Phase)`.

#### `DistrictOwnershipSnapshot`
| Column | Type | Constraints |
|---|---|---|
| `Id` | int | PK, auto-increment |
| `RoundSessionId` | int | FK → RoundSession (cascade delete) |
| `DistrictSessionId` | int | FK → DistrictSession (cascade delete) |
| `OwnerTeamSessionId` | int? | FK → TeamSession (set null), nullable |
| `Phase` | string | Required, stored as string enum |
| `SnapshotTaken` | datetime | Required |

Unique index on `(RoundSessionId, DistrictSessionId, Phase)`.

### 15.5 Delete Behaviour Summary

| Relationship | On Delete |
|---|---|
| GameTemplate → Teams/Rounds/Districts | Cascade |
| GameTemplate → GameSessions | Restrict |
| GameSession → Teams/Rounds/Districts/Players/Events | Cascade |
| TeamSession → OwnedDistricts | Set Null |
| TeamSession → WonRounds | Set Null |
| RoundSession → Votes/Snapshots/OwnershipSnapshots | Cascade |
| DistrictSession → OwnershipSnapshots | Cascade |
| Vote → VotingTeam / TargetTeam | Restrict |
| DistrictOwnershipSnapshot → OwnerTeam | Set Null |
| GameEvent → ActorPlayerSession | Set Null |

---

---

## 16. UI & Navigation

### 16.1 Pages & Views Overview

| Page / View | Route | Accessible By | Description |
|---|---|---|---|
| Login | `/team/{name}` or `/login/{token}` | All (unauthenticated) | Team URL entry point; role selection buttons |
| Waiting Room | `/waiting` | All (authenticated) | Shown before GM starts the game |
| Map (Gathering) | `/map` | Runner, GM | District map with trigger circles; active during Gathering phase |
| Voting | `/vote` | Runner (spectator), Team Leader, GM | Live vote bar chart and countdown during Voting phase |
| Scores | `/scores` | Team Leader, GM | Running team scores and resource totals |
| GM Dashboard | `/gm` | GM only | Phase controls, round management, voting enable/close |
| GM Scores & Management | `/gm/scores` | GM only | Full scores view + Runner force-logout controls |
| Finished | `/finished` | All | End-of-game screen (GM sees results, others see thank-you) |
| Admin — Templates | `/admin/templates` | Admin | GameTemplate CRUD |
| Admin — Districts | `/admin/districts` | Admin | KML import, district list + map preview |
| Admin — Session | `/admin/session` | Admin | Create/start/reset GameSession |

> **GM unrestricted navigation:** GMs can access Map, Voting, and Scores at any time regardless of the current game phase. Their navigation is never blocked by phase transitions.

### 16.2 Role-Based Routing

Navigation is gated at the Blazor router level using role-based route guards:

- **Runners** are automatically redirected to the correct phase view when a `GamePhaseChanged` event is received — they cannot freely browse between pages
- **Team Leaders** are similarly phase-locked, but can freely move between Voting and Scores during their permitted phases
- **GMs** have unrestricted navigation at all times
- Unauthorized route access redirects to the login page

### 16.3 Phase-Driven Navigation

All major view transitions are **event-driven**, not user-initiated (except for the GM). When the server emits a `GamePhaseChanged` SignalR event, all connected clients are automatically navigated to the appropriate view:

| Phase | Runner View | Team Leader View | GM View |
|---|---|---|---|
| `WaitingForPlayers` | Waiting Room | Waiting Room | GM Dashboard + Waiting |
| `Gathering` | Map | Map (read-only) | Map (full access) |
| `Voting` | Voting (spectator) | Voting (can vote) | Voting + controls |
| `RoundResolution` | Voting (results) | Voting (results) | Voting (results) |
| `Finished` | Finished (thank-you) | Finished (thank-you) | Finished (full scores) |

### 16.4 Mobile Navigation — Swipe Gestures

On mobile devices, swipe gestures provide a natural navigation mechanism between the views a player has access to. This reduces reliance on tap-based navigation and feels natural during cycling.

**Runner swipe navigation (during Gathering phase):**
- Swipe left/right between: **Map** ↔ (no other views during gathering)

**Team Leader swipe navigation:**
- Swipe left/right between: **Voting** ↔ **Scores** (during Voting phase)
- Swipe left/right between: **Map** ↔ **Scores** (during Gathering phase)

**GM swipe navigation:**
- Swipe left/right freely between: **Map** ↔ **Voting** ↔ **Scores / Management**

Swipe detection is implemented via touch event listeners in JavaScript interop, dispatching Fluxor navigation actions. The current active tab is visually indicated by a bottom tab bar or page indicator dots.

### 16.5 Navigation Layout & Structure

**Player App layout:**
- **Top bar:** Team name, role icon (tapping role icon toggles light/dark mode), connection status indicator
- **Content area:** Full-screen view for the current phase (map fills entire screen)
- **Bottom tab bar:** Visible tabs depend on role and current phase; swipe-compatible
- **Offline banner:** Shown below the top bar when SignalR connection is lost (see Section 17)

**GM Interface layout:**
- Same shell as the player app, with additional GM-specific controls overlaid on relevant views
- Phase advancement buttons shown as floating action buttons or a persistent GM control panel
- Force-logout buttons available on each team card in the Scores view

**Admin App layout:**
- Standard desktop sidebar navigation (Blazor SSR)
- Left sidebar with links: Templates, Districts, Teams, Rounds, Session
- Main content area renders the selected admin view

### 16.6 Key Reusable UI Components

The following shared components are defined once and reused across the app:

| Component | Usage |
|---|---|
| `TeamColorBadge` | Team name + colored dot; used on maps, scores, voting bars |
| `ResourceRow` | Horizontal row of Gold/Voters/Likes/Oil values with icons |
| `CountdownTimer` | Circular or linear countdown display; used on Voting screen |
| `OnlineIndicator` | Green/red dot indicating Runner connection status |
| `OfflineBanner` | Full-width banner shown when client is disconnected |
| `DistrictPopup` | Tappable info popup on the map |
| `VoteBar` | Single team's vote bar in the voting chart |
| `TeamScoreCard` | Summary card showing a team's score and resources |
| `RoundBadge` | Current round number indicator |
| `StakeDisplay` | Shows the round's perk/punishment text at the top of the voting screen |
| `PhaseLabel` | Current game phase indicator in the top bar |

---

## 17. Offline Behaviour

### 17.1 Philosophy

KONQVIST requires an active internet connection for gameplay. However, when a Runner's connection drops temporarily — which is realistic during a cycling event due to mobile network coverage gaps — the app should remain as functional as possible rather than becoming completely unusable. The game continues on the server; the Runner's client must handle its disconnected state gracefully and resync cleanly on reconnect.

### 17.2 What Continues Working Offline

| Feature | Offline Behaviour |
|---|---|
| Map display | Fully functional — cached map tiles remain visible |
| District overlays | Last known ownership state displayed (may be stale) |
| GPS tracking | `watchPosition` continues running — Runner's position updates locally on the map in real-time |
| Trigger zone detection | Client still detects entry into trigger zones |
| Team Leader scores view | Last known state displayed (stale data) |
| Voting screen | Last known vote state displayed; vote buttons are disabled |

### 17.3 What Does Not Work Offline

| Feature | Offline Behaviour |
|---|---|
| **District claiming** | Disabled — claim commands cannot reach the server |
| Casting votes | Disabled — no server connection |
| Real-time opponent tracking | Frozen at last known positions |
| Score updates | Not received until reconnect |
| Phase transitions | Not received; UI stays on current phase until reconnect + resync |
| GM phase controls | Disabled — all GM commands require server connection |

### 17.4 Offline Visual Communication

When a Runner's SignalR connection drops, the UI communicates the disconnected state clearly:

- **Offline banner:** A prominent full-width banner appears below the top bar with a message such as "You're offline — reconnecting…". It is styled in a warning color (amber/red) and is hard to miss
- **Map dimming:** The map overlay receives a semi-transparent dark tint, visually indicating that the map data may be stale and that interaction is limited
- **Claim button disabled:** Trigger circle tap targets are visually disabled (grayed out, no interaction feedback)
- **Reconnecting indicator:** A subtle animated spinner or pulsing icon in the top bar supplements the banner
- **On reconnect:** The banner and dimming disappear immediately, and a full state resync is performed before re-enabling claim interaction

On **other clients' screens** (Team Leader, GM), a Runner going offline is shown via:
- The Runner's map marker becoming faded or marked with an offline icon
- A `RunnerStateChanged` event updates the `OnlineIndicator` component on all applicable views

### 17.5 Reconnection & Resync Flow

On SignalR reconnection:

1. The `ReconnectModal` (`.NET 10` built-in, customized for game UI) shows a reconnecting state
2. On successful reconnect, the client requests a **full session state resync** from the server
3. Stale local state is replaced with the server's authoritative state (current phase, district ownership, scores, voting state)
4. Static data (map, districts, team definitions) is **never re-requested** — served from client cache via the initial `/api/map` call
5. The offline banner and map dim are removed
6. Claim commands that were queued during the offline period are **re-sent** using the idempotent retry mechanism (see Section 5.3) — the server handles duplicates gracefully

### 17.6 Heartbeat & Session Expiry During Offline

- The client sends periodic heartbeat pings while connected
- If the connection drops, heartbeats stop
- The server marks the player as **offline** after missing heartbeats (via `IsOnline = false` in `PlayerSession`)
- If the player remains offline beyond the session cookie lifetime, their session expires and they are returned to the login page on reconnect
- Short disconnections (seconds to a few minutes) recover transparently with full resync

*End of Document*
