using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public class RoundDataStore(List<RoundData> rounds)
{

    public int CurrentRoundNumber { get; private set; }

    public List<RoundData> Rounds { get; } = rounds;

    public RoundData GetCurrentRound()
    {
        return Rounds.FirstOrDefault(r => r.Order == CurrentRoundNumber) ?? RoundData.Empty;
    }

    public RoundData? NextRound()
    {
        if (CurrentRoundNumber >= Rounds.Count - 1)
            return null;

        CurrentRoundNumber++;
        return GetCurrentRound();
    }

    public RoundData? PreviousRound()
    {
        if (CurrentRoundNumber == 0)
            return null;

        CurrentRoundNumber--;
        return GetCurrentRound();
    }

    public static RoundDataStore Empty { get; } = new([]);
}