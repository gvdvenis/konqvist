using Konqvist.Data.Contracts;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using OpenLayers.Blazor;
using Xunit.Abstractions;

namespace Konqvist.Data.Tests;

public class SnapshotDataStoreScoringTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void CreateSnapshot_SingleTeam_ScoreIsVoteWeight()
    {
        // Arrange
        var team = new TeamData("Alpha", "Red") { AdditionalResources = new ResourcesData { R1 = 2, R2 = 3, R3 = 0, R4 = 0 } };
        var teamResources = new List<TeamResources>
        {
            new(team, team.AdditionalResources, ResourcesData.Empty, "R1")
        };
        var mapData = new MapData { Coordinates = new List<Coordinate>(), Districts = [] };
        var roundData = new RoundData(1, "Voting 1", RoundKind.Voting, "R1");
        var votingData = new VotingData([new TeamVote(team.Name, 5)], [new Voter("Alpha", "Alpha")]);
        var store = new SnapshotDataStore();

        // Act
        store.CreateSnapshot(mapData, teamResources, roundData, votingData);
        var scores = store.GetAllTeamScores();

        // Assert
        Assert.Single(scores);
        Assert.Equal(team.Name, scores[0].TeamName);
        // Vote weight: AdditionalResources.Total + R1 = (2+3+0+0)+2 = 7, plus voting bonus 150 = 157
        Assert.Equal(157, scores[0].Score);
    }

    [Fact]
    public void CreateSnapshot_MultipleTeams_VotingBonusDistributed()
    {
        // Arrange
        var teamA = new TeamData("Alpha", "Red") { AdditionalResources = new ResourcesData { R1 = 1, R2 = 0, R3 = 0, R4 = 0 } };
        var teamB = new TeamData("Bravo", "Blue") { AdditionalResources = new ResourcesData { R1 = 2, R2 = 0, R3 = 0, R4 = 0 } };
        var teamResources = new List<TeamResources>
        {
            new(teamA, teamA.AdditionalResources, ResourcesData.Empty, "R1"),
            new(teamB, teamB.AdditionalResources, ResourcesData.Empty, "R1")
        };
        var mapData = new MapData { Coordinates = new List<Coordinate>(), Districts = [] };
        var roundData = new RoundData(1, "Voting 1", RoundKind.Voting, "R1");
        var votingData = new VotingData(
            [new TeamVote(teamA.Name, 5), new TeamVote(teamB.Name, 5)],
            [new Voter("Alpha", "Alpha"), new Voter("Bravo", "Bravo")]
        );
        var store = new SnapshotDataStore();

        // Act
        store.CreateSnapshot(mapData, teamResources, roundData, votingData);
        var scores = store.GetAllTeamScores();

        // Assert
        Assert.Equal(2, scores.Count);
        // Each team gets their vote weight plus half the bonus (150/2=75)
        // Alpha: (1+0+0+0)+1 = 2, plus 75 = 77
        // Bravo: (2+0+0+0)+2 = 4, plus 75 = 79
        Assert.Contains(scores, s => s.TeamName == "Alpha" && s.Score == 77);
        Assert.Contains(scores, s => s.TeamName == "Bravo" && s.Score == 79);
    }

    [Fact]
    public async Task Score_Evolution_Through_Rounds_Using_RealData()
    {
        // Arrange: Load real data from JSON files
        var mapData = await MapDataHelper.GetMapData();
        var teams = (await MapDataHelper.GetTeamsData()).Where(t => !t.IsDisabled).ToList();
        var rounds = new List<RoundData>
        {
            new(0, "Waiting for Game Start", RoundKind.NotStarted, null),
            new(1, "Running 1", RoundKind.GatherResources, nameof(ResourcesData.R1)),
            new(2, "Voting 1", RoundKind.Voting, nameof(ResourcesData.R1)),
            new(3, "Running 2", RoundKind.GatherResources, nameof(ResourcesData.R4)),
            new(4, "Voting 2", RoundKind.Voting, nameof(ResourcesData.R4)),
            new(5, "Running 3", RoundKind.GatherResources, nameof(ResourcesData.R2)),
            new(6, "Voting 3", RoundKind.Voting, nameof(ResourcesData.R2)),
            new(7, "Running 4", RoundKind.GatherResources, nameof(ResourcesData.R3)),
            new(8, "Voting 4", RoundKind.Voting, nameof(ResourcesData.R3)),
            new(9, "Game Over", RoundKind.GameOver, null)
        };

        var snapshotStore = new SnapshotDataStore();

        // Simulate the game: for each round, create a snapshot (no votes, no additional resources)
        foreach (var round in rounds)
        {
            var teamResources = teams.Select(t => new TeamResources(t, t.AdditionalResources, ResourcesData.Empty, round.ResourceOfInterest)).ToList();
            var votingData = new VotingData([], []);
            snapshotStore.CreateSnapshot(mapData, teamResources, round, votingData);
        }

        // Act: Get the scores after the last round
        var scores = snapshotStore.GetAllTeamScores();

        // Assert: There should be a score for each enabled team
        Assert.Equal(teams.Count, scores.Count);

        // Optionally, print scores for manual inspection
        foreach (var score in scores)
        {
            testOutputHelper.WriteLine($"{score.TeamName}: {score.Score}");
        }
    }

    [Fact]
    public async Task Score_Evolution_With_Gameplay_Actions()
    {
        // Arrange: Load real data from JSON files
        var mapData = await MapDataHelper.GetMapData();
        var teams = (await MapDataHelper.GetTeamsData()).Where(t => !t.IsDisabled).ToList();
        var rounds = new List<RoundData>
        {
            new(0, "Waiting for Game Start", RoundKind.NotStarted, null),
            new(1, "Running 1", RoundKind.GatherResources, nameof(ResourcesData.R1)),
            new(2, "Voting 1", RoundKind.Voting, nameof(ResourcesData.R1)),
            new(3, "Running 2", RoundKind.GatherResources, nameof(ResourcesData.R4)),
            new(4, "Voting 2", RoundKind.Voting, nameof(ResourcesData.R4)),
            new(5, "Running 3", RoundKind.GatherResources, nameof(ResourcesData.R2)),
            new(6, "Voting 3", RoundKind.Voting, nameof(ResourcesData.R2)),
            new(7, "Running 4", RoundKind.GatherResources, nameof(ResourcesData.R3)),
            new(8, "Voting 4", RoundKind.Voting, nameof(ResourcesData.R3)),
            new(9, "Game Over", RoundKind.GameOver, null)
        };

        var snapshotStore = new SnapshotDataStore();

        // Simulate the game: gameplay actions between rounds
        var teamVotes = new Dictionary<int, List<TeamVote>>
        {
            // Voting 1: Bravo gets all votes
            [2] = [new TeamVote("Bravo", 4)],
            // Voting 2: Delta gets all votes
            [4] = [new TeamVote("Delta", 4)],
            // Voting 3: Echo gets all votes
            [6] = [new TeamVote("Echo", 4)],
            // Voting 4: Charly gets all votes
            [8] = [new TeamVote("Charly", 4)]
        };

        // Give each team some resources in each running round
        foreach (var round in rounds)
        {
            if (round is { Kind: RoundKind.GatherResources, ResourceOfInterest: not null })
            {
                foreach (var team in teams)
                {
                    // Give each team 5 of the resource of interest
                    var res = new ResourcesData();
                    switch (round.ResourceOfInterest)
                    {
                        case nameof(ResourcesData.R1): res.R1 = 5; break;
                        case nameof(ResourcesData.R2): res.R2 = 5; break;
                        case nameof(ResourcesData.R3): res.R3 = 5; break;
                        case nameof(ResourcesData.R4): res.R4 = 5; break;
                    }
                    team.AdditionalResources += res;
                }
            }

            var teamResources = teams.Select(t => new TeamResources(t, t.AdditionalResources, ResourcesData.Empty, round.ResourceOfInterest)).ToList();
            var votingData = new VotingData(
                teamVotes.TryGetValue(round.Index, out var votes) ? votes : [],
                teams.Select(t => new Voter(t.Name, t.Name)).ToList()
            );
            snapshotStore.CreateSnapshot(mapData, teamResources, round, votingData);

            // After each voting round, clear additional resources (as in real game)
            if (round.Kind == RoundKind.Voting)
            {
                foreach (var team in teams)
                    team.AdditionalResources = ResourcesData.Empty;
            }
        }

        // Act: Get the scores after the last round
        var scores = snapshotStore.GetAllTeamScores();

        // Assert: There should be a score for each enabled team
        Assert.Equal(teams.Count, scores.Count);
        // Print scores for manual inspection
        foreach (var score in scores)
        {
            testOutputHelper.WriteLine($"{score.TeamName}: {score.Score}");
        }

        // Optionally, assert that the team with the most votes in the last round has the highest score
        int topScore = scores.Max(s => s.Score);
        Assert.Contains(scores, s => s.TeamName == "Charly" && s.Score == topScore);
    }

    [Fact]
    public async Task Score_Evolution_With_Gameplay_Actions_CustomScenario()
    {
        // Arrange: Load real data from JSON files
        var mapData = await MapDataHelper.GetMapData();
        var teams = (await MapDataHelper.GetTeamsData()).Where(t => !t.IsDisabled).ToList();
        var bravo = teams.First(t => t.Name == "Bravo");
        var charly = teams.First(t => t.Name == "Charly");
        var delta = teams.First(t => t.Name == "Delta");

        // 1. Gathering round: only Bravo and Charly get resources (all types)
        var gatheringRound = new RoundData(1, "Running 1", RoundKind.GatherResources, null);
        bravo.AdditionalResources = new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 };
        charly.AdditionalResources = new ResourcesData { R1 = 1, R2 = 1, R3 = 1, R4 = 1 };
        delta.AdditionalResources = ResourcesData.Empty;
        foreach (var t in teams.Where(t => t != bravo && t != charly))
            t.AdditionalResources = ResourcesData.Empty;

        var snapshotStore = new SnapshotDataStore();
        var teamResourcesGather = teams.Select(t => new TeamResources(t, t.AdditionalResources, ResourcesData.Empty, null)).ToList();
        var votingDataEmpty = new VotingData([], []);
        snapshotStore.CreateSnapshot(mapData, teamResourcesGather, gatheringRound, votingDataEmpty);

        // 2. Voting round: both Bravo and Charly vote for Delta
        var votingRound = new RoundData(2, "Voting 1", RoundKind.Voting, null);
        var votes = new List<TeamVote> { new TeamVote("Delta", 1), new TeamVote("Delta", 1) };
        var voters = new List<Voter> { new Voter("Bravo", "Bravo"), new Voter("Charly", "Charly") };
        var teamResourcesVote = teams.Select(t => new TeamResources(t, t.AdditionalResources, ResourcesData.Empty, null)).ToList();
        var votingData = new VotingData(votes, voters);
        snapshotStore.CreateSnapshot(mapData, teamResourcesVote, votingRound, votingData);

        // 3. Act: Next round (simulate clearing resources as in real game)
        foreach (var t in teams) t.AdditionalResources = ResourcesData.Empty;

        // 4. Assert: Check scores for Bravo and Charly
        var scores = snapshotStore.GetAllTeamScores();
        var bravoScore = scores.FirstOrDefault(s => s.TeamName == "Bravo")?.Score ?? 0;
        var charlyScore = scores.FirstOrDefault(s => s.TeamName == "Charly")?.Score ?? 0;

        // Bravo: 2+3+4+5 = 14 (gathering), Charly: 1+1+1+1 = 4 (gathering)
        // No voting bonus for Bravo/Charly, since all votes go to Delta
        Assert.Equal(14, bravoScore);
        Assert.Equal(4, charlyScore);
        testOutputHelper.WriteLine($"Bravo: {bravoScore}, Charly: {charlyScore}");
    }

    [Fact]
    public async Task Score_Evolution_With_Gameplay_Actions_CustomScenario_MapDataStore()
    {
        // Arrange: Use MapDataStore to simulate the scenario
        var mapDataStore = await MapDataStore.GetInstanceAsync();

        await mapDataStore.NextRound(); // start game -> start gathering resources

        // Set up the teams in the store
        // (simulate what InitializeAsync would do)
        // Set resources for Bravo and Charly only
        var districts = await mapDataStore.GetAllDistricts();
        await mapDataStore.SetDistrictOwner(new DistrictOwner("Bravo", districts[0].Name));
        await mapDataStore.SetDistrictOwner(new DistrictOwner("Charly", districts[1].Name));

        //await mapDataStore.SetAdditionalTeamResource("Bravo", new ResourcesData { R1 = 2, R2 = 3, R3 = 4, R4 = 5 }); //14
        //await mapDataStore.SetAdditionalTeamResource("Charly", new ResourcesData { R1 = 1, R2 = 1, R3 = 1, R4 = 1 }); //5

        // 3. Act: Next round (should snapshot and clear resources)
        await mapDataStore.NextRound(); // move to voting round

        // 1. Gathering round: nothing else to do, resources are set
        int voteWeightBravo = await mapDataStore.GetVoteWeightForTeam("Bravo"); // 110
        int voteWeightCharly = await mapDataStore.GetVoteWeightForTeam("Charly"); // 140

        // 2. Voting round: both Bravo and Charly vote for Delta
        await mapDataStore.AddVoteForCurrentRound("Delta", "Bravo", voteWeightBravo);
        await mapDataStore.AddVoteForCurrentRound("Delta", "Charly", voteWeightCharly);

        await mapDataStore.NextRound(); // move to next running round (triggers snapshot of voting)

        // 4. Assert: Check scores for Bravo and Charly
        var scores = (await mapDataStore.GetTeamScores()).ToList();
        var bravoScore = scores.FirstOrDefault(s => s.TeamName == "Bravo")?.Score ?? 0;
        var charlyScore = scores.FirstOrDefault(s => s.TeamName == "Charly")?.Score ?? 0;

        // Bravo: 2+3+4+5 = 14 (gathering), Charly: 1+1+1+1 = 4 (gathering)
        // No voting bonus for Bravo/Charly, since all votes go to Delta
        Assert.Equal(110+75, bravoScore);
        Assert.Equal(140+75, charlyScore);
        testOutputHelper.WriteLine($"Bravo: {bravoScore}, Charly: {charlyScore}");
    }
}
