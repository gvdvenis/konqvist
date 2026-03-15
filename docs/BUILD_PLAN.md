# KONQVIST — Build Plan
> Version 1.0 | March 2026  
> Companion document to KONQVIST PRD v1.5

---

## How to Use This Document

This plan breaks the full KONQVIST build into **9 sequential milestones**. Each milestone:

- Ends with a concrete, runnable, verifiable state
- Builds directly on the previous milestone — never skips ahead
- Is divided into **ordered slices** that should be implemented one at a time
- Has a **"Done means"** checklist you can verify before starting the next milestone

When working with an AI agent, start each session by providing:
1. The full PRD (`KONQVIST_PRD.md`) as reference context
2. This build plan to establish where you are
3. The specific slice you are working on

The agent should implement **one slice at a time**. Do not ask it to implement an entire milestone in one go.

---

## Milestone Overview

| # | Name | Deliverable | Depends On |
|---|---|---|---|
| M1 | Foundation | Runnable solution, DB schema, admin login | — |
| M2 | Template Management | Full game template setup via admin UI | M1 |
| M3 | Authentication & Routing | Token login, cookie sessions, role-locked routes | M2 |
| M4 | Real-Time Core | Game Aggregate, SignalR hub, Fluxor wiring, phase model | M3 |
| M5 | Gathering Phase | Map, GPS, trigger detection, district claiming | M4 |
| M6 | Voting Phase | Timer, votes, prediction bonus, round resolution | M5 |
| M7 | GM Controls & Scores | Scores page, GM dashboard, resource editing, force-logout | M6 |
| M8 | Offline & PWA | Offline banner, resync, service worker, PWA install | M7 |
| M9 | Polish & Deploy | Sound effects, AOT build, Azure deployment | M8 |

---

## M1 — Foundation

**Goal:** A running, deployable .NET solution with the correct project structure, database schema, and admin authentication. No game logic yet — just the skeleton everything else will be built on.

**PRD references:** §8.2, §14, §15, §8.4

### Slices

**1.1 — Solution scaffold**
- Create `Konqvist.sln` with four projects: `Konqvist.Client` (Blazor WASM), `Konqvist.Admin` (Blazor SSR), `Konqvist.Server` (ASP.NET Core), `Konqvist.Infrastructure` (class library)
- Configure project references: Server → Infrastructure, Admin → Infrastructure, Client has no Infrastructure reference
- Add NuGet packages to each project as specified in PRD §8.12: `Mediator.SourceGenerator`, `Fluxor.Blazor.Web`, `MudBlazor`, `FluentValidation`, `NetTopologySuite`, `SharpKml`, `EF Core Sqlite`, `SignalR`, `Serilog`
- Configure Serilog in `Program.cs` on Server with console + file sinks

**1.2 — Infrastructure: entities and DbContext**
- Implement all entity classes under `Konqvist.Infrastructure/Entities/` matching PRD §15 exactly:
  - Template layer: `GameTemplate`, `TeamTemplate`, `PlayerTemplate`, `DistrictTemplate`, `RoundTemplate`
  - Session layer: `GameSession`, `TeamSession`, `PlayerSession`, `DistrictSession`, `RoundSession`, `Vote`, `GameEvent`, `RoundSnapshot`, `DistrictOwnershipSnapshot`
- Implement all enums as strings: `PlayerRole`, `ResourceType`, `GameStatus`, `GamePhase`, `RoundStatus`, `SnapshotPhase`
- Implement `KonqvistDbContext` with `ApplyConfigurationsFromAssembly`
- Implement one `IEntityTypeConfiguration<T>` per entity with Fluent API — all constraints, indexes, and FK delete behaviours per PRD §15.5

**1.3 — First migration and seed**
- Create initial EF Core migration
- Add seed data: one `GameTemplate` named "Demo Game" with 2 teams (Alpha, Bravo), 2 `PlayerTemplate` records per team (1 Runner + 1 Team Leader each), 1 GM `PlayerTemplate`, 2 `RoundTemplate` records
- Verify database creates and seeds on first run

**1.4 — Admin app: login**
- Implement admin login page at `/admin/login` using cookie authentication
- Credentials read from `appsettings.json` (`Admin:Username`, `Admin:Password`)
- Redirect to `/admin` on success; redirect unauthenticated requests to `/admin/login`
- Implement MudBlazor-based admin layout: left sidebar navigation, main content area

