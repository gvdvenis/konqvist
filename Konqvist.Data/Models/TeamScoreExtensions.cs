using Konqvist.Data.Stores;

namespace Konqvist.Data.Models
{
    public static class TeamScoreExtensions
    {
        public static int GetScoreForTeam(this IEnumerable<TeamScore>? teamScores, string teamName)
        {
            return teamScores?.FirstOrDefault(ts => ts.TeamName == teamName)?.Score ?? 0;
        }
    }
}
