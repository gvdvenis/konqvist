using System.Text.Json;
using Konqvist.Data.Models;

namespace Konqvist.Data.Tests;

internal static class MockData
{
    public static string MapData = 
        """
    {
     "coordinates": [
       [
         6.2797001,
         51.8743213
       ],
       [
         6.2816743,
         51.8737648
       ],
       [
         6.2797001,
         51.8743213
       ]
     ],
     "districts": [
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2417494,
           51.8789577
         ],
         "owner": null,
         "name": "Pann\u00E5k\u00F6i",
         "resources": {
           "r1": 10,
           "r2": 40,
           "r3": 30,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2473606,
           51.8820804
         ],
         "owner": null,
         "name": "M\u00F6lenBarg",
         "resources": {
           "r1": 40,
           "r2": 30,
           "r3": 20,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [],
         "triggerCircleCenter": [
           6.2400855,
           51.8738511
         ],
         "owner": null,
         "name": "Jykkel",
         "resources": {
           "r1": 10,
           "r2": 30,
           "r3": 40,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2568063,
           51.8740193
         ],
         "owner": null,
         "name": "Sacka",
         "resources": {
           "r1": 20,
           "r2": 30,
           "r3": 40,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2582606,
           51.8781864
         ],
         "owner": null,
         "name": "B\u00E5lla",
         "resources": {
           "r1": 40,
           "r2": 20,
           "r3": 10,
           "r4": 30
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.249897,
           51.8751957
         ],
         "owner": null,
         "name": "Sh\u00E4fte",
         "resources": {
           "r1": 40,
           "r2": 30,
           "r3": 10,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2522925,
           51.8833491
         ],
         "owner": null,
         "name": "TroiH\u00FCllen",
         "resources": {
           "r1": 40,
           "r2": 10,
           "r3": 30,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2573508,
           51.8811673
         ],
         "owner": null,
         "name": "Sol\u00E4ng",
         "resources": {
           "r1": 10,
           "r2": 20,
           "r3": 30,
           "r4": 40
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2603364,
           51.8810647
         ],
         "owner": null,
         "name": "Rodingsvik",
         "resources": {
           "r1": 40,
           "r2": 20,
           "r3": 10,
           "r4": 30
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2680171,
           51.8797671
         ],
         "owner": null,
         "name": "Friidrottsbana",
         "resources": {
           "r1": 20,
           "r2": 30,
           "r3": 40,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2658664,
           51.8806515
         ],
         "owner": null,
         "name": "Fisk butik J\u00F6rgen\n",
         "resources": {
           "r1": 30,
           "r2": 20,
           "r3": 10,
           "r4": 40
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2662756,
           51.8859703
         ],
         "owner": null,
         "name": "L\u00E4ngels\u00F6n",
         "resources": {
           "r1": 20,
           "r2": 30,
           "r3": 40,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2481156,
           51.8721039
         ],
         "owner": null,
         "name": "Sh\u00E4m\u00E4g\u00E5rd",
         "resources": {
           "r1": 30,
           "r2": 20,
           "r3": 40,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2603564,
           51.8793723
         ],
         "owner": null,
         "name": "Puck \u0026 kompani",
         "resources": {
           "r1": 10,
           "r2": 40,
           "r3": 30,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2484269,
           51.8784681
         ],
         "owner": null,
         "name": "Lit\u00F6rna",
         "resources": {
           "r1": 10,
           "r2": 20,
           "r3": 40,
           "r4": 30
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2493009,
           51.8707532
         ],
         "owner": null,
         "name": "Vitsj\u00F6",
         "resources": {
           "r1": 10,
           "r2": 40,
           "r3": 30,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2571834,
           51.8714501
         ],
         "owner": null,
         "name": "Isg\u00E5rd",
         "resources": {
           "r1": 30,
           "r2": 40,
           "r3": 20,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2626543,
           51.8696828
         ],
         "owner": null,
         "name": "Dalah\u00E4st",
         "resources": {
           "r1": 10,
           "r2": 20,
           "r3": 40,
           "r4": 30
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.2644288,
           51.8762589
         ],
         "owner": null,
         "name": "K\u00E4ti\u00E4",
         "resources": {
           "r1": 40,
           "r2": 30,
           "r3": 10,
           "r4": 20
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.274991,
           51.8782126
         ],
         "owner": null,
         "name": "Nattstj\u00E4rna",
         "resources": {
           "r1": 40,
           "r2": 30,
           "r3": 20,
           "r4": 10
         },
         "isClaimable": true
       },
       {
         "coordinates": [
           [
             6.2797001,
             51.8743213
           ],
           [
             6.2816743,
             51.8737648
           ],
           [
             6.2797001,
             51.8743213
           ]
         ],
         "triggerCircleCenter": [
           6.27621,
           51.8760417
         ],
         "owner": null,
         "name": "T\u00E4nka",
         "resources": {
           "r1": 10,
           "r2": 40,
           "r3": 30,
           "r4": 20
         },
         "isClaimable": true
       }
     ]
    }
    """;