**1.5 — Admin app: shell navigation**
- Implement sidebar links: Templates, Districts, Teams, Rounds, Session (all route to placeholder pages for now)
- Each placeholder page renders its own name so routing is verifiable

### Done means
- [x] `dotnet build` succeeds with no warnings on all four projects
- [x] `dotnet run` on `Konqvist.Server` starts without errors
- [x] SQLite database is created on first run with all tables and correct schema
- [x] Navigating to `/admin/login` shows login form
- [x] Logging in with credentials from `appsettings.json` redirects to `/admin` with sidebar visible
- [x] All five sidebar links navigate to their respective placeholder pages
- [x] Unauthenticated access to `/admin` redirects to `/admin/login`

---

## M2 — Template Management

**Goal:** A fully functional admin UI for creating and configuring game templates, including KML/KMZ district import with map preview. After this milestone, a complete game template can be set up from scratch without touching the database.

**PRD references:** §10, §5.7, §9.1

### Slices

**2.1 — GameTemplate CRUD**
- Admin page at `/admin/templates`: list all templates, create new, delete
- Create form: Name, LocationUpdateIntervalSeconds (default 30), MinLocationUpdateIntervalSeconds (default 5), VotingDurationSeconds (default 30), PredictionBonusPoints (default 150), VoteTimeoutPenalty, DistrictCaptureRadiusMeters (default 50)
- `TotalRounds` is derived from configured `RoundTemplates` (new games start with 4 auto-generated rounds)
- All settings stored in `GameTemplate` DB entity — no `appsettings.json` game config

**2.2 — Team management**
- Sub-page at `/admin/templates/{id}/teams`: list teams, add/edit/delete
- Fields: Name (NATO phonetic alphabet), Color (hex color picker)
- Unique name constraint enforced in UI and validated server-side

**2.3 — Player token management**
- Within each team, display Runner and Team Leader token entries
- Auto-generate tokens on team creation using `{TeamInitial}R{4-random}` and `{TeamInitial}TC{4-random}` patterns
- Show GM token (`GM{4-random}`) at template level
- Display tokens as copyable text; provide a "Regenerate" button per token

**2.4 — Round configuration**
- Sub-page at `/admin/templates/{id}/rounds`: list rounds, add/remove rounds, edit each
- `TotalRounds` is derived from the number of configured rounds
- Per-round fields: RoundNumber (read-only), RoiResource (dropdown: Gold/Voters/Likes/Oil), Stake (text input up to 500 chars)

**2.5 — KML/KMZ district import**
- Sub-page at `/admin/templates/{id}/districts`
- File upload component (`.kml` or `.kmz`)
- Server-side: parse with SharpKml; extract Polygon placemarks → `DistrictTemplate`; extract Point placemarks → trigger circles; match points to parent district via NetTopologySuite point-in-polygon
- Store districts with Name, GeoJSON polygon, TriggerLat/Lng, TriggerRadiusMeters (default from template), resource values all defaulting to 0
- Display import result summary: N districts imported, N trigger circles matched

**2.6 — District list and map preview**
- After import, render district list (Name, resource totals, trigger radius) alongside an OpenLayers map preview
- Map preview shows district polygons filled with a neutral color and trigger circles as dashed rings
- Selecting a district in the list highlights it on the map (solid fill); clicking a district on the map highlights it in the list

**2.7 — Resource value assignment**
- Per-district editing: click a district to open inline edit for Gold, Voters, Likes, Oil, TriggerRadiusMeters
- "Randomize Resources" button: admin sets min/max range → all district resources randomized within range
- Manual per-district overrides always possible after randomization

**2.8 — GameSession management**
- Admin page at `/admin/session`
- Create a new `GameSession` from a selected template (only if no session is `Pending` or `Running`)
- Show current session status (Pending / Running / Finished)
- "Start" button (only if Pending): sets status to Running, records `StartedAt`
- "Reset" button (only if Finished): deletes all session-layer records, session returns to Pending state

### Done means
- [x] A complete GameTemplate can be created with name, all settings, teams, players, and rounds via the admin UI
- [x] All generated tokens are visible and copyable
- [x] A KML/KMZ file with polygons and points imports successfully; districts appear in the list and on the map preview
- [x] Selecting a district in the list highlights it on the map and vice versa
- [x] Resource randomization fills all districts within the specified range
- [x] A GameSession can be created from a template, started, and reset via the admin session page
- [x] All validation errors (duplicate team name, missing stake, etc.) surface as UI messages

---

## M3 — Authentication & Routing

