using Konqvist.Data.Contracts;
using Konqvist.Data.Models;
using OpenLayers.Blazor;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Konqvist.Data.Stores;

public class MapDataStore
{
    // Singleton instance
    private static MapDataStore? _instance;

    // SemaphoreSlim for controlling concurrent read/write access
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Storage for map and team data
    private MapData _mapData = MapData.Empty;
    private ConcurrentBag<TeamData> _teamsData = [];
    private RoundDataStore _roundsDataStore = RoundDataStore.Empty;

    public bool TestmodeEnabled { get; set; } = Debugger.IsAttached;

    private MapDataStore() { }

    public static async Task<MapDataStore> GetInstanceAsync()
    {
        if (_instance != null) return _instance;
        _instance = new MapDataStore();
        await _instance.InitializeAsync().ConfigureAwait(false);
        return _instance;
    }

    #region Initializers

    private async Task InitializeAsync()
    {
        var mapData = await MapDataHelper.GetMapData().ConfigureAwait(false);
        _mapData = mapData;

        var teamsData = await MapDataHelper.GetTeamsData();
        _teamsData = [.. teamsData];

        var roundsData = await MapDataHelper.GetRoundsData();
        _roundsDataStore = new RoundDataStore(roundsData);
    }

    #endregion

    #region ReadData methods

    /// <summary>
    ///     Gets a copy of all map data
    /// </summary>
    public async Task<MapData> GetMapData()
    {
        return await ProtectedInvoke(() => _mapData);
    }

    /// <summary>
    ///     Gets a copy of all teams data, optionally filtering out
    ///     teams where no runner is logged in (they should not be shown on the map)
    ///     and/or teams that have been disabled (not participating in the game).
    ///     If Location is not set, a default location is assigned.
    /// </summary>
    public async Task<List<TeamData>> GetTeams(
        bool onlyLoggedIn = false,
        bool includeDisabled = false)
    {
        var counter = 0;

        return await ProtectedInvoke(() =>
        {
            var teams = _teamsData
                .Where(t => !onlyLoggedIn || t.PlayerLoggedIn)
                .Where(t => includeDisabled || !t.IsDisabled);

            List<TeamData> result = [];
            foreach (var td in teams)
            {
                td.Location = td.Location == Coordinate.Empty
                    ? GetDefaultLocation()
                    : td.Location;
                result.Add(td);
            }
            return result;
        });

        Coordinate GetDefaultLocation()
        {
            double maxX = _mapData.Coordinates.Max(c => c.X);
            double maxY = _mapData.Coordinates.Max(c => c.Y);
            counter++;

            return new Coordinate(maxX, maxY - 0.0015 * counter);
        }
    }

    /// <summary>
    ///     Returns a single teams data. Please note that the location
    ///     of the team is NOT falling back to a default location as is
    ///     the case with the <see cref="GetTeams"/> method.
    /// </summary>
    public async Task<TeamData> GetTeamByName(string teamName)
    {
        return await ProtectedInvoke(() => TeamByName(teamName));
    }

    /// <summary>
    ///     Gets all districts
    /// </summary>
    public async Task<List<DistrictData>> GetAllDistricts()
    {
        return await ProtectedInvoke(() => _mapData.Districts.ToList());
    }

    /// <summary>
    ///     returns the total amount of resources for a team
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    public async Task<ResourcesData> GetResourcesForTeam(string teamName)
    {
        var team = await GetTeamByName(teamName);

        return await ProtectedInvoke(() =>
        {
            var currentAdditionalTeamResources = team.AdditionalResources;
            var districtResources = GetCurrentDistrictResources(team);
            return currentAdditionalTeamResources + districtResources;
        });
    }

    private ResourcesData GetCurrentDistrictResources(TeamData? team)
    {
        return _mapData.Districts
            .Where(dd => dd.Owner is not null && dd.Owner.Name == team?.Name)
            .Select(dd => dd.Resources)
            .Aggregate(new ResourcesData(), (acc, item) => acc + item);
    }

    public async Task<string?> GetCurrentResourceOfInterest()
    {
        return await ProtectedInvoke(() =>
            _roundsDataStore.GetCurrentRound().ResourceOfInterest);
    }

