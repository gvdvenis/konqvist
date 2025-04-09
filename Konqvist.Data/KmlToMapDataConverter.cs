using Konqvist.Data.Models;
using OpenLayers.Blazor;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace Konqvist.Data;

public class KmlToMapDataConverter
{
    private const string _mapLayerName = "Speelgebied";
    private const string _triggerCircleLayerName = "TriggerCirkels";
    private const string _districtsLayerName = "Districten";
    private static readonly XNamespace _ns = "http://www.opengis.net/kml/2.2";
    private static XDocument _kml = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

    /// <summary>
    ///    Saves the MapData object to a JSON file.
    /// </summary>
    /// <param name="mapData"></param>
    /// <param name="jsonFilePath"></param>
    public static void SaveToJsonFile(MapData mapData, string jsonFilePath)
    {
        string jsonString = JsonSerializer.Serialize(mapData, _jsonOptions);
        File.WriteAllText(jsonFilePath, jsonString);
    }

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
            _kml = XDocument.Load(kmlFilePath);

            // Parse 'Speelgebied' element for map coordinates
            mapData.Coordinates = ExtractCoordinatesFromLayer(_mapLayerName);

            // Parse 'Districten' element for district data and add to map data
            ParseFolderContent(_districtsLayerName, "Polygon", (element, placemark) =>
            {
                mapData.Districts.Add(new DistrictData
                {
                    Name = placemark.Element(_ns + "name")?.Value ?? "Unnamed District",
                    Coordinates = ExtractCoordinatesFromElement(element),
                    IsClaimable = true, // Default value, adjust as needed
                    Resources = GenerateRandomResources() // Assign randomized resources
                });
            });

            // Parse 'TriggerCircles' elements for trigger circle coordinates and assign to equally named districts
            ParseFolderContent(_triggerCircleLayerName, "Point", (element, placemark) =>
            {
                if (mapData
                    .Districts
                    .FirstOrDefault(district => string.Equals(
                        placemark.Element(_ns + "name")?.Value,
                        district.Name,
                        StringComparison.OrdinalIgnoreCase)) is not { } matchingDistrict)
                    return;

                if (element
                    .Element(_ns + "coordinates")?.Value
                    .Trim() is not { } coordinate)
                    return;

                matchingDistrict.TriggerCircleCenter = ParseCoordinate(coordinate);
            });

            return mapData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing KML file: {ex.Message}");
        }

        return mapData;
    }

    private static void ParseFolderContent(
        string layerName,
        string elementName,
        Action<XElement, XElement> elementParser)
    {
        if (GetFolderElementByLayerName(layerName) is XElement elementFolder)
        {
            foreach (var placemark in elementFolder.Descendants(_ns + "Placemark"))
            {
                if (placemark.Element(_ns + elementName) is not XElement element) continue;

                elementParser(element, placemark);
            }
        }
    }

    private static List<Coordinate> ExtractCoordinatesFromLayer(string layerName)
    {
        XElement? folderElement = GetFolderElementByLayerName(layerName);
        return ExtractCoordinatesFromElement(folderElement);
    }

    private static List<Coordinate> ExtractCoordinatesFromElement(XElement? folderElement)
    {
        return folderElement?
            .Descendants(_ns + "coordinates")
            .FirstOrDefault()?.Value
            .Trim()
            .Split(' ')
            .Where(coord => !string.IsNullOrWhiteSpace(coord)) // Filter out empty strings
            .Select(ParseCoordinate)
            .ToList() ?? [];
    }

    private static XElement? GetFolderElementByLayerName(string layerName)
    {
        return _kml
            .Descendants(_ns + "Folder")
            .FirstOrDefault(xElement => string.Equals(
                xElement.Element(_ns + "name")?.Value,
                layerName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static Coordinate ParseCoordinate(string coordinateString)
    {
        if (string.IsNullOrWhiteSpace(coordinateString))
            throw new FormatException("Coordinate string is null or empty.");

        var parts = coordinateString.Split(',');
        return parts.Length < 2
            ? throw new FormatException($"Invalid coordinate format: {coordinateString}")
            : new Coordinate(
                double.Parse(parts[0], CultureInfo.InvariantCulture), // Longitude (X)
                double.Parse(parts[1], CultureInfo.InvariantCulture)  // Latitude (Y)
        );
    }

    private static ResourcesData GenerateRandomResources()
    {
        var random = new Random();
        var values = new[] { 10, 20, 30, 40 }; // Use new collection initializer syntax
        var shuffledValues = values.OrderBy(_ => random.Next()).ToArray();

        return new ResourcesData
        {
            R1 = shuffledValues[0],
            R2 = shuffledValues[1],
            R3 = shuffledValues[2],
            R4 = shuffledValues[3]
        };
    }
}
