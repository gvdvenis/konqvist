# Konqvist.Data

This project provides the core data models and data store logic for the Konqvist game platform. It is responsible for managing game state, teams, rounds, voting, and resource calculations.

## Project Structure

```
Konqvist.Data/
??? Contracts/           # Shared contracts and records (e.g., TeamResources, DistrictOwner)
??? Models/              # Core data models (e.g., TeamData, MapData, ResourcesData, SnapshotData)
??? Stores/              # Data store classes (e.g., MapDataStore, VotingDataStore, SnapshotDataStore, RoundDataStore)
??? MapDataHelper.cs     # Static helpers for loading map and team data from JSON
??? KmlToMapDataConverter.cs # (Optional) KML conversion utilities
??? Konqvist.Data.csproj # Project file
```

## Main Components

- **Models**: Represent the main entities in the game (teams, maps, resources, rounds, snapshots, etc).
- **Stores**: Manage the state and logic for different aspects of the game (map, voting, rounds, snapshots).
- **Contracts**: Define shared records and interfaces for use across stores and models.

## Data Store Dependency Graph

Below is a simplified dependency graph showing how the main data stores and models interact:

```mermaid
graph TD
    MapDataStore -->|uses| MapData
    MapDataStore -->|uses| TeamData
    MapDataStore -->|uses| RoundDataStore
    MapDataStore -->|uses| VotingDataStore
    MapDataStore -->|uses| SnapshotDataStore
    MapDataStore -->|uses| ResourcesData
    MapDataStore -->|uses| DistrictData
    MapDataStore -->|uses| TeamResources
    MapDataStore -->|uses| DistrictOwner
    SnapshotDataStore -->|uses| SnapshotData
    SnapshotDataStore -->|uses| TeamResources
    SnapshotDataStore -->|uses| DistrictOwner
    SnapshotDataStore -->|uses| RoundData
    SnapshotDataStore -->|uses| VotingData (Votes, Voters)
    VotingDataStore -->|uses| VotingData (Votes, Voters)
    VotingDataStore -->|uses| TeamData
    VotingDataStore -->|uses| RoundData
    RoundDataStore -->|uses| RoundData
    TeamResources -->|uses| TeamData
    TeamResources -->|uses| ResourcesData
    DistrictData -->|uses| TeamData
    MapData -->|uses| DistrictData
```

## Key Flows

- **MapDataStore** orchestrates the main game state, delegating to other stores for voting, rounds, and snapshots.
- **VotingDataStore** manages voting state per round.
- **SnapshotDataStore** stores round-by-round snapshots and calculates team scores (including voting bonuses).
- **TeamResources** encapsulates resource calculations for teams.

---

For more details, see the inline documentation in each class.
