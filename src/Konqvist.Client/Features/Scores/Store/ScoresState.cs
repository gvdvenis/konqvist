using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Scores.Store;

[FeatureState]
public sealed record ScoresState
{
    public IReadOnlyDictionary<int, int> TeamScores { get; init; } = new Dictionary<int, int>();

    public IReadOnlyDictionary<int, TeamResourceTotals> TeamResources { get; init; } =
        new Dictionary<int, TeamResourceTotals>();

    private ScoresState()
    {
    }
}
