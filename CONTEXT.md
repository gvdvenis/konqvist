# Konqvist

Konqvist is a game app with a durable match record and a separate authentication layer. The glossary below keeps the mutable game state distinct from the seed catalog and from user sessions.

## Language

**Game state**:
The mutable record of a match: round progress, district ownership, team positions, votes, scores, and temporary team resources.
_Avoid_: app state, gameplay state

**Game catalog**:
The seeded map, district, team, and round definitions that establish a new match.
_Avoid_: static data, setup data, seed data

**Credential**:
The secret value a player enters to authenticate.
_Avoid_: login, password

**Session**:
The authenticated user state for a browser or player.
_Avoid_: login, credential