    public static string TeamsData =
        """
        [
          {
            "name": "Alpha",
            "color": "#880000", // red
            "isDisabled": true
          },
          {
            "name": "Bravo",
            "color": "#000088" // blue
          },
          {
            "name": "Charly",
            "color": "#008000" // green
          },
          {
            "name": "Delta",
            "color": "#888800" // yellow
          },
          {
            "name": "Echo",
            "color": "#800080" // purple
          },
          {
            "name": "Foxtrot",
            "color": "#FFA500", // orange
            "isDisabled": true
          },
          {
            "name": "Golf",
            "color": "#FFC0CB", // pink
            "isDisabled": true
          },
          {
            "name": "Hotel",
            "color": "#A52A2A", // brown
            "isDisabled": true
          },
          {
            "name": "India",
            "color": "#000000", // black
            "isDisabled": true
          }
        ]
        """;
}

internal class GameDataLoader : IMapDataLoader
{
    #region Implementation of IMapDataLoader

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new CoordinateConverter(), new CoordinateArrayConverter() }
    };

    /// <inheritdoc />
    public Task<MapData> GetMapData()
    {

        var map = JsonSerializer.Deserialize<MapData>(MockData.MapData, Options);
        return Task.FromResult(map ?? MapData.Empty);
    }

    /// <inheritdoc />
    public Task<TeamData[]> GetTeamsData()
    {
        var teams = JsonSerializer.Deserialize<TeamData[]>(MockData.TeamsData, Options);
        return Task.FromResult(teams ?? []);
    }

    /// <inheritdoc />
    public Task<List<RoundData>> GetRoundsData()
    {
        return Task.FromResult<List<RoundData>>(
        [
            new RoundData(0, "Waiting for Game Start", RoundKind.NotStarted,null),
            new RoundData(1, "Running 1", RoundKind.GatherResources, nameof(ResourcesData.R1)),
            new RoundData(2, "Voting 1", RoundKind.Voting,nameof(ResourcesData.R1)),
            new RoundData(3, "Running 2", RoundKind.GatherResources, nameof(ResourcesData.R4)),
            new RoundData(4, "Voting 2", RoundKind.Voting, nameof(ResourcesData.R4)),
            new RoundData(5, "Running 3", RoundKind.GatherResources, nameof(ResourcesData.R2)),
            new RoundData(6, "Voting 3", RoundKind.Voting, nameof(ResourcesData.R2)),
            new RoundData(7, "Running 4", RoundKind.GatherResources, nameof(ResourcesData.R3)),
            new RoundData(8, "Voting 4", RoundKind.Voting, nameof(ResourcesData.R3)),
            new RoundData(9, "Game Over", RoundKind.GameOver, null)
        ]);
    }

    #endregion
}