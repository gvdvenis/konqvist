using System.Text.Json;
using System.Xml;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SharpKml.Dom;
using SharpKml.Engine;
using NtsLinearRing = NetTopologySuite.Geometries.LinearRing;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using SharpKmlPoint = SharpKml.Dom.Point;
using SharpKmlPolygon = SharpKml.Dom.Polygon;

namespace Konqvist.Admin.Features.Districts;

public sealed class DistrictImportAdminService(
    IDbContextFactory<KonqvistDbContext> dbContextFactory,
    HttpClient httpClient)
{
    private const int MaxNetworkLinkDepth = 5;
    private const string InvalidSourceUrlMessage = "Source URL must be an absolute HTTPS URL.";

    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<DistrictTemplateManagementSnapshot?> GetAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.GameTemplates
            .AsNoTracking()
            .Where(entity => entity.Id == templateId)
            .Select(entity => new DistrictTemplateManagementSnapshot(
                entity.Id,
                entity.Name,
                entity.Districts.Count,
                entity.DistrictImportSourceUrl))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UpdateDistrictImportSourceUrlResult> SaveSourceUrlAsync(
        int templateId,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return UpdateDistrictImportSourceUrlResult.TemplateNotFound();
        }

        if (!TryNormalizeSourceUrl(sourceUrl, out var normalizedSourceUrl, out var errorMessage))
        {
            return UpdateDistrictImportSourceUrlResult.InvalidSourceUrl(errorMessage);
        }

        template.DistrictImportSourceUrl = normalizedSourceUrl.AbsoluteUri;
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateDistrictImportSourceUrlResult.Updated(template.DistrictImportSourceUrl);
    }

    public async Task<UpdateDistrictImportSourceUrlResult> ClearSourceUrlAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return UpdateDistrictImportSourceUrlResult.TemplateNotFound();
        }

        template.DistrictImportSourceUrl = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return UpdateDistrictImportSourceUrlResult.Cleared();
    }

    public async Task<ImportDistrictTemplatesResult> RefreshFromSourceAsync(
        int templateId,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var templateSnapshot = await dbContext.GameTemplates
            .AsNoTracking()
            .Where(entity => entity.Id == templateId)
            .Select(entity => new
            {
                entity.Id,
                entity.DistrictImportSourceUrl
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (templateSnapshot is null)
        {
            return ImportDistrictTemplatesResult.TemplateNotFound();
        }

        var sourceUrlCandidate = string.IsNullOrWhiteSpace(sourceUrl)
            ? templateSnapshot.DistrictImportSourceUrl
            : sourceUrl;
        if (string.IsNullOrWhiteSpace(sourceUrlCandidate))
        {
            return ImportDistrictTemplatesResult.SourceUrlNotConfigured();
        }

        if (!TryNormalizeSourceUrl(sourceUrlCandidate, out var sourceUri, out var errorMessage))
        {
            return ImportDistrictTemplatesResult.InvalidSourceUrl(errorMessage);
        }

        return await ImportFromSourceUrlAsync(
            templateId,
            sourceUri,
            sourceUri,
            MaxNetworkLinkDepth,
            cancellationToken);
    }

    public async Task<DistrictUploadClassificationResult> ClassifyUploadAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName);
        if (!string.Equals(extension, ".kml", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".kmz", StringComparison.OrdinalIgnoreCase))
        {
            return DistrictUploadClassificationResult.InvalidFileType();
        }

        ParsedImportData parsedData;
        try
        {
            await using var bufferedStream = await BufferForParsingAsync(fileStream, cancellationToken);
            parsedData = ParseImportData(bufferedStream, ResolveImportDataFormat(extension));
        }
        catch (XmlException exception)
        {
            return DistrictUploadClassificationResult.InvalidFileContent(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return DistrictUploadClassificationResult.InvalidFileContent(exception.Message);
        }
        catch (IOException exception)
        {
            return DistrictUploadClassificationResult.InvalidFileContent(exception.Message);
        }
        catch (FormatException exception)
        {
            return DistrictUploadClassificationResult.InvalidFileContent(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return DistrictUploadClassificationResult.InvalidFileContent(exception.Message);
        }

        return parsedData.NetworkLinkHref is null
            ? DistrictUploadClassificationResult.DirectData()
            : DistrictUploadClassificationResult.NetworkLink(parsedData.NetworkLinkHref.OriginalString);
    }

    public async Task<ImportDistrictTemplatesResult> ImportAsync(
        int templateId,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName);
        if (!string.Equals(extension, ".kml", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".kmz", StringComparison.OrdinalIgnoreCase))
        {
            return ImportDistrictTemplatesResult.InvalidFileType();
        }

        ParsedImportData parsedData;
        try
        {
            await using var bufferedStream = await BufferForParsingAsync(fileStream, cancellationToken);
            parsedData = ParseImportData(bufferedStream, ResolveImportDataFormat(extension));
        }
        catch (XmlException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (IOException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (FormatException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }

        if (parsedData.NetworkLinkHref is not null)
        {
            if (!TryNormalizeSourceUrl(parsedData.NetworkLinkHref, out var sourceUri, out var errorMessage))
            {
                return ImportDistrictTemplatesResult.InvalidSourceUrl(errorMessage);
            }

            return await ImportFromSourceUrlAsync(
                templateId,
                sourceUri,
                sourceUri,
                MaxNetworkLinkDepth,
                cancellationToken);
        }

        return await ImportParsedDataAsync(templateId, parsedData, sourceUrlToPersist: null, cancellationToken);
    }

    private async Task<ImportDistrictTemplatesResult> ImportFromSourceUrlAsync(
        int templateId,
        Uri sourceUri,
        Uri sourceUrlToPersist,
        int remainingDepth,
        CancellationToken cancellationToken)
    {
        var downloadResult = await DownloadSourceContentAsync(sourceUri, cancellationToken);
        if (downloadResult.Status == DownloadSourceStatus.DownloadFailed)
        {
            return ImportDistrictTemplatesResult.SourceDownloadFailed(downloadResult.ErrorMessage);
        }

        if (downloadResult.Status == DownloadSourceStatus.EmptyContent)
        {
            return ImportDistrictTemplatesResult.SourceContentEmpty(downloadResult.ErrorMessage);
        }

        if (downloadResult.ContentBytes is null)
        {
            return ImportDistrictTemplatesResult.SourceDownloadFailed("Linked source download failed.");
        }

        ParsedImportData parsedData;
        try
        {
            await using var stream = new MemoryStream(downloadResult.ContentBytes, writable: false);
            var importDataFormat = ResolveImportDataFormat(
                sourceUri,
                downloadResult.MediaType,
                downloadResult.ContentBytes);
            parsedData = ParseImportData(stream, importDataFormat);
        }
        catch (XmlException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (IOException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (FormatException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return ImportDistrictTemplatesResult.InvalidFileContent(exception.Message);
        }

        if (parsedData.NetworkLinkHref is not null)
        {
            if (remainingDepth <= 0)
            {
                return ImportDistrictTemplatesResult.InvalidFileContent("NetworkLink depth limit exceeded.");
            }

            if (!TryNormalizeSourceUrl(parsedData.NetworkLinkHref, out var nestedSourceUri, out var errorMessage))
            {
                return ImportDistrictTemplatesResult.InvalidSourceUrl(errorMessage);
            }

            return await ImportFromSourceUrlAsync(
                templateId,
                nestedSourceUri,
                sourceUrlToPersist,
                remainingDepth - 1,
                cancellationToken);
        }

        return await ImportParsedDataAsync(
            templateId,
            parsedData,
            sourceUrlToPersist.AbsoluteUri,
            cancellationToken);
    }

    private async Task<ImportDistrictTemplatesResult> ImportParsedDataAsync(
        int templateId,
        ParsedImportData parsedData,
        string? sourceUrlToPersist,
        CancellationToken cancellationToken)
    {
        if (parsedData.Polygons.Count == 0)
        {
            return ImportDistrictTemplatesResult.NoDistrictPolygonsFound();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Districts)
            .FirstOrDefaultAsync(entity => entity.Id == templateId, cancellationToken);
        if (template is null)
        {
            return ImportDistrictTemplatesResult.TemplateNotFound();
        }

        var matchedTriggers = MatchTriggerPoints(parsedData.Polygons, parsedData.Points);
        var triggerCentersDerived = 0;
        var districtsToInsert = new List<DistrictTemplate>(parsedData.Polygons.Count);

        for (var index = 0; index < parsedData.Polygons.Count; index++)
        {
            var polygon = parsedData.Polygons[index];
            if (!matchedTriggers.TryGetValue(index, out var triggerCoordinate))
            {
                triggerCoordinate = ComputePolygonCenter(polygon.Geometry);
                triggerCentersDerived++;
            }

            districtsToInsert.Add(new DistrictTemplate
            {
                GameTemplateId = template.Id,
                Name = NormalizeDistrictName(polygon.Name, index + 1),
                GeoJson = polygon.GeoJson,
                TriggerLat = triggerCoordinate.Y,
                TriggerLng = triggerCoordinate.X,
                TriggerRadiusMeters = template.DistrictCaptureRadiusMeters,
                Gold = 0,
                Voters = 0,
                Likes = 0,
                Oil = 0
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.DistrictTemplates.RemoveRange(template.Districts);
        dbContext.DistrictTemplates.AddRange(districtsToInsert);
        if (sourceUrlToPersist is not null)
        {
            template.DistrictImportSourceUrl = sourceUrlToPersist;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ImportDistrictTemplatesResult.Imported(new DistrictImportSummary(
            DistrictsImported: districtsToInsert.Count,
            TriggerCirclesMatched: matchedTriggers.Count,
            TriggerCentersDerived: triggerCentersDerived));
    }

    private async Task<DownloadSourceContentResult> DownloadSourceContentAsync(
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                sourceUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return DownloadSourceContentResult.DownloadFailed(
                    $"Linked source download failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (contentBytes.Length == 0)
            {
                return DownloadSourceContentResult.EmptyContent("Linked source returned empty content.");
            }

            return DownloadSourceContentResult.Downloaded(
                contentBytes,
                response.Content.Headers.ContentType?.MediaType);
        }
        catch (HttpRequestException exception)
        {
            return DownloadSourceContentResult.DownloadFailed(exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return DownloadSourceContentResult.DownloadFailed(exception.Message);
        }
    }

    private static ImportDataFormat ResolveImportDataFormat(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".kml" => ImportDataFormat.Kml,
            ".kmz" => ImportDataFormat.Kmz,
            _ => ImportDataFormat.Auto
        };
    }

    private static ImportDataFormat ResolveImportDataFormat(
        Uri sourceUri,
        string? mediaType,
        byte[] contentBytes)
    {
        var extension = Path.GetExtension(sourceUri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return ResolveImportDataFormat(extension);
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            if (mediaType.Contains("kmz", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("zip", StringComparison.OrdinalIgnoreCase))
            {
                return ImportDataFormat.Kmz;
            }

            if (mediaType.Contains("kml", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                return ImportDataFormat.Kml;
            }
        }

        return LooksLikeZipArchive(contentBytes) ? ImportDataFormat.Kmz : ImportDataFormat.Kml;
    }

    private static bool LooksLikeZipArchive(byte[] contentBytes)
    {
        return contentBytes.Length >= 4
               && contentBytes[0] == 0x50
               && contentBytes[1] == 0x4B
               && contentBytes[2] == 0x03
               && contentBytes[3] == 0x04;
    }

    private static ParsedImportData ParseImportData(Stream stream, ImportDataFormat format)
    {
        KmlFile? kmlFile = format switch
        {
            ImportDataFormat.Kml => KmlFile.Load(stream),
            ImportDataFormat.Kmz => LoadKmlFromKmz(stream),
            _ => LoadKmlWithAutoFormat(stream)
        };
        if (kmlFile?.Root is null)
        {
            return new ParsedImportData([], [], null);
        }

        var networkLinkHref = EnumerateNetworkLinkHrefs(kmlFile.Root).FirstOrDefault();
        var polygons = new List<ParsedPolygon>();
        var points = new List<NtsPoint>();
        foreach (var placemark in EnumeratePlacemarks(kmlFile.Root))
        {
            foreach (var polygonGeometry in EnumeratePolygonGeometries(placemark.Geometry))
            {
                if (!TryCreatePolygon(polygonGeometry, out var polygon))
                {
                    continue;
                }

                polygons.Add(new ParsedPolygon(
                    placemark.Name ?? string.Empty,
                    polygon,
                    BuildGeoJsonPolygon(polygon)));
            }

            foreach (var pointGeometry in EnumeratePointGeometries(placemark.Geometry))
            {
                if (!TryCreatePoint(pointGeometry, out var point))
                {
                    continue;
                }

                points.Add(point);
            }
        }

        return new ParsedImportData(polygons, points, networkLinkHref);
    }

    private static KmlFile? LoadKmlWithAutoFormat(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return KmlFile.Load(stream);
        }

        stream.Position = 0;
        if (TryDetectZipArchive(stream))
        {
            stream.Position = 0;
            return LoadKmlFromKmz(stream);
        }

        stream.Position = 0;
        return KmlFile.Load(stream);
    }

    private static bool TryDetectZipArchive(Stream stream)
    {
        Span<byte> signature = stackalloc byte[4];
        var bytesRead = stream.Read(signature);
        return bytesRead == 4
               && signature[0] == 0x50
               && signature[1] == 0x4B
               && signature[2] == 0x03
               && signature[3] == 0x04;
    }

    private static async Task<MemoryStream> BufferForParsingAsync(Stream sourceStream, CancellationToken cancellationToken)
    {
        var bufferedStream = new MemoryStream();
        await sourceStream.CopyToAsync(bufferedStream, cancellationToken);
        bufferedStream.Position = 0;
        return bufferedStream;
    }

    private static KmlFile? LoadKmlFromKmz(Stream stream)
    {
        using var kmzFile = KmzFile.Open(stream);
        return kmzFile.GetDefaultKmlFile();
    }

    private static IEnumerable<Uri> EnumerateNetworkLinkHrefs(Element root)
    {
        foreach (var feature in EnumerateFeatures(root))
        {
            if (feature is NetworkLink networkLink && networkLink.Link?.Href is not null)
            {
                yield return networkLink.Link.Href;
            }
        }
    }

    private static IEnumerable<Placemark> EnumeratePlacemarks(Element root)
    {
        return EnumerateFeatures(root).OfType<Placemark>();
    }

    private static IEnumerable<Feature> EnumerateFeatures(Element root)
    {
        if (root is Kml kml && kml.Feature is not null)
        {
            foreach (var nestedFeature in EnumerateFeatures(kml.Feature))
            {
                yield return nestedFeature;
            }

            yield break;
        }

        if (root is not Feature feature)
        {
            yield break;
        }

        foreach (var nestedFeature in EnumerateFeatures(feature))
        {
            yield return nestedFeature;
        }
    }

    private static IEnumerable<Feature> EnumerateFeatures(Feature feature)
    {
        yield return feature;

        if (feature is not Container container)
        {
            yield break;
        }

        foreach (var nestedFeature in container.Features)
        {
            foreach (var descendant in EnumerateFeatures(nestedFeature))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<SharpKmlPolygon> EnumeratePolygonGeometries(SharpKml.Dom.Geometry? geometry)
    {
        if (geometry is null)
        {
            yield break;
        }

        if (geometry is SharpKmlPolygon polygon)
        {
            yield return polygon;
            yield break;
        }

        if (geometry is not MultipleGeometry multipleGeometry)
        {
            yield break;
        }

        foreach (var nestedGeometry in multipleGeometry.Geometry)
        {
            foreach (var nestedPolygon in EnumeratePolygonGeometries(nestedGeometry))
            {
                yield return nestedPolygon;
            }
        }
    }

    private static IEnumerable<SharpKmlPoint> EnumeratePointGeometries(SharpKml.Dom.Geometry? geometry)
    {
        if (geometry is null)
        {
            yield break;
        }

        if (geometry is SharpKmlPoint point)
        {
            yield return point;
            yield break;
        }

        if (geometry is not MultipleGeometry multipleGeometry)
        {
            yield break;
        }

        foreach (var nestedGeometry in multipleGeometry.Geometry)
        {
            foreach (var nestedPoint in EnumeratePointGeometries(nestedGeometry))
            {
                yield return nestedPoint;
            }
        }
    }

    private static bool TryCreatePoint(SharpKmlPoint point, out NtsPoint pointGeometry)
    {
        pointGeometry = null!;
        var coordinate = point.Coordinate;
        if (coordinate is null)
        {
            return false;
        }

        if (!IsFinite(coordinate.Longitude) || !IsFinite(coordinate.Latitude))
        {
            return false;
        }

        pointGeometry = GeometryFactory.CreatePoint(
            new Coordinate(coordinate.Longitude, coordinate.Latitude));
        return true;
    }

    private static bool TryCreatePolygon(SharpKmlPolygon polygon, out NtsPolygon polygonGeometry)
    {
        polygonGeometry = null!;
        var outerRing = polygon.OuterBoundary?.LinearRing;
        if (outerRing?.Coordinates is null || !TryCreateLinearRing(outerRing.Coordinates, out var shell))
        {
            return false;
        }

        var holes = new List<NtsLinearRing>();
        foreach (var innerBoundary in polygon.InnerBoundary)
        {
            var innerRing = innerBoundary.LinearRing;
            if (innerRing?.Coordinates is null)
            {
                continue;
            }

            if (!TryCreateLinearRing(innerRing.Coordinates, out var hole))
            {
                continue;
            }

            holes.Add(hole);
        }

        polygonGeometry = GeometryFactory.CreatePolygon(shell, [.. holes]);
        return !polygonGeometry.IsEmpty && polygonGeometry.IsValid;
    }

    private static bool TryCreateLinearRing(IEnumerable<SharpKml.Base.Vector> vectors, out NtsLinearRing linearRing)
    {
        linearRing = null!;
        var coordinates = vectors
            .Where(vector => IsFinite(vector.Longitude) && IsFinite(vector.Latitude))
            .Select(vector => new Coordinate(vector.Longitude, vector.Latitude))
            .ToList();
        if (coordinates.Count < 3)
        {
            return false;
        }

        if (!coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
        }

        if (coordinates.Count < 4)
        {
            return false;
        }

        linearRing = GeometryFactory.CreateLinearRing([.. coordinates]);
        return !linearRing.IsEmpty && linearRing.IsValid;
    }

    private static Dictionary<int, Coordinate> MatchTriggerPoints(
        IReadOnlyList<ParsedPolygon> polygons,
        IReadOnlyList<NtsPoint> points)
    {
        var matchedTriggers = new Dictionary<int, Coordinate>();
        foreach (var point in points)
        {
            var containingPolygonIndexes = polygons
                .Select((polygon, index) => (polygon, index))
                .Where(item => item.polygon.Geometry.Covers(point))
                .OrderBy(item => item.polygon.Geometry.Area)
                .ThenBy(item => item.index)
                .Select(item => item.index)
                .ToList();
            if (containingPolygonIndexes.Count == 0)
            {
                continue;
            }

            var chosenPolygonIndex = containingPolygonIndexes[0];
            if (matchedTriggers.ContainsKey(chosenPolygonIndex))
            {
                continue;
            }

            matchedTriggers[chosenPolygonIndex] = new Coordinate(point.X, point.Y);
        }

        return matchedTriggers;
    }

    private static Coordinate ComputePolygonCenter(NtsPolygon polygon)
    {
        var interiorPoint = polygon.InteriorPoint;
        if (interiorPoint is not null && !interiorPoint.IsEmpty && IsValidCoordinate(interiorPoint.Coordinate))
        {
            return interiorPoint.Coordinate;
        }

        var centroid = polygon.Centroid;
        if (centroid is not null && !centroid.IsEmpty && IsValidCoordinate(centroid.Coordinate))
        {
            return centroid.Coordinate;
        }

        return polygon.Coordinate;
    }

    private static bool IsValidCoordinate(Coordinate coordinate)
    {
        return IsFinite(coordinate.X) && IsFinite(coordinate.Y);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string NormalizeDistrictName(string? sourceName, int sequence)
    {
        var normalized = string.IsNullOrWhiteSpace(sourceName)
            ? $"District {sequence}"
            : sourceName.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static string BuildGeoJsonPolygon(NtsPolygon polygon)
    {
        var rings = new List<double[][]> { BuildRingCoordinates(polygon.ExteriorRing.Coordinates) };
        foreach (var hole in polygon.Holes)
        {
            rings.Add(BuildRingCoordinates(hole.Coordinates));
        }

        var geoJson = new GeoJsonPolygon("Polygon", [.. rings]);
        return JsonSerializer.Serialize(geoJson, DistrictGeoJsonSerializerContext.Default.GeoJsonPolygon);
    }

    private static double[][] BuildRingCoordinates(Coordinate[] ringCoordinates)
    {
        return ringCoordinates
            .Select(coordinate => new[] { coordinate.X, coordinate.Y })
            .ToArray();
    }

    private static bool TryNormalizeSourceUrl(
        string sourceUrl,
        out Uri normalizedSourceUrl,
        out string errorMessage)
    {
        normalizedSourceUrl = null!;
        errorMessage = InvalidSourceUrlMessage;
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return false;
        }

        return Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var parsedSourceUri)
               && TryNormalizeSourceUrl(parsedSourceUri, out normalizedSourceUrl, out errorMessage);
    }

    private static bool TryNormalizeSourceUrl(
        Uri sourceUrl,
        out Uri normalizedSourceUrl,
        out string errorMessage)
    {
        normalizedSourceUrl = null!;
        errorMessage = InvalidSourceUrlMessage;
        if (!sourceUrl.IsAbsoluteUri)
        {
            return false;
        }

        if (!string.Equals(sourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourceUrl.Host))
        {
            return false;
        }

        normalizedSourceUrl = sourceUrl;
        return true;
    }

    private sealed record ParsedImportData(
        IReadOnlyList<ParsedPolygon> Polygons,
        IReadOnlyList<NtsPoint> Points,
        Uri? NetworkLinkHref);

    private sealed record ParsedPolygon(
        string Name,
        NtsPolygon Geometry,
        string GeoJson);

    private enum ImportDataFormat
    {
        Kml,
        Kmz,
        Auto
    }

    private enum DownloadSourceStatus
    {
        Downloaded,
        DownloadFailed,
        EmptyContent
    }

    private sealed record DownloadSourceContentResult(
        DownloadSourceStatus Status,
        byte[]? ContentBytes = null,
        string? MediaType = null,
        string? ErrorMessage = null)
    {
        public static DownloadSourceContentResult Downloaded(byte[] contentBytes, string? mediaType) =>
            new(DownloadSourceStatus.Downloaded, contentBytes, mediaType);

        public static DownloadSourceContentResult DownloadFailed(string errorMessage) =>
            new(DownloadSourceStatus.DownloadFailed, null, null, errorMessage);

        public static DownloadSourceContentResult EmptyContent(string errorMessage) =>
            new(DownloadSourceStatus.EmptyContent, null, null, errorMessage);
    }
}
