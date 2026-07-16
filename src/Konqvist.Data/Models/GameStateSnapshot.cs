using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public record GameStateSnapshot(
    string? GameDefinitionHash,
    int CurrentRoundNumber,
    List<DistrictStateSnapshot> Districts,
    List<TeamStateSnapshot> Teams)
{
    public static GameStateSnapshot Empty { get; } = new(null, 0, [], []);
}

public record DistrictStateSnapshot(string Name, string? OwnerTeamName, bool IsClaimable);

public record TeamStateSnapshot(
    string Name,
    Coordinate Location,
    bool PlayerLoggedIn,
    ResourcesData AdditionalResources,
    List<ScoreData> Scores,
    List<VoteData> Votes,
    List<VoterData> CastVotes);