    public async Task<RoundDataStore> GetRoundsDataStore()
    {
        return await Task.FromResult(_roundsDataStore);
    }

    /// <summary>
    ///     returns the team scores for all teams in the game at the current moment.
    /// </summary>
    /// <returns></returns>
    public Task<List<TeamScore>> GetAllTeamScores()
    {
        return ProtectedInvoke(() => _teamsData
            .Select(team => new TeamScore(team.Name,  team.GetScoreTotalForRound(_roundsDataStore.CurrentRoundNumber, true)))
            .ToList()
        );
    }

    /// <summary>
    ///     returns the cumulative score for a single team at the current point in time.
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    public Task<TeamScore> GetTeamScore(string teamName)
    {
        return ProtectedInvoke(() =>
        {
            var team = TeamByName(teamName);
            return new TeamScore(team.Name, team.GetScoreTotalForRound(_roundsDataStore.CurrentRoundNumber, true));
        });
    }

    /// <summary>
    ///     Calculates the score for the owned district resources and adds it to the scores list.
    /// </summary>
    /// <param name="team"></param>
    /// <param name="resourceOfInterest"></param>
    private TeamScore CalculateTeamResourceScore(TeamData team, string? resourceOfInterest)
    {
        // check if the resource of interest is null or empty
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceOfInterest);

        // calculate the score for the owned district resources
        var allResources = _mapData
            .GetResourcesForTeam(team.Name) + team.AdditionalResources;

        // now calculate the score based on the total amount of
        // resources and the extra weight of the resource of interest
        int resourceScore = allResources
            .CalculateVoteWeight(resourceOfInterest);

