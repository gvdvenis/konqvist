using Konqvist.Data.Contracts;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using Xunit.Abstractions;

namespace Konqvist.Data.Tests;

public class MapDataStoreTests(ITestOutputHelper testOutputHelper)
{
    private readonly MapDataStore _mapDataStore = MapDataStore.GetInstanceAsync().GetAwaiter().GetResult();

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
        //TODO: implement this
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
        
        testOutputHelper.WriteLine($"Bravo: {bravoScore}, Charly: {charlyScore}");
    }
}
