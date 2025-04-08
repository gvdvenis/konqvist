using System.Collections.Concurrent;
using Konqvist.Data.Contracts;
using Konqvist.Data.Models;
using OpenLayers.Blazor;

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
    private RoundDataStore _roundsData = RoundDataStore.Empty;

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
        await InitializeMapDataAsync(mapData);

        var teamsData = await MapDataHelper.GetTeamsData();
        await InitializeTeamsDataAsync(teamsData);

        //var roundsData = await MapDataHelper.GetRoundsData();
        await InitializeRoundsDataAsync(null);
    }

    /// <summary>
    /// Initializes the data store with map data
    /// </summary>
    public async Task InitializeMapDataAsync(MapData mapData)
    {
        await _semaphore.WaitAsync();
        try
        {
            _mapData = mapData;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Initializes the data store with team data
    /// </summary>
    public async Task InitializeTeamsDataAsync(IEnumerable<TeamData>? teamsData)
    {
        await _semaphore.WaitAsync();
        try
        {
            _teamsData = [.. teamsData ?? []];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task InitializeRoundsDataAsync(List<RoundData>? roundsData)
    {
        await _semaphore.WaitAsync();
        try
        {
            roundsData ??=
            [
                new RoundData(0, "Waiting for Game Start", RoundKind.NotStarted),
                new RoundData(1, "Running 1", RoundKind.GatherResources),
                new RoundData(2, "Voting 1", RoundKind.Voting),
                new RoundData(3, "Running 2", RoundKind.GatherResources),
                new RoundData(4, "Voting 2", RoundKind.Voting),
                new RoundData(5, "Running 3", RoundKind.GatherResources),
                new RoundData(6, "Voting 3", RoundKind.Voting),
                new RoundData(7, "Running 4", RoundKind.GatherResources),
                new RoundData(8, "Voting 4", RoundKind.Voting),
                new RoundData(9, "Game Over", RoundKind.Finished)
            ];

            _roundsData = new RoundDataStore(roundsData);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

    #region ReadData methods

    /// <summary>
    ///     Gets a copy of all map data
    /// </summary>
    public async Task<MapData> GetMapData()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _mapData;
        }
        finally
        {
            _semaphore.Release();
        }
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

        await _semaphore.WaitAsync();
        try
        {
            var teams = _teamsData
                 .Where(t => !onlyLoggedIn || t.PlayerLoggedIn)
                 .Where(t => includeDisabled || !t.IsDisabled);

            return [.. teams.Select(td =>
            {
                td.Location = td.Location == Coordinate.Empty
                    ? GetDefaultLocation()
                    : td.Location;
                return td;
            })];
        }
        finally
        {
            _semaphore.Release();
        }

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
    public async Task<TeamData?> GetTeamByName(string teamName)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _teamsData
                .SingleOrDefault(td => td.Name == teamName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Gets all districts
    /// </summary>
    public async Task<List<DistrictData>> GetAllDistricts()
    {
        await _semaphore.WaitAsync();
        try
        {
            return [.. _mapData.Districts];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     returns the total amount of resources for a team
    /// </summary>
    /// <param name="teamName"></param>
    /// <returns></returns>
    public async Task<ResourcesData> GetResourcesForTeam(string teamName)
    {
        var team = await GetTeamByName(teamName);

        await _semaphore.WaitAsync();
        try
        {
            var currentAdditionalTeamResources= team?.AdditionalResources ?? ResourcesData.Empty;
            
            var destrictResources = _mapData.Districts
                .Where(dd => dd.Owner is not null && dd.Owner.Name == teamName)
                .Select(dd => dd.Resources)
                .Aggregate(new ResourcesData(), (acc, item) => acc + item);

            var totalResources = currentAdditionalTeamResources + destrictResources;

            return totalResources;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public async Task<RoundDataStore> GetRounds()
    {
        return await Task.FromResult(_roundsData);
    }

    #endregion

    #region WriteData methods

    /// <summary>
    /// Sets the owner of a district
    /// </summary>
    public async Task<bool> SetDistrictOwner(DistrictOwner districtOwner)
    {
        string districtName = districtOwner.DistrictName;
        string newOwnerName = districtOwner.TeamName;

        await _semaphore.WaitAsync();

        try
        {
            var district = _mapData.Districts.FirstOrDefault(d => d.Name == districtName);
            if (district is null) return false;

            var newOwner = _teamsData.FirstOrDefault(t => t.Name == newOwnerName);
            if (newOwner is null) return false;

            district.Owner = newOwner;
            district.IsClaimable = false;

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateTeamPosition(string teamName, Coordinate coordinate)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (TeamByName(teamName) is { } team)
                team.Location = coordinate;
        }
        finally
        {
            _semaphore.Release();
        }
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

    private TeamData? TeamByName(string name) => _teamsData.FirstOrDefault(t => t.Name == name);

    public async Task<bool> TryLoginTeamMember(
        TeamData? teamData,
        TeamMemberRole? role = TeamMemberRole.Observer)
    {
        if (teamData is null) return false;

        // Team captains and observers are always allowed to log in
        if (role is not TeamMemberRole.Runner) return true;

        await _semaphore.WaitAsync();
        try
        {
            // Runners are only allowed to log in once
            if (teamData.PlayerLoggedIn) return false;

            teamData.PlayerLoggedIn = true;
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> LogoutRunner(string teamName)
    {
        // we don't bother with observers and team captains for now
        //if (role is not TeamMemberRole.Runner) return true;

        await _semaphore.WaitAsync();
        try
        {
            var team = _teamsData.FirstOrDefault(t => t.Name == teamName);
            if (team == null) return false;
            team.PlayerLoggedIn = false;
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<string>> LogoutAllRunners()
    {
        await _semaphore.WaitAsync();
        try
        {
            List<string> loggedOutPlayerTeamNames = [];

            foreach (var team in _teamsData)
            {
                if (team.PlayerLoggedIn)
                    loggedOutPlayerTeamNames.Add(team.Name);

                team.PlayerLoggedIn = false;
            }

            return loggedOutPlayerTeamNames;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearClaims(string? teamName = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var districts = _mapData
                .Districts
                .Where(d => teamName is null || d.Owner is { } td && td.Name == teamName);

            foreach (var districtData in districts)
            {
                districtData.IsClaimable = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<RoundData?> NextRound()
    {
        await _semaphore.WaitAsync();
        try
        {

            return _roundsData.NextRound();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<RoundData?> PreviousRound()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _roundsData.PreviousRound();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Sets the additional resources for a team. Please note that
    ///     this replaces the current resources and does not add to them. 
    /// </summary>
    /// <param name="teamName"></param>
    /// <param name="resourcesReplacement"></param>
    /// <returns></returns>
    public async Task SetAdditionalTeamResource(string teamName, ResourcesData resourcesReplacement)
    {
        var team = await GetTeamByName(teamName);
        
        if (team is null) return;
        
        await _semaphore.WaitAsync();
        try
        {
            team.AdditionalResources += resourcesReplacement;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    #endregion

}