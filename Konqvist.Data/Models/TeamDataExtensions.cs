namespace Konqvist.Data.Models;

public static class TeamDataExtensions
{
    /// <summary>
    ///     Calculates the total score for a team in a specific round. If
    ///     cumulative is true, it sums up all scores up to and including the specified round.
    /// </summary>
    /// <param name="team">The team for which the score is calculated.</param>
    /// <param name="round">The round for which the score is calculated.</param>
    /// <param name="cumulative">Indicates whether to calculate cumulative score.</param>
    /// <returns>The total (cumulative) score for the specified round.</returns>
    public static int GetScoreTotalForRound(this TeamData team, int round, bool cumulative = true)
    {
        return cumulative
            ? team.Scores.TakeWhile(s => s.Round <= round).Sum(s => s.Amount)
            : team.Scores.Where(s => s.Round == round).Sum(s => s.Amount);
    }
}