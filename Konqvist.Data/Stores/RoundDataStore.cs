using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public class RoundDataStore(List<RoundData> rounds)
{

    public int CurrentRound { get; private set; }

    public List<RoundData> Rounds { get; } = rounds;

    public RoundData? GetCurrentRound()
    {
        return Rounds.FirstOrDefault(r => r.Order == CurrentRound);
    }

    public RoundData? NextRound()
    {
        if (CurrentRound >= Rounds.Count - 1)
            return null;

        CurrentRound++;
        return GetCurrentRound();
    }

    public RoundData? PreviousRound()
    {
        if (CurrentRound == 0)
            return null;

        CurrentRound--;
        return GetCurrentRound();
    }

    public static RoundDataStore Empty { get; } = new([]);
}