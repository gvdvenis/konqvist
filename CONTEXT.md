# Konqvist

The project distinguishes immutable game setup from mutable saved play so operators can reset a game without losing the seeded content.

## Game State

**Game definition**:
The immutable seeded map, districts, teams, and round sequence that define a game.
_Avoid_: catalog, seed data, static setup

**Gameplay state**:
The mutable saved match progress, including district ownership, votes, scores, team positions, login state, and round progress.
_Avoid_: match state, snapshot, save file

**Reset**:
A user-invoked action that clears gameplay state and starts a new game from the current game definition.
_Avoid_: restart, reboot

**Restart**:
An app recovery event that restores the latest gameplay state after an interruption.
_Avoid_: reset
