namespace Konqvist.Data.Models;

public class TeamData(string name, string color) : ActorData(name, color)
{
    public static TeamData Empty { get; } = new(string.Empty, "#05203b");
    public bool IsDisabled { get; internal set; }
    
    public IReadOnlyList<ScoreData> Scores { get;  } = new List<ScoreData>();
    public IReadOnlyList<VoteData> Votes { get; } = new List<VoteData>();
    public IReadOnlyList<VoterData> CastVotes { get; } = new List<VoterData>();
    public ResourcesData AdditionalResources { get; internal set; } = ResourcesData.Empty;
    public bool PlayerLoggedIn { get; internal set; }

    internal bool HasVoted(int roundNumber) => CastVotes.Any(cv => cv.Round == roundNumber);

    /// <summary>
    ///     Registers a vote that this team has cast
    /// </summary>
    /// <param name="receiverTeamName"></param>
    /// <param name="roundNumber"></param>
    internal void LogCastVote(string receiverTeamName, int roundNumber)
    {
        var voterData = new VoterData(receiverTeamName, roundNumber);
        ((List<VoterData>)CastVotes).Add(voterData);
    }

    /// <summary>
    ///     Registers a vote that this team has received
    /// </summary>
    /// <param name="voterTeamName"></param>
    /// <param name="amount"></param>
    /// <param name="roundNumber"></param>
    internal void LogReceivedVote(string voterTeamName, int amount, int roundNumber)
    {
        var voteData = new VoteData(voterTeamName, amount, roundNumber);
        ((List<VoteData>)Votes).Add(voteData);
    }

    internal void LogScore(int amount, int roundNumber, ScoreType scoreType )
    {
        var newScore = new ScoreData(amount, roundNumber, scoreType);
        ((List<ScoreData>)Scores).Add(newScore);
    }

    internal int GetTotalVotesAmount(int roundNumber)
    {
        return Votes
            .Where(v => v.Round == roundNumber)
            .Sum(v => v.Amount);
    }

    internal void LogAdditionalResource(ResourcesData resourceData)
    {
        AdditionalResources += resourceData;
    }

}

public enum TeamMemberRole
{
    TeamCaptain,
    Runner,
    Observer,
    GameMaster
}