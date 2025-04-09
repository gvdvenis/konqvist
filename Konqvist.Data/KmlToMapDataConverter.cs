using Konqvist.Data.Models;
using OpenLayers.Blazor;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Konqvist.Data;

public class KmlToMapDataConverter
{
    /// <summary>
    ///     Converts a KML file to MapData.
    /// </summary>
    /// <param name="kmlFilePath"></param>
    /// <returns></returns>
    public static MapData ConvertKmlToMapData(string kmlFilePath)
    {
        var mapData = MapData.Empty;

        try
        {
            var kml = XDocument.Load(kmlFilePath);
            XNamespace ns = "http://www.opengis.net/kml/2.2";

            // Parse 'Speelgebied' folder for map coordinates
            var speelgebiedFolder = kml.Descendants(ns + "Folder")
                .FirstOrDefault(folder => string.Equals(folder.Element(ns + "name")?.Value, "Speelgebied", StringComparison.OrdinalIgnoreCase));

            if (speelgebiedFolder != null)
            {
                var coordinates = speelgebiedFolder
                    .Descendants(ns + "coordinates")
                    .FirstOrDefault()?.Value
                    .Trim()
                    .Split(' ')
                    .Where(coord => !string.IsNullOrWhiteSpace(coord)) // Filter out empty strings
                    .Select(coord => ParseCoordinate(coord))
                    .ToList();

                if (coordinates != null)
                {
                    mapData.Coordinates = coordinates;
                }
            }

            // Parse 'Districten' folder for district data
            var districtenFolder = kml.Descendants(ns + "Folder")
                .FirstOrDefault(folder => string.Equals(folder.Element(ns + "name")?.Value, "Districten", StringComparison.OrdinalIgnoreCase));

            if (districtenFolder != null)
            {
                var districtPlacemarks = districtenFolder.Descendants(ns + "Placemark");
                foreach (var placemark in districtPlacemarks)
                {
                    var name = placemark.Element(ns + "name")?.Value;
                    var polygon = placemark.Element(ns + "Polygon");

                    if (polygon != null)
                    {
                        var coordinates = polygon
                            .Descendants(ns + "coordinates")
                            .FirstOrDefault()?.Value
                            .Trim()
                            .Split(' ')
                            .Where(coord => !string.IsNullOrWhiteSpace(coord)) // Filter out empty strings
                            .Select(coord => ParseCoordinate(coord))
                            .ToList();

                        if (coordinates != null)
                        {
                            mapData.Districts.Add(new DistrictData
                            {
                                Name = name ?? "Unnamed District",
                                Coordinates = coordinates,
                                IsClaimable = true, // Default value, adjust as needed
                                Resources = GenerateRandomResources() // Assign randomized resources
                            });
                        }
                    }
                }
            }

            // Parse 'TriggerCirkels' folder for trigger circle data
            var triggerCirkelsFolder = kml.Descendants(ns + "Folder")
                .FirstOrDefault(folder => string.Equals(folder.Element(ns + "name")?.Value, "TriggerCirkels", StringComparison.OrdinalIgnoreCase));

            if (triggerCirkelsFolder != null)
            {
                var triggerPlacemarks = triggerCirkelsFolder.Descendants(ns + "Placemark");
                foreach (var placemark in triggerPlacemarks)
                {
                    var name = placemark.Element(ns + "name")?.Value;
                    var point = placemark.Element(ns + "Point");

                    if (point != null)
                    {
                        var coordinate = point
                            .Element(ns + "coordinates")?.Value
                            .Trim();

                        if (!string.IsNullOrWhiteSpace(coordinate)) // Check for empty or null coordinate
                        {
                            var triggerCircleCenter = ParseCoordinate(coordinate);

                            // Match trigger circle to district by name (case-insensitive)
                            var matchingDistrict = mapData.Districts
                                .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

                            if (matchingDistrict != null)
                            {
                                matchingDistrict.TriggerCircleCenter = triggerCircleCenter;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Handle errors (e.g., log them)
            Console.WriteLine($"Error parsing KML file: {ex.Message}");
        }

        return mapData;
    }

    private static Coordinate ParseCoordinate(string coordinateString)
    {
        if (string.IsNullOrWhiteSpace(coordinateString))
        {
            throw new FormatException("Coordinate string is null or empty.");
        }

        var parts = coordinateString.Split(',');
        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid coordinate format: {coordinateString}");
        }

        return new Coordinate(
            double.Parse(parts[0], CultureInfo.InvariantCulture), // Longitude (X)
            double.Parse(parts[1], CultureInfo.InvariantCulture)  // Latitude (Y)
        );
    }

    private static ResourcesData GenerateRandomResources()
    {
        var random = new Random();
        var values = new List<int> { 10, 20, 30, 40 };
        var shuffledValues = values.OrderBy(_ => random.Next()).ToList();

        return new ResourcesData
        {
            R1 = shuffledValues[0],
            R2 = shuffledValues[1],
            R3 = shuffledValues[2],
            R4 = shuffledValues[3]
        };
    }

    /// <summary>
    ///     Runs the KML to MapData conversion and saves the result as a JSON file.
    /// </summary>
    /// <param name="kmlFilePath">The path to the KML file.</param>
    /// <param name="jsonFilePath">The path to the output JSON file.</param>
    public static void Run(string kmlFilePath, string jsonFilePath)
    {
        // Convert KML to MapData
        var mapData = ConvertKmlToMapData(kmlFilePath);

        // Save MapData to JSON file
        SaveToJsonFile(mapData, jsonFilePath);
    }

    public static void SaveToJsonFile(MapData mapData, string jsonFilePath)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string jsonString = JsonSerializer.Serialize(mapData, options);
        File.WriteAllText(jsonFilePath, jsonString);
    }
}
