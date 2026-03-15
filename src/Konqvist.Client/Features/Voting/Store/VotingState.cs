using Fluxor;

namespace Konqvist.Client.Features.Voting.Store;

[FeatureState]
public sealed record VotingState
{
    public IReadOnlyDictionary<int, int> VotesPerTeam { get; init; } = new Dictionary<int, int>();

    public bool IsVotingOpen { get; init; }

    public TimeSpan? TimeRemaining { get; init; }

    private VotingState()
    {
    }
}
