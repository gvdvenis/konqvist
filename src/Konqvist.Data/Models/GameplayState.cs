using OpenLayers.Blazor;

namespace Konqvist.Data.Models;

public record GameplayState(
    string? GameDefinitionHash,
    int CurrentRoundNumber,
    List<DistrictGameplayState> Districts,
    List<TeamGameplayState> Teams)
{
    public static GameplayState Empty { get; } = new(null, 0, [], []);
}

public record DistrictGameplayState(string Name, string? OwnerTeamName, bool IsClaimable);

public record TeamGameplayState(
    string Name,
    Coordinate Location,
    bool PlayerLoggedIn,
    ResourcesData AdditionalResources,
    List<ScoreData> Scores,
    List<VoteData> Votes,
    List<VoterData> CastVotes);
