using Konqvist.Data.Contracts;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using OpenLayers.Blazor;
using Xunit.Abstractions;

namespace Konqvist.Data.Tests;

public class MapDataStoreTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly InMemoryGameplayStateStore _gameplayStateStore = new();
    private readonly MapDataStore _mapDataStore;

    public MapDataStoreTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _mapDataStore = new MapDataStore(new GameDataLoader(), _gameplayStateStore);
    }

    [Fact]
    public async Task GetAllTeams_Should_Only_Include_Enabled_Teams()
    {
        // Arrange:
        var teams = await _mapDataStore.GetTeams();

        // Act:
        bool containsDisabledTeams = teams.Any(t => t.IsDisabled);

        // Assert
        Assert.False(containsDisabledTeams, "Only enabled teams should be returned");
        Assert.Equal(4, teams.Count);
    }

    [Fact]
    public async Task Mutations_Should_Be_Captured_In_The_Persisted_Gameplay_State()
    {
        // Arrange
        string districtName = await ApplyPersistedGameplayStateMutations();

        // Act
        var gameplayState = _gameplayStateStore.Read();

        // Assert
        Assert.NotNull(gameplayState);
        Assert.Equal(2, gameplayState!.CurrentRoundNumber);

        var bravoGameplayState = gameplayState.Teams.Single(t => t.Name == "Bravo");
        Assert.Equal(12, bravoGameplayState.Location.X);
        Assert.Equal(24, bravoGameplayState.Location.Y);
        Assert.True(bravoGameplayState.PlayerLoggedIn);
        Assert.Equal(1, bravoGameplayState.AdditionalResources.R1);
        Assert.Single(bravoGameplayState.Votes);
        Assert.Empty(bravoGameplayState.CastVotes);

        var districtGameplayState = gameplayState.Districts.Single(d => d.Name == districtName);
        Assert.Equal("Bravo", districtGameplayState.OwnerTeamName);
        Assert.False(districtGameplayState.IsClaimable);
    }

    [Fact]
    public async Task A_Fresh_Store_Should_Restore_The_Last_Persisted_Gameplay_State()
    {
        // Arrange
        string districtName = await ApplyPersistedGameplayStateMutations(includeExtraRoundAfterVote: true);

        int expectedScore = (await _mapDataStore.GetTeamScore("Bravo")).Amount;

        var restoredStore = new MapDataStore(new GameDataLoader(), _gameplayStateStore);

        // Act
        await restoredStore.InitializeAsync();

        // Assert
        var restoredDistrict = await restoredStore.GetDistrictOwner(districtName);
        var restoredBravo = await restoredStore.GetTeamByName("Bravo");
        int restoredScore = (await restoredStore.GetTeamScore("Bravo")).Amount;

        Assert.Equal("Bravo", restoredDistrict.TeamName);
        Assert.Equal(12, restoredBravo.Location.X);
        Assert.Equal(24, restoredBravo.Location.Y);
        Assert.True(restoredBravo.PlayerLoggedIn);
        Assert.Single(restoredBravo.Votes);
        Assert.Empty(restoredBravo.CastVotes);
        Assert.Equal(expectedScore, restoredScore);
    }

    private async Task<string> ApplyPersistedGameplayStateMutations(bool includeExtraRoundAfterVote = false)
    {
        string districtName = (await _mapDataStore.GetAllDistricts())[0].Name;

        await _mapDataStore.NextRound();
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districtName));
        await _mapDataStore.UpdateTeamPosition("Bravo", new Coordinate(12, 24));
        await _mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 1, R2 = 2, R3 = 3, R4 = 4 });
        await _mapDataStore.TryLoginTeamMember("br");
        await _mapDataStore.NextRound();
        await _mapDataStore.CastVoteFor("Bravo", "Delta");

        if (includeExtraRoundAfterVote)
        {
            await _mapDataStore.NextRound();
        }

        return districtName;
    }

    [Fact]
    public async Task A_Fresh_Store_Should_Reset_When_The_Game_Definition_Changes()
    {
        // Arrange
        string districtName = (await _mapDataStore.GetAllDistricts())[0].Name;

        await _mapDataStore.NextRound();
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districtName));
        await _mapDataStore.TryLoginTeamMember("br");
        var staleGameplayState = _gameplayStateStore.Read()!;
        _gameplayStateStore.Write(staleGameplayState with { GameDefinitionHash = "stale" });

        var restoredStore = new MapDataStore(new GameDataLoader(), _gameplayStateStore);

        // Act
        await restoredStore.InitializeAsync();

        // Assert
        var restoredDistrict = await restoredStore.GetDistrictOwner(districtName);
        var restoredBravo = await restoredStore.GetTeamByName("Bravo");

        Assert.Equal(DistrictOwner.Empty, restoredDistrict);
        Assert.False(restoredBravo.PlayerLoggedIn);
        Assert.Empty(restoredBravo.Votes);
        Assert.Empty(restoredBravo.CastVotes);
        Assert.Equal(0, restoredStore.GetCurrentRoundData().Index);
    }

    [Fact]
    public async Task ResetGame_Should_Clear_Gameplay_State_Without_Removing_Game_Definition()
    {
        // Arrange
        var initialDistrictNames = (await _mapDataStore.GetAllDistricts()).Select(d => d.Name).ToList();
        var initialTeamNames = (await _mapDataStore.GetTeams(includeDisabled: true)).Select(t => t.Name).ToList();
        var initialDistrictCount = (await _mapDataStore.GetAllDistricts()).Count;
        var initialTeamCount = (await _mapDataStore.GetTeams(includeDisabled: true)).Count;
        string districtName = (await _mapDataStore.GetAllDistricts())[0].Name;

        await _mapDataStore.NextRound();
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districtName));
        await _mapDataStore.TryLoginTeamMember("cr");
        await _mapDataStore.TryLoginTeamMember("br");

        // Act
        await _mapDataStore.ResetGame();

        // Assert
        var resetGameplayState = _gameplayStateStore.Read();
        var resetDistrict = await _mapDataStore.GetDistrictOwner(districtName);
        var resetBravo = await _mapDataStore.GetTeamByName("Bravo");
        var restoredStore = new MapDataStore(new GameDataLoader(), _gameplayStateStore);

        Assert.Equal(initialDistrictCount, (await _mapDataStore.GetAllDistricts()).Count);
        Assert.Equal(initialTeamCount, (await _mapDataStore.GetTeams(includeDisabled: true)).Count);
        Assert.Equal(initialDistrictNames, (await _mapDataStore.GetAllDistricts()).Select(d => d.Name).ToList());
        Assert.Equal(initialTeamNames, (await _mapDataStore.GetTeams(includeDisabled: true)).Select(t => t.Name).ToList());
        Assert.NotNull(resetGameplayState);
        Assert.Equal(DistrictOwner.Empty, resetDistrict);
        Assert.False(resetBravo.PlayerLoggedIn);
        Assert.False((await _mapDataStore.GetTeamByName("Charly")).PlayerLoggedIn);
        Assert.Empty(resetBravo.Votes);
        Assert.Empty(resetBravo.CastVotes);
        Assert.Equal(0, _mapDataStore.GetCurrentRoundData().Index);

        // A restarted store should see the reset gameplay state, not the stale match state.
        await restoredStore.InitializeAsync();

        Assert.Equal(initialDistrictCount, (await restoredStore.GetAllDistricts()).Count);
        Assert.Equal(initialTeamCount, (await restoredStore.GetTeams(includeDisabled: true)).Count);
        Assert.Equal(initialDistrictNames, (await restoredStore.GetAllDistricts()).Select(d => d.Name).ToList());
        Assert.Equal(initialTeamNames, (await restoredStore.GetTeams(includeDisabled: true)).Select(t => t.Name).ToList());
        Assert.Equal(DistrictOwner.Empty, await restoredStore.GetDistrictOwner(districtName));
        Assert.False((await restoredStore.GetTeamByName("Bravo")).PlayerLoggedIn);
        Assert.False((await restoredStore.GetTeamByName("Charly")).PlayerLoggedIn);
        Assert.Empty((await restoredStore.GetTeamByName("Bravo")).Votes);
        Assert.Empty((await restoredStore.GetTeamByName("Bravo")).CastVotes);
        Assert.Equal(0, restoredStore.GetCurrentRoundData().Index);
    }

    [Fact]
    public async Task IsClaimable_Should_Be_Reset_On_Start_Of_New_Gathering_Rounds()
    {
        // Arrange: Use MapDataStore to simulate the scenario
        await _mapDataStore.NextRound(); // start game -> start gathering resources

        // Act: Check if circles are enabled
        var districts = await _mapDataStore.GetAllDistricts();
        bool allDistrictsClaimableOnStart = districts.All(d => d.IsClaimable);
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districts[0].Name));

        await _mapDataStore.NextRound(); // progress to voting round
        int numberOfClaimedDistricts = districts.Count(d => !d.IsClaimable);

        await _mapDataStore.NextRound(); // progress to next gathering round
        districts = await _mapDataStore.GetAllDistricts();
        bool allDistrictsAreClaimableOnNextGatheringRound = districts.All(d => d.IsClaimable);

        // Assert:
        Assert.True(allDistrictsClaimableOnStart, "All districts should be claimable on start of gathering round.");
        Assert.Equal(1, numberOfClaimedDistricts);
        Assert.True(allDistrictsAreClaimableOnNextGatheringRound, "All districts should be claimable on next gathering round.");
    }

    [Fact]
    public async Task Claiming_Districts_Should_Only_Be_Allowed_Once_Per_Round()
    {
        // Arrange
        string districtName = (await _mapDataStore.GetAllDistricts())[0].Name;
        await _mapDataStore.NextRound(); // start game -> start gathering resources
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districtName));

        // Act: Try to claim the same district again
        bool claimResult = await _mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districtName));
        var owner = await _mapDataStore.GetDistrictOwner(districtName);

        // Assert: The claim should fail since the district is already owned by Bravo
        Assert.False(claimResult, "Claiming the same district twice should return false on second attempt.");
        Assert.Equal("Bravo", owner.TeamName);
    }

    [Fact]
    public async Task Claiming_Districts_Should_Only_Be_Allowed_On_Subsequent_Gathering_Rounds()
    {
        // Arrange
        string districtName = (await _mapDataStore.GetAllDistricts())[0].Name;

        await _mapDataStore.NextRound(); // start game -> start gathering resources
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districtName));
        await _mapDataStore.NextRound(); // progress to next (voting) round

        // Act 1: Try to claim the same district again in the next (voting) round
        bool claimResult = await _mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districtName));

        // Assert 1: The claim should fail since the district is already owned by Bravo
        Assert.False(claimResult, "Claiming districts should not be possible on voting rounds.");

        // Act 2: Try to claim the same district again in the next gathering round
        await _mapDataStore.NextRound(); // progress to next (gathering) round
        claimResult = await _mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districtName));

        // Assert 2: The claim should fail since the district is already owned by Bravo
        Assert.True(claimResult, "Claiming the same district should be possible after switching round.");
    }

    [Fact]
    public async Task Teams_Should_Not_Be_Allowed_To_Vote_More_Than_Once_Per_Voting_Round()
    {
        // Arrange: Use MapDataStore to simulate the scenario
        await _mapDataStore.NextRound(); // start game -> start gathering resources
        await _mapDataStore.NextRound(); // move to voting round
        await _mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 });

        // Act: Cast a vote for Bravo
        bool voteResult1 = await _mapDataStore.CastVoteFor("Delta", "Bravo"); // vote for Bravo
        bool voteResult2 = await _mapDataStore.CastVoteFor("Foxtrot", "Bravo"); // vote for Bravo
        
        await _mapDataStore.NextRound(); // exit voting round enforces score calculation
        await _mapDataStore.NextRound(); // enter new voting round

        bool voteResult3 = await _mapDataStore.CastVoteFor("Delta", "Bravo"); // vote for Bravo again

        // Assert
        Assert.True(voteResult1, "First vote from Bravo should succeed.");
        Assert.False(voteResult2, "Second vote from Bravo should fail, because they should not be able to vote again in the same round.");
        Assert.True(voteResult3, "Third vote from Bravo for Delta should now succeed, because we're in another voting round");
    }

    [Fact]
    public async Task When_Multiple_Teams_Receive_Equal_Amount_Of_Votes_All_Their_Voters_Should_Receive_Points()
    {
        // Arrange: Use MapDataStore to simulate the scenario
        await _mapDataStore.NextRound(); // start game -> start gathering resources

        // Act: Continue to voting round and simulate voting
        await _mapDataStore.NextRound();
        await _mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 }); // 14 + 2 
        await _mapDataStore.SetAdditionalTeamResource("Charly", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 }); // 14 + 2 
        await _mapDataStore.CastVoteFor("Delta", "Bravo"); // vote weight: 16
        await _mapDataStore.CastVoteFor("Echo", "Charly"); // vote weight: 16
        await _mapDataStore.NextRound(); // enforce score calculation

        // Assert: Both Bravo and Echo should have received equal bonus points

        int bravoScore = (await _mapDataStore.GetTeamScore("Bravo")).Amount;
        int charlyScore = (await _mapDataStore.GetTeamScore("Charly")).Amount;

        Assert.Equal(bravoScore, charlyScore);
    }

    [Fact]
    public async Task Score_Evolution_With_Gameplay_Actions_CustomScenario_MapDataStore()
    {
        // Arrange: Use MapDataStore to simulate the scenario
        var districts = await _mapDataStore.GetAllDistricts();
        await _mapDataStore.NextRound(); // start game -> start gathering resources

        // Act: Start the game simulation

        // Bravo and Charly gather resources from district 0 and 1 only
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districts[0].Name)); // total resources: 100 
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districts[1].Name)); // total resources: 100
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districts[2].Name)); // total resources: 100

        await _mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 }); // 14 + 2 

        // Voting round starts now
        await _mapDataStore.NextRound(); // move to voting -> starts voting process

        await _mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 }); // 14 + 2 

        // Apply additional resources, earned by completing tasks (R1 is significant resource this round)
        await _mapDataStore.CastVoteFor("Delta", "Bravo"); // vote weight: (100 + 10) + 16
        await _mapDataStore.CastVoteFor("Delta", "Charly"); // vote weight: (100 + 40 + 100 + 10)

        // Voting round finished
        await _mapDataStore.NextRound(); // exit voting round

        // Assert: Check scores for Bravo and Charly
        var scores = await _mapDataStore.GetAllTeamScores();
        int bravoScore = scores.FirstOrDefault(ts => ts.TeamName == "Bravo")?.Amount ?? 0;
        int charlyScore = scores.FirstOrDefault(ts => ts.TeamName == "Charly")?.Amount ?? 0;

        Assert.Equal(110 + 16 + 16 + 75, bravoScore); // 217
        Assert.Equal(140 + 110 + 75, charlyScore); // 325

        // Let's continue our simulation
        // Bravo claims district 1 that previously belonged to Charly
        await _mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districts[1].Name)); // Bravo now owns district 1

        await _mapDataStore.NextRound(); // calculate new scores
        await _mapDataStore.NextRound(); // scores are calculated after leaving a voting round

        bravoScore = (await _mapDataStore.GetTeamScore("Bravo")).Amount;
        charlyScore = (await _mapDataStore.GetTeamScore("Charly")).Amount;

        Assert.Equal(217 + 110 + 120, bravoScore);
        Assert.Equal(325 + 120, charlyScore);

        _testOutputHelper.WriteLine($"Bravo: {bravoScore}, Charly: {charlyScore}");
    }

    #region Implementation of IAsyncLifetime

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _mapDataStore.InitializeAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    #endregion
}
