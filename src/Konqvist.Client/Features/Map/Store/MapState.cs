using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Map.Store;

[FeatureState]
public sealed record MapState
{
    public IReadOnlyDictionary<int, int?> DistrictOwners { get; init; } = new Dictionary<int, int?>();

    public IReadOnlyDictionary<int, RunnerPosition> RunnerPositions { get; init; } =
        new Dictionary<int, RunnerPosition>();

    private MapState()
    {
    }
}
