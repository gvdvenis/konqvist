using election_game.Data.Contracts;
using election_game.Data.Models;

namespace election_game.Data.Stores;

public class MapDataStore
{
    // Singleton instance
    private static MapDataStore? _instance;

    public static async Task<MapDataStore> GetInstanceAsync()
    {
        if (_instance != null) return _instance;
        _instance = new MapDataStore();
        await _instance.InitializeAsync();
        return _instance;
    }

    // SemaphoreSlim for controlling concurrent read/write access
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Storage for map and team data
    private MapData _mapData = MapData.Empty;
    private TeamData[] _teamsData = [];

    // Events for notifying subscribers of data changes
    public event Func<string, string, Task>? DistrictOwnerChanged;

    private async Task InitializeAsync()
    {
        var mapData = await MapDataHelper.GetMapData() ?? MapData.Empty;
        await InitializeMapDataAsync(mapData);

        var teamsData = await MapDataHelper.GetTeamsData();
        await InitializeTeamsDataAsync(teamsData);
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
    public async Task InitializeTeamsDataAsync(TeamData[]? teamsData)
    {
        await _semaphore.WaitAsync();
        try
        {
            _teamsData = teamsData ?? [];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets a copy of all map data
    /// </summary>
    public async Task<MapData> GetMapDataAsync()
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
    /// Gets a copy of all teams data
    /// </summary>
    public async Task<TeamData[]> GetTeamsDataAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _teamsData;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets a specific district by name
    /// </summary>
    public async Task<DistrictData?> GetDistrictByNameAsync(string districtName)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _mapData.Districts.FirstOrDefault(d => d.Name == districtName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Sets the owner of a district
    /// </summary>
    public async Task<bool> SetDistrictOwnerAsync(DistrictOwner districtOwner)
    {
        string districtName = districtOwner.DistrictName;
        string? teamName = districtOwner.TeamName;

        await _semaphore.WaitAsync();

        try
        {
            var district = _mapData.Districts.FirstOrDefault(d => d.Name == districtName);
         
            if (district == null) return false;

            TeamData? newOwner = null;
            if (teamName != null)
            {
                newOwner = _teamsData.FirstOrDefault(t => t.Name == teamName);
                if (newOwner == null) return false;
            }

            district.Owner = newOwner;

            // Notify subscribers
            if (DistrictOwnerChanged != null)
            {
                await DistrictOwnerChanged.Invoke(districtName, teamName ?? "");
            }

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets all districts
    /// </summary>
    public async Task<List<DistrictData>> GetAllDistrictsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _mapData.Districts.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Updates a district's resources
    /// </summary>
    public async Task<bool> UpdateDistrictResourcesAsync(string districtName, DistrictResourcesData resources)
    {
        await _semaphore.WaitAsync();
        try
        {
            var district = _mapData.Districts.FirstOrDefault(d => d.Name == districtName);
            if (district == null) return false;

            district.Resources = resources;
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