**Goal:** Players can log in via team URL or token, receive a session cookie, and are routed to the correct view based on their role. The Blazor WASM app shell is in place with role-aware navigation guards.

**PRD references:** §3, §16.1, §16.2

### Slices

**3.1 — Login API endpoint**
- `POST /api/auth/login` — accepts `{ token: string }`
- Validates token against `PlayerTemplate.LoginToken`
- Enforces Runner uniqueness: if `Role == Runner` and another `PlayerSession` for the same team already has `IsLoggedIn = true`, return 409 Conflict with message
- On success: creates or updates `PlayerSession` (`IsLoggedIn = true`), issues a cookie (`HttpOnly`, `SameSite=Strict`)
- `POST /api/auth/logout` — clears cookie, sets `IsLoggedIn = false`

**3.2 — Team login page**
- Blazor WASM page at `/team/{name}` (and `/login/{token}` for direct token entry)
- `/team/{name}`: shows team name, two MudBlazor buttons — "Runner" and "Team Captain"
- Runner button disabled with tooltip if Runner slot is taken
- On button click: calls login API with the appropriate token for that team and role
- On success: navigates to `/waiting`

**3.3 — Blazor auth state and route guards**
- Implement `AuthenticationStateProvider` reading the session cookie / auth state from a `GET /api/auth/me` endpoint
- `[Authorize]` route attribute on all protected pages
- Role-based `RouteView` guards: Runner cannot access `/scores`; Team Leader cannot access `/gm/*`; unauthenticated access redirects to `/team/{name}` or `/login`

**3.4 — Client app shell**
- Main layout: top bar (team name, role icon, connection indicator), content area (full-screen), bottom tab bar
- Tapping role icon in top bar toggles light/dark mode (MudBlazor theme toggle); preference stored in `localStorage`
- `OfflineBanner` component wired to SignalR connection state (show/hide only — no reconnect logic yet)
- Bottom tab bar renders tabs based on role (Runner: Map only; Team Leader: Map + Scores; GM: Map + Voting + Scores)

**3.5 — Waiting room page**
- Page at `/waiting` shown after login until GM starts the game
- Displays team name, role, list of currently online players (placeholder — populated in M4)
- "Waiting for Game Master to start the game…" message with animated indicator

### Done means
- [x] `/team/alpha` shows correct team name with Runner and Team Captain buttons
- [x] Runner button is disabled when another Runner is already logged in for that team
- [x] Successful login navigates to `/waiting` and sets the session cookie
- [x] Refreshing the page keeps the session (cookie-based auth persists)
- [x] Navigating to a role-restricted page without correct role redirects to login
- [x] Top bar shows team name and role icon; dark/light mode toggle works
- [x] Bottom tab bar shows correct tabs per role

---

## M4 — Real-Time Core

**Goal:** The Game Aggregate exists on the server, the SignalR hub is wired, Fluxor stores are connected to SignalR events, and the GamePhase model drives client navigation. No gameplay yet — but the plumbing is complete and testable.

**PRD references:** §8.5, §8.6, §8.7, §8.10, §4.4

### Slices

**4.1 — Game Aggregate skeleton**
- Implement `GameAggregate` class in `Konqvist.Server/Domain/Aggregates/`
- Holds in-memory state: current `GamePhase`, current round, team scores, district ownership
- Exposes methods for each command: `ClaimDistrict`, `CastVote`, `OpenVoting`, `CloseVoting`, `AdvanceRound`, `ForceLogoutRunner`
- Each method validates the command, produces a `GameEvent`, appends it to the WAL (persisted events only per PRD §8.7), updates in-memory state, and returns the event
- Register as a singleton in DI

**4.2 — WAL persistence**
- Implement `GameEventRepository` that writes `GameEvent` entities to the database
- Only the 8 persisted event types are written (DistrictClaimed, VoteCast, VotingOpened, VotingClosed, RoundAdvanced, RunnerLogin, RunnerLogout, GamePhaseChanged)
- All other events (location updates, heartbeats) remain in-memory and are never written

**4.3 — SignalR hub**
- Implement `GameHub : Hub` at `/hubs/game`
- On connect: authenticate via cookie, join player to their team group and a global game group
- On disconnect: update `PlayerSession.IsOnline = false`, broadcast `RunnerStateChanged` if applicable
- Hub methods corresponding to client→server commands: `ClaimDistrict(districtId)`, `CastVote(targetTeamId)`, `UpdateLocation(lat, lng)` — all delegate to the Game Aggregate
- Server→client typed interface: `IGameClient` with methods for each event type in PRD §8.10

**4.4 — Fluxor stores (client)**
- Implement Fluxor feature stores in `Konqvist.Client/`:
  - `GameStore`: current `GamePhase`, round number, game session id
  - `MapStore`: district ownership dictionary, runner positions
  - `VotingStore`: current votes per team, voting enabled flag, timer remaining
  - `ScoresStore`: team scores and resource totals
  - `PlayerStore`: own player identity, team, role, online/offline state
- Each store has corresponding Actions and Reducers
- No data in stores yet — wiring only

**4.5 — SignalR client service**
- Implement `GameHubService` in `Konqvist.Client/Core/SignalR/`
- Connects to `/hubs/game` on app startup (after login)
- On each incoming SignalR event: dispatches the corresponding Fluxor action
- Handles reconnection: on `Reconnected`, calls `GET /api/session/state` and dispatches a full-resync action to all stores
- Exposes `ConnectionState` observable for `OfflineBanner` to react to

**4.6 — Phase-driven navigation**
- In `GameHubService`, handle `GamePhaseChanged` event: dispatch `NavigateToPhaseAction`
- Implement a `PhaseNavigator` effect in Fluxor that maps `GamePhase` → route and calls `NavigationManager.NavigateTo()`
- Test: when server transitions to `Gathering`, all connected Runners navigate to `/map`; Team Leaders navigate to `/map` (read-only); GMs stay on their current page

**4.7 — GM: start game command**
- GM Dashboard page at `/gm`
- "Start Game" button: calls `POST /api/game/start`
- Server handler: validates session is Pending, transitions `GameSession.Status` to Running, sets `CurrentPhase` to `Gathering`, emits `GamePhaseChanged` via SignalR to all clients
- Waiting room page navigates away on receiving `GamePhaseChanged`

### Done means
- [x] `GameAggregate` singleton is registered and holds in-memory state
- [x] WAL writes to the database only for the 8 persisted event types — verified by inspecting the `GameEvents` table
- [x] Two browser tabs logged in as different players both receive a SignalR event when game phase changes
- [x] Clicking "Start Game" in the GM dashboard navigates Runner clients from `/waiting` to `/map`
- [x] `GameHubService` reconnection handler calls the state resync endpoint on reconnect
- [x] `OfflineBanner` appears when SignalR connection is manually severed (e.g. DevTools offline mode)
- [x] Fluxor Redux DevTools (if using the Fluxor dev extension) show actions being dispatched on phase change

---

## M5 — Gathering Phase

**Goal:** Runners can cycle, see the map with districts, and claim districts by entering trigger zones. District ownership updates in real-time on all connected clients. The claim retry mechanism handles unstable connections.

**PRD references:** §5, §8.11, §11.1, §11.2, §11.3, §5.3

### Slices

**5.1 — Map page and district rendering**
- Page at `/map` with OpenLayers map (OpenStreetMap tiles)
- On page load: call `GET /api/map` and populate `MapStore` with districts + game config
- Render each `DistrictOverlay` component: GeoJSON polygon filled with team signature color (semi-transparent) if owned, neutral if unclaimed
- Render trigger circles as dashed rings for unclaimed districts
- Implement `DistrictPopup` component: tap district → popup shows name, resources, current owner

**5.2 — Runner toolbar**
- `MapToolbar` component with four buttons (PRD §5.5): Tracking mode, Center on location, Full map view, Toggle other teams
- Tracking mode: rotates/tilts map to heading direction using device orientation API
- Center: calls OpenLayers `view.setCenter(currentPosition)`
- Full map view: calls `view.fit(extent)` for the play area bounding box
- Toggle other teams: show/hide `RunnerMarker` components for other teams

**5.3 — GPS tracking and Runner position**
- Implement `GeolocationService` in `Konqvist.Client/Core/Geolocation/`
- Wraps browser `navigator.geolocation.watchPosition`
- Throttles emissions to `MinLocationUpdateIntervalSeconds`
- On each position: dispatch `LocationUpdatedAction` to `MapStore`, call `UpdateLocation(lat, lng)` on SignalR hub
- Render own Runner's position as a distinct marker (different color/icon from other teams)
- Server: `UpdateLocation` handler receives position, updates `PlayerSession.LocationLat/Lng/UpdatedAt`, broadcasts `LocationUpdated` to own team (real-time) and opposing teams (throttled by `LocationUpdateIntervalSeconds`)

**5.4 — Trigger zone detection and claim**
- In `GeolocationService`, after each position update: check distance against all active (unclaimed this round) trigger circles using Haversine formula
- On transition from outside → inside a trigger circle: call `ClaimDistrict(districtId)` on SignalR hub
- Client-side lock: mark district as "claim in flight" — do not re-trigger for same district while awaiting acknowledgement
- Implement retry: if no `DistrictClaimed` event received within 3s, re-send `ClaimDistrict`. Retry up to 5 times with 3s spacing, then surface an error snackbar

**5.5 — Server: ClaimDistrict command**
- `GameAggregate.ClaimDistrict(districtId, runnerPlayerSessionId)`:
  - Validate: phase is `Gathering`, district trigger is active, Runner is online
  - Idempotency check: if district is already owned by this team this round, return success without re-applying
  - Apply: set `DistrictSession.CurrentOwnerTeamSessionId`, `IsClaimedThisRound = true`, `LastClaimedAt = now`
  - Add claimed district resources to `TeamSession` running totals
  - Emit `DistrictClaimed` event (persisted), broadcast to all clients via SignalR
- Client on receiving `DistrictClaimed`: update `MapStore`, cancel any pending retry for that district

**5.6 — Runner markers for other teams**
- Render `RunnerMarker` components for all online Runners of all teams on the map
- Marker color matches team signature color
- Offline Runners shown with a faded/greyed marker and an `OnlineIndicator` badge showing offline state
- Markers update position on `LocationUpdated` event

**5.7 — GM: advance to voting**
- "Advance to Voting" button on GM Dashboard
- Server handler: validates phase is `Gathering`, takes `EndOfGathering` round snapshot for all teams + all districts, transitions phase to `Voting`, emits `GamePhaseChanged`
- All non-GM clients navigate to `/vote`

### Done means
- [ ] Map loads with all district polygons visible; tapping a district opens the popup with correct data
- [ ] Runner's GPS position is shown on the map as a moving marker
- [ ] Walking/cycling into a trigger circle claims the district: color changes to team color on all connected clients within 1 second
- [ ] Trigger circle disappears after claim; re-entering the same zone does not re-trigger
- [ ] Claim retry fires if SignalR is briefly interrupted (test by briefly toggling DevTools offline during a claim)
- [ ] Duplicate claim commands are silently discarded by the server (idempotency)
- [ ] Other team Runners are visible on the map; toggling "other teams" hides/shows them
- [ ] GM "Advance to Voting" navigates all Runner and Team Leader clients to the voting screen
- [ ] `EndOfGathering` snapshots are written to the database

---

## M6 — Voting Phase

**Goal:** The full voting flow works end to end — countdown timer, live bar chart, auto-cast on timeout, prediction bonus distribution, score updates, and round resolution.

**PRD references:** §7, §6.3, §6.4, §11.4, §11.5, §11.6, §4.2 step 7

### Slices

**6.1 — Voting page shell**
- Page at `/vote`
- `StakeDisplay` component at top: shows round's stake text and ROI resource (highlighted)
- `CountdownTimer` component: circular countdown, starts at `VotingDurationSeconds`, counts down visibly
- `VoteBar` components: one per team, colored in team signature color, width proportional to vote total
- Runners see "You are spectating — only Team Leaders can vote" message; vote buttons hidden
- Timer does not start until `VoteStarted` SignalR event is received

**6.2 — GM: enable voting**
- On GM's voting view: "Enable Voting" button
- Server: `GameAggregate.OpenVoting()` — sets `RoundSession.VotingEnabled = true`, `VotingStartedAt = now`, emits `VotingOpened` (persisted) + `VoteStarted` (broadcast)
- All clients: timer starts; Team Leader vote buttons become active on `VoteStarted`

**6.3 — Team Leader: cast vote**
- Vote buttons: one per opposing team (cannot vote for own team)
- On click: call `POST /api/vote` with target team id
- Server: `GameAggregate.CastVote(teamSessionId, targetTeamSessionId)`:
  - Validate: voting is enabled, team hasn't already voted, target is not own team
  - Record `Vote` with VoteValue = team's current `TotalVoters`
  - Emit `VoteCast` (persisted), broadcast to all clients
- After voting, all vote buttons disabled for that team; "Voted!" indicator shown
- Client: `VotingStore` updates → `VoteBar` widths animate to new totals

**6.4 — Voting timer expiry and auto-cast**
- Server: background timer service monitors `RoundSession.VotingStartedAt + VotingDurationSeconds`
- On expiry: for each team without a vote, select random target (not self), deduct `VoteTimeoutPenalty` from `TeamSession.TotalScore`, record `Vote` with `IsAutocast = true`
- Emit `VotingClosed` (persisted), then proceed to vote resolution (slice 6.5)
- GM can also close voting early via "End Voting" button — early close awards no penalties

**6.5 — Vote resolution and scoring**
- `GameAggregate.CloseVoting()`:
  1. Tally votes → determine `WinnerTeamSessionId` (highest vote total)
  2. Set `RoundSession.WinnerTeamSessionId`
  3. Calculate prediction bonuses: teams that voted for the winner share `PredictionBonusPoints / correctPredictorCount`; add to `TeamSession.TotalScore`
  4. Update all `TeamSession.TotalScore` values
  5. Emit `VoteEnded` and `ScoreUpdated` (broadcast)
  6. Take `EndOfVoting` round snapshot for all teams and districts
  7. Transition phase to `RoundResolution`, emit `GamePhaseChanged`

**6.6 — Round resolution view**
- On `RoundResolution` phase: voting page transitions to a results view
- Shows: winner name and team color, vote breakdown, prediction bonus recipients
- "Results" remain visible until GM advances to the next round or ends the game

**6.7 — GM: advance round or end game**
- "Next Round" button on GM controls: validates more rounds remain
  - Resets all `DistrictSession.IsClaimedThisRound = false` (trigger circles reactivate)
  - Increments to next `RoundSession`, sets phase to `Gathering`, emits `GamePhaseChanged`
- "End Game" button: only shown when current round is the final round
  - Sets `GameSession.Status = Finished`, `FinishedAt = now`, phase to `Finished`
  - GM clients navigate to `/finished` with full score breakdown
  - All other clients navigate to `/finished` with "Thanks for Playing" message

### Done means
- [ ] Voting page shows stake, ROI highlight, timer, and per-team vote bars
- [ ] Timer does not start until GM clicks "Enable Voting"
- [ ] Team Leader can cast one vote; buttons disable after voting
- [ ] Vote bars update in real-time on all connected clients as votes are cast
- [ ] Timer expiry auto-casts votes for non-voting teams with penalty deducted
- [ ] GM early close skips penalties
- [ ] Winner is correctly determined; prediction bonuses distribute correctly (verified against the example in PRD §6.4)
- [ ] `EndOfVoting` snapshots are written to the database
- [ ] "Next Round" reactivates all trigger circles; Runners navigate back to map
- [ ] "End Game" on final round shows correct screens for GM vs other players

---

## M7 — GM Controls & Scores

**Goal:** The Scores page is fully functional for Team Leaders and GMs. The GM can edit resources, force-logout Runners, and has a complete management view. The GM Dashboard provides all round controls in one place.

**PRD references:** §2.4, §6.5, §7.4, §16.1

### Slices

**7.1 — Scores page**
- Page at `/scores` (Team Leader and GM)
- One `TeamScoreCard` per team showing: team name + color badge, TotalScore, resource breakdowns (Gold/Voters/Likes/Oil), current online Runner status (`OnlineIndicator`)
- Sorted by TotalScore descending
- Updates in real-time on `ScoreUpdated` SignalR event

**7.2 — GM resource editing**
- On GM's Scores view: each resource value in each `TeamScoreCard` is tappable
- Tapping opens a MudBlazor dialog (`ResourceBonusDialog`): label shows team + resource name, numeric input (positive = bonus, negative = penalty)
- On confirm: `POST /api/gm/resources` → server applies delta to `TeamSession` totals, emits `ScoreUpdated`

**7.3 — GM force-logout Runner**
- Each `TeamScoreCard` on GM view shows a "Force Logout Runner" button
- Button enabled only when `PlayerSession.IsLoggedIn = true` for that team's Runner
- On click: confirmation dialog ("Are you sure?") → `POST /api/gm/force-logout/{teamId}`
- Server: sets `PlayerSession.IsLoggedIn = false`, `IsOnline = false`, emits `RunnerLoggedOut` targeted to that Runner's SignalR connection
- Runner client receives `RunnerLoggedOut` → navigated to login page
- Button disables after successful force-logout

**7.4 — GM Dashboard — full controls**
- Page at `/gm`: all GM phase controls in one view
- Shows current phase (`PhaseLabel` component), current round, round stake
- Phase-appropriate action buttons: "Start Game" (Pending), "Advance to Voting" (Gathering), "Enable Voting" (Voting / pre-enable), "End Voting Early" (Voting / enabled), "Next Round" / "End Game" (RoundResolution)
- Only the contextually valid button is shown/enabled — never all at once
- Scores summary widget embedded (compact version of Scores page without editing)

**7.5 — Online player list on waiting room**
- Back-fill the Waiting Room page (created in M3) with live data
- Show all logged-in players grouped by team with `OnlineIndicator`
- Updates on `RunnerStateChanged` events

### Done means
- [ ] Scores page shows all teams with correct scores updating live during a game
- [ ] GM can apply a resource bonus/penalty via the dialog; scores update on all clients immediately
- [ ] GM force-logout button is only enabled when a Runner is logged in
- [ ] Clicking force-logout and confirming navigates that Runner's client to the login page
- [ ] GM Dashboard shows only the contextually valid action button at each game phase
- [ ] Waiting room shows live player list with online indicators

---

## M8 — Offline & PWA

**Goal:** The app handles disconnection gracefully — offline banner, map dimming, claim retry on reconnect, full state resync. The app is installable as a PWA and caches static assets correctly.

**PRD references:** §17, §12.6, §5.3, §11.2

### Slices

**8.1 — Offline visual feedback**
- `OfflineBanner` component (created in M3 as a stub): wire to SignalR `ConnectionState`
- When offline: show amber/red full-width banner "You're offline — reconnecting…" with animated spinner
- `MapDimOverlay` component: semi-transparent dark overlay on map during offline; rendered via conditional in `MapPage`
- Trigger zone tap targets disabled (no claim interaction) when offline
- When reconnected: banner and overlay disappear; `OnlineIndicator` in top bar returns to green

**8.2 — Reconnect and full state resync**
- Implement `GET /api/session/state` endpoint: returns complete current session state (phase, round, district ownership, team scores, runner positions, voting state)
- `GameHubService.Reconnected` handler: calls `/api/session/state`, dispatches `FullStateSyncAction` to all stores
- Each Fluxor store handles `FullStateSyncAction` by replacing its state with the received data
- After resync: re-evaluate if any pending claim commands should be re-sent (idempotent retry from §5.3)

**8.3 — Local map movement while offline**
- Verify `watchPosition` continues when offline (it does — it uses device GPS, not network)
- Runner marker moves on map locally even while disconnected
- "Last known position" vs "live position" distinguished only by the banner; no separate UI treatment needed

**8.4 — PWA manifest and service worker**
- Add `manifest.json` to `Konqvist.Client/wwwroot/` with: name, short_name, icons, theme_color (dark game theme), background_color, display: standalone, start_url
- Configure service worker (Blazor WASM offline template) to cache: app shell, static assets, map tiles (via cache-first strategy for tile URLs)
- Prompt Runner to install PWA on login page if not already installed (`beforeinstallprompt` API)
- Prompt Runner to grant "Always Allow" location permission after login

**8.5 — Heartbeat and session expiry**
- Implement client-side heartbeat: `GameHubService` sends a `Heartbeat` SignalR message every 30s while connected
- Server: `GameHub.OnHeartbeat()` updates `PlayerSession.LastSeen = now`
- Background service on server: mark players with `LastSeen` older than 90s as `IsOnline = false` and broadcast `RunnerStateChanged`
- On session cookie expiry during a long disconnect: client receives 401 from `/api/session/state` on reconnect → navigate to login page

### Done means
- [ ] Going offline (DevTools) shows amber banner and dims the map within 1 second
- [ ] District claim tap targets are disabled while offline
- [ ] Reconnecting removes the banner and dim, and the district ownership + scores are fully resynced
- [ ] A claim attempted just before going offline is retried and lands correctly on reconnect
- [ ] The app can be installed to the home screen on Android Chrome and iOS Safari
- [ ] After installation, the app launches in standalone mode (no browser chrome)
- [ ] Map tiles load from cache on second visit (no re-download)
- [ ] Heartbeat updates `LastSeen` in the database every 30s (verifiable in DB)
- [ ] A player missing heartbeats for 90s is marked offline and other clients see the indicator update

---

## M9 — Polish & Deploy

**Goal:** The game feels complete — sound effects, animations, AOT build, and a live deployment on Azure App Service that the build team can access.

**PRD references:** §12.8, §8.3 (AOT section), §8.1

### Slices

**9.1 — Sound effects**
- Add sound files to `Konqvist.Client/wwwroot/sounds/`: claim success, vote cast, phase started, runner online, runner offline
- Implement `SoundService` (JS interop wrapper around `AudioContext`)
- Wire sounds to Fluxor effects: `DistrictClaimedEffect` → play claim sound; `VoteCastEffect` → play vote sound; `GamePhaseChangedEffect` → play phase sound; `RunnerStateChangedEffect` → play online/offline sound
- Sounds respect the user's device mute/silent mode (browser AudioContext inherits device audio policy)

**9.2 — Transition animations**
- Phase transition animation: brief full-screen flash or overlay when `GamePhaseChanged` fires (e.g. "GATHERING BEGINS" text fade-in/out using CSS transition)
- District claim animation: brief pulse/glow on the district polygon when claimed
- Vote bar growth animation: MudBlazor progress bar transitions smoothly to new width
- All animations use CSS transitions — no JS animation libraries

**9.3 — AOT publish configuration**
- Enable AOT in `Konqvist.Client.csproj`:
  ```xml
  <PropertyGroup>
    <RunAOTCompilation>true</RunAOTCompilation>
    <PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>
  ```
- Add `TrimmerRootDescriptor` if any dynamic types need preservation
- Ensure all JSON serialization uses `System.Text.Json` source generators (`[JsonSerializable]` attributes on context classes)
- Run `dotnet publish -c Release` and confirm no trim warnings that would affect runtime
- Verify app loads and all features work correctly in the AOT-compiled build

**9.4 — Azure App Service deployment**
- Create Azure App Service (Linux, .NET 10 runtime)
- Configure connection string for SQLite file path in Azure App Service environment variables
- Set up GitHub Actions (or equivalent) CI/CD pipeline: build → test → publish → deploy
- Configure HTTPS (Azure provides managed TLS certificate)
- Smoke test all major flows on the live deployment: login, claim, vote, GM controls

**9.5 — Final QA pass**
- Play a complete game end-to-end: 2+ teams, 2 rounds, full claim + vote + resolution cycle
- Verify on physical Android and iOS devices (not just DevTools emulation)
- Verify PWA install + standalone launch on both platforms
- Verify AOT-built app performs acceptably on a mid-range Android device (map smooth, < 1s SignalR events)
- Fix any regressions found

### Done means
- [ ] Sound effects play for all key events; no errors in console related to audio
- [ ] Phase transition animation plays when GM advances phases
- [ ] `dotnet publish -c Release` succeeds with no meaningful trim warnings
- [ ] AOT-compiled app loads and a complete game can be played end-to-end
- [ ] App is deployed to Azure App Service and accessible via HTTPS
- [ ] CI/CD pipeline deploys successfully on push to main branch
- [ ] Complete end-to-end game tested on physical Android and iOS devices
- [ ] No critical bugs found in final QA pass

---

## Implementation Notes for the AI Agent

These notes apply to every slice in every milestone:

**Architecture:**
- Always follow Vertical Slice Architecture — one file (or folder) per feature, not horizontal layers
- Every new command handler must go through the `GameAggregate` — never mutate `DbContext` directly from a controller or hub method
- Only the 8 persisted events listed in PRD §8.7 should be written to `GameEvent` table

**Blazor components:**
- No `.razor` file should exceed ~100 lines of markup — extract components aggressively
- Every identifiable UI unit (a card, a bar, a badge, a button group) is its own component
- Shared components live in `Konqvist.Client/Shared/`
- Use MudBlazor components as the base for all standard controls; custom CSS/Tailwind layered on top

**AOT and trimming safety:**
- Use `System.Text.Json` source generators for all serialization — never `JsonSerializer.Serialize<T>(obj)` with reflection
- Never use `dynamic`, `Assembly.LoadFile`, or `Reflection.Emit`
- If using a pattern that might not be trim-safe, add a `// TRIM-UNSAFE:` comment and flag it for review

**Testing:**
- Each slice should be manually verified using the "Done means" checklist of its milestone before moving to the next slice
- Unit tests for `GameAggregate` methods are encouraged but not blocking — prioritise them in M4 while the aggregate is small

**Database:**
- Always add a new EF Core migration for any schema change — never hand-edit the database
- Migration naming convention: `{MilestoneNumber}_{SliceNumber}_{Description}` e.g. `1_2_InitialSchema`

---

*End of Document*