        return new TeamScore(team.Name, resourceScore);
    }

    public async Task<RoundKind> GetCurrentAppState()
    {
        return await ProtectedInvoke(() =>
            _roundsDataStore.GetCurrentRound().Kind);
    }

    public async Task<bool> HasTeamVotedInCurrentRound(string teamName)
    {
        return await ProtectedInvoke(() => TeamByName(teamName)
            .HasVoted(_roundsDataStore.CurrentRoundNumber));
    }

    #endregion

    #region WriteData methods

    public async Task<DistrictOwner> GetDistrictOwner(string districtName)
    {
        return await ProtectedInvoke(() =>
        {
            var district = _mapData.Districts.FirstOrDefault(d => d.Name == districtName);
            if (district is null) return DistrictOwner.Empty;

            var owner = district.Owner;
            return owner is not null
                ? new DistrictOwner(owner.Name, district.Name)
                : DistrictOwner.Empty;
        });
    }

    /// <summary>
    ///     Sets the owner of a district
    /// </summary>
    public async Task<bool> SetDistrictOwner(DistrictOwner districtOwner)
    {
        string districtName = districtOwner.DistrictName;
        string newOwnerName = districtOwner.TeamName;

        return await ProtectedInvoke(() =>
        {
            var district = _mapData.Districts.FirstOrDefault(d => d.Name == districtName);

            // if the district is not found or is not claimable, we return false
            if (district is null || district.IsClaimable == false) 
                return false;

            var newOwner = TeamByName(newOwnerName);

            district.AssignDistrictOwner(newOwner);

            return true;
        });
    }

    public async Task UpdateTeamPosition(string teamName, Coordinate coordinate)
    {
        await ProtectedInvoke(() =>
        {
            if (TeamByName(teamName) is { } team)
                team.Location = coordinate;
        });
    }

    public async Task<(TeamData?, TeamMemberRole role)?> TryLoginTeamMember(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return null;

        (TeamData? teamData, TeamMemberRole role)? td = password switch
        {
            "agm" => (TeamByName("Alpha"), TeamMemberRole.GameMaster),

            "btc" => (TeamByName("Bravo"), TeamMemberRole.TeamCaptain),
            "br" => (TeamByName("Bravo"), TeamMemberRole.Runner),
            "b" => (TeamByName("Bravo"), TeamMemberRole.Observer),

            "ctc" => (TeamByName("Charly"), TeamMemberRole.TeamCaptain),
            "cr" => (TeamByName("Charly"), TeamMemberRole.Runner),
            "c" => (TeamByName("Charly"), TeamMemberRole.Observer),

            "dtc" => (TeamByName("Delta"), TeamMemberRole.TeamCaptain),
            "dr" => (TeamByName("Delta"), TeamMemberRole.Runner),
            "d" => (TeamByName("Delta"), TeamMemberRole.Observer),

            "etc" => (TeamByName("Echo"), TeamMemberRole.TeamCaptain),
            "er" => (TeamByName("Echo"), TeamMemberRole.Runner),
            "e" => (TeamByName("Echo"), TeamMemberRole.Observer),

            "ftc" => (TeamByName("Foxtrot"), TeamMemberRole.TeamCaptain),
            "fr" => (TeamByName("Foxtrot"), TeamMemberRole.Runner),
            "f" => (TeamByName("Foxtrot"), TeamMemberRole.Observer),

            "gtc" => (TeamByName("Golf"), TeamMemberRole.TeamCaptain),
            "gr" => (TeamByName("Golf"), TeamMemberRole.Runner),
            "g" => (TeamByName("Golf"), TeamMemberRole.Observer),

            "htc" => (TeamByName("Hotel"), TeamMemberRole.TeamCaptain),
            "hr" => (TeamByName("Hotel"), TeamMemberRole.Runner),
            "h" => (TeamByName("Hotel"), TeamMemberRole.Observer),

            "itc" => (TeamByName("India"), TeamMemberRole.TeamCaptain),
            "ir" => (TeamByName("India"), TeamMemberRole.Runner),
            "i" => (TeamByName("India"), TeamMemberRole.Observer),

            _ => null
        };

        return await TryLoginTeamMember(td?.teamData, td?.role)
            ? (td?.teamData, td?.role ?? TeamMemberRole.Observer)
            : null;
    }

    private TeamData TeamByName(string name) => _teamsData.FirstOrDefault(t => t.Name == name) 
        ?? TeamData.Empty;

    public async Task<bool> TryLoginTeamMember(
        TeamData? teamData,
        TeamMemberRole? role = TeamMemberRole.Observer)
    {
        if (teamData is null) return false;

        // Team captains and observers are always allowed to log in
        if (role is not TeamMemberRole.Runner) 
            return true;

        return await ProtectedInvoke(() =>
        {
            // Runners are only allowed to log in once
            if (teamData.PlayerLoggedIn) return false;

            teamData.PlayerLoggedIn = true;
            return true;
        });
    }

    public async Task<bool> LogoutRunner(string teamName)
    {
        // we don't bother with observers and team captains for now
        // if (role is not TeamMemberRole.Runner) return true;

        return await ProtectedInvoke(() =>
        {
            var team = TeamByName(teamName);
            team.PlayerLoggedIn = false;
            return true;
        });
    }

    public async Task<List<string>> LogoutAllRunners()
    {
        return await ProtectedInvoke(() =>
        {
            List<string> loggedOutPlayerTeamNames = [];

            foreach (var team in _teamsData)
            {
                if (team.PlayerLoggedIn)
                    loggedOutPlayerTeamNames.Add(team.Name);

                team.PlayerLoggedIn = false;
            }

            return loggedOutPlayerTeamNames;
        });
    }

    public async Task ClearClaims(string? teamName = null)
    {
        await ProtectedInvoke(() =>
        {
            ClearClaimsInternal(teamName);
        });
    }

    public async Task<RoundData?> NextRound()
    {
        return await ProtectedInvoke(() =>
        {
            var currentRound = _roundsDataStore.GetCurrentRound();

            // we only want to reset our resources when we're exiting a voting round
            if (currentRound.Kind != RoundKind.Voting) return _roundsDataStore.NextRound();

            // calculate the scores based on the resources gathered
            AssignTeamResourceScores(currentRound);

            // calculate the bonus scores based on the voting results
            AssignVoteBonusScores(currentRound.Index);

            // additional resource scores are only assigned once during the voting round
            FlushAllTeamsAdditionalResources();
            
            // reset all trigger circles 
            ClearClaimsInternal(null);

            return _roundsDataStore.NextRound();
        });
    }

    private void AssignTeamResourceScores(RoundData round)
    {
        foreach (var team in _teamsData)
        {
            team.LogScore(
                CalculateTeamResourceScore(team, round.ResourceOfInterest).Amount,
                round.Index,
                ScoreType.Resource);
        }
    }

    private void AssignVoteBonusScores(int roundNumber)
    {
        const int bonusPoints = 150;

        int maxVotesAmount = _teamsData
            .Max(td => td.GetTotalVotesAmount(roundNumber));

        if (maxVotesAmount == 0 ) return;

        // first get the team or teams that received the most votes
        var teamsWithMostVotes = _teamsData
            .Where(td => td.GetTotalVotesAmount(roundNumber) == maxVotesAmount);

        // now determine the teams that voted for those winning teams
        var teamsThatVotedForWinner = teamsWithMostVotes
            .SelectMany(tmv=> tmv.Votes
                .Select(v=>TeamByName(v.Voter)))
            .ToList();

        // next we divide the bonus of 150 points between teams that voted for the winner
        int bonusPerTeam = teamsThatVotedForWinner.Count > 0
            ? bonusPoints / teamsThatVotedForWinner.Count
            : 0;

        foreach (var team in teamsThatVotedForWinner)
        {
            team.LogScore(bonusPerTeam, roundNumber, ScoreType.Vote);
        }
    }

    private void FlushAllTeamsAdditionalResources()
    {
        foreach (var team in _teamsData)
        {
            team.AdditionalResources = ResourcesData.Empty;
        }
    }

    public async Task ResetGame()
    {
        await ProtectedInvoke(async () =>
        {
            await InitializeAsync();
        });
    }

    /// <summary>
    ///     Adds additional resources to a team. 
    /// </summary>
    /// <param name="teamName"></param>
    /// <param name="resourcesReplacement"></param>
    /// <returns></returns>
    public async Task SetAdditionalTeamResource(string teamName, ResourcesData resourcesReplacement)
    {
        await ProtectedInvoke(() =>
        {
            var team = TeamByName(teamName);
            team.LogAdditionalResource(resourcesReplacement);
            return Task.CompletedTask;
        });
    }

    public async Task CastVoteFor(string recipientTeamName, string voterTeamName)
    {
        int voteWeight = await GetVoteWeightForTeam(voterTeamName);

        await ProtectedInvoke(() =>
        {
            int roundNumber = _roundsDataStore.CurrentRoundNumber;
            var receiver = TeamByName(recipientTeamName);
            var voter = TeamByName(voterTeamName);

            if (voter.HasVoted(roundNumber))
                return;

            receiver.LogReceivedVote(voter.Name, voteWeight, roundNumber);
            voter.LogCastVote(receiver.Name, roundNumber);
        });
    }

    public async Task<List<TeamVote>> GetVotesForCurrentRound()
    {
        return await ProtectedInvoke(() =>
        {
            int roundNumber = _roundsDataStore.CurrentRoundNumber;
            return _teamsData
                .Select(t => new TeamVote(t.Name, t.GetScoreTotalForRound(roundNumber)))
                .ToList();
        });
    }

    public async Task<int> GetVoteWeightForTeam(string teamName)
    {
        var teamResources = await GetResourcesForTeam(teamName);
        string? resourceOfInterest = await GetCurrentResourceOfInterest();
        return teamResources.CalculateVoteWeight(resourceOfInterest);
    }

    #endregion

    /// <summary>
    ///     Resets all trigger circles to be reclaimable. If a teamName is provided,
    ///     only the trigger circles of that team are set to be reclaimable.
    /// </summary>
    /// <param name="teamName"></param>
    private void ClearClaimsInternal(string? teamName)
    {
        var districts = _mapData
            .Districts
            .Where(d => teamName is null || d.Owner is { } td && td.Name == teamName);

        foreach (var districtData in districts)
        {
            districtData.ReleaseClaim();
        }
    }

    // Helper for protected invocation with return value
    private async Task<T> ProtectedInvoke<T>(Func<T> callback)
    {
        await _semaphore.WaitAsync();
        try
        {
            return callback();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION: {ex.Message}\nDetails: {ex}");
            throw; // Rethrow the exception to propagate it to the caller
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task ProtectedInvoke(Action callback)
    {
        await _semaphore.WaitAsync();
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION: {ex.Message}\nDetails: {ex}");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}