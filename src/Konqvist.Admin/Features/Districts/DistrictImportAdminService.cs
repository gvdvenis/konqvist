using System.Text.Json;
using System.Xml;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using SharpKml.Dom;
using SharpKml.Engine;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
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
                entity.DistrictImportSourceUrl,
                entity.MapOutlineGeoJson))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DistrictPreviewItem>> GetDistrictPreviewAsync(
        int templateId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.DistrictTemplates
            .AsNoTracking()
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .Select(entity => new DistrictPreviewItem(
                entity.Id,
                entity.Name,
                entity.Gold,
                entity.Voters,
                entity.Likes,
                entity.Oil,
                entity.TriggerLat,
                entity.TriggerLng,
                entity.TriggerRadiusMeters,
                entity.GeoJson))
            .ToListAsync(cancellationToken);
    }

    public async Task<SaveDistrictResourcesResult> UpdateDistrictResourcesAsync(
        int templateId,
        int districtId,
        DistrictResourceEditorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsValidResourceInput(input))
        {
            return SaveDistrictResourcesResult.InvalidInput;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var templateExists = await dbContext.GameTemplates
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == templateId, cancellationToken);
        if (!templateExists)
        {
            return SaveDistrictResourcesResult.TemplateNotFound;
        }

        var district = await dbContext.DistrictTemplates
            .FirstOrDefaultAsync(
                entity => entity.GameTemplateId == templateId && entity.Id == districtId,
                cancellationToken);
        if (district is null)
        {
            return SaveDistrictResourcesResult.DistrictNotFound;
        }

        district.Gold = input.Gold;
        district.Voters = input.Voters;
        district.Likes = input.Likes;
        district.Oil = input.Oil;
        district.TriggerRadiusMeters = input.TriggerRadiusMeters;
        await dbContext.SaveChangesAsync(cancellationToken);
        return SaveDistrictResourcesResult.Saved;
    }

    public async Task<RandomizeDistrictResourcesResult> RandomizeDistrictResourcesAsync(
        int templateId,
        int minValue,
        int maxValue,
        CancellationToken cancellationToken = default)
    {
        if (minValue < 0 || maxValue < 0 || minValue > maxValue)
        {
            return RandomizeDistrictResourcesResult.InvalidRange();
        }

        var minTen = RoundUpToNearestTen(minValue);
        var maxTen = RoundDownToNearestTen(maxValue);
        if (minTen > maxTen)
        {
            return RandomizeDistrictResourcesResult.InvalidRange();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var templateExists = await dbContext.GameTemplates
            .AsNoTracking()
            .AnyAsync(entity => entity.Id == templateId, cancellationToken);
        if (!templateExists)
        {
            return RandomizeDistrictResourcesResult.TemplateNotFound();
        }

        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync(cancellationToken);
        foreach (var district in districts)
        {
            district.Gold = NextTenInclusive(minTen, maxTen);
            district.Voters = NextTenInclusive(minTen, maxTen);
            district.Likes = NextTenInclusive(minTen, maxTen);
            district.Oil = NextTenInclusive(minTen, maxTen);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return RandomizeDistrictResourcesResult.Randomized(districts.Count);
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

    private static bool IsValidResourceInput(DistrictResourceEditorInput input)
    {
        return input.Gold >= 0
               && input.Voters >= 0
               && input.Likes >= 0
               && input.Oil >= 0
               && double.IsFinite(input.TriggerRadiusMeters)
               && input.TriggerRadiusMeters > 0;
    }

    private static int NextTenInclusive(int minValue, int maxValue)
    {
        var steps = ((long)maxValue - minValue) / 10L + 1L;
        if (steps <= 1)
        {
            return minValue;
        }

        var randomStep = Random.Shared.NextInt64(steps);
        return checked(minValue + (int)(randomStep * 10L));
    }

    private static int RoundUpToNearestTen(int value)
    {
        if (value % 10 == 0)
        {
            return value;
        }

        return checked(value + (10 - (value % 10)));
    }

    private static int RoundDownToNearestTen(int value)
    {
        return value - (value % 10);
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
        var mapOutlinePolygonIndex = TryGetMapOutlinePolygonIndex(parsedData.Polygons, parsedData.Points, matchedTriggers);
        var mapOutlineStatus = DistrictMapOutlineStatus.NotAvailable;
        string? mapOutlineGeoJson;
        if (mapOutlinePolygonIndex is int outlineIndex)
        {
            mapOutlineGeoJson = parsedData.Polygons[outlineIndex].GeoJson;
            mapOutlineStatus = DistrictMapOutlineStatus.Imported;
        }
        else if (TryBuildFallbackMapOutlineGeoJson(parsedData.Polygons, out var generatedMapOutlineGeoJson))
        {
            mapOutlineGeoJson = generatedMapOutlineGeoJson;
            mapOutlineStatus = DistrictMapOutlineStatus.GeneratedFallback;
        }
        else
        {
            mapOutlineGeoJson = null;
        }
        var triggerCentersDerived = 0;
        var triggerCirclesMatched = 0;
        var districtsToInsert = new List<DistrictTemplate>(parsedData.Polygons.Count);

        for (var index = 0; index < parsedData.Polygons.Count; index++)
        {
            if (mapOutlinePolygonIndex is int mapIndex && index == mapIndex)
            {
                continue;
            }

            var polygon = parsedData.Polygons[index];
            if (!matchedTriggers.TryGetValue(index, out var triggerCoordinate))
            {
                triggerCoordinate = ComputePolygonCenter(polygon.Geometry);
                triggerCentersDerived++;
            }
            else
            {
                triggerCirclesMatched++;
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
        template.MapOutlineGeoJson = mapOutlineGeoJson;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ImportDistrictTemplatesResult.Imported(new DistrictImportSummary(
            DistrictsImported: districtsToInsert.Count,
            TriggerCirclesMatched: triggerCirclesMatched,
            TriggerCentersDerived: triggerCentersDerived,
            MapOutlineStatus: mapOutlineStatus));
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

    private static int? TryGetMapOutlinePolygonIndex(
        IReadOnlyList<ParsedPolygon> polygons,
        IReadOnlyList<NtsPoint> points,
        IReadOnlyDictionary<int, Coordinate> matchedTriggers)
    {
        if (polygons.Count < 2)
        {
            return null;
        }

        var areaRankedPolygons = polygons
            .Select((polygon, index) => (polygon, index))
            .OrderByDescending(item => item.polygon.Geometry.Area)
            .ThenBy(item => item.index)
            .ToList();
        var mapCandidate = areaRankedPolygons[0];
        if (areaRankedPolygons.Count > 1)
        {
            var secondLargestArea = areaRankedPolygons[1].polygon.Geometry.Area;
            if (mapCandidate.polygon.Geometry.Area <= secondLargestArea)
            {
                return null;
            }
        }

        var allTriggersWithinCandidate = points.All(point => mapCandidate.polygon.Geometry.Covers(point));
        if (!allTriggersWithinCandidate)
        {
            return null;
        }

        if (matchedTriggers.ContainsKey(mapCandidate.index))
        {
            return null;
        }

        var requiredMatchedTriggers = polygons.Count - 1;
        if (matchedTriggers.Count != requiredMatchedTriggers)
        {
            return null;
        }

        var allNonCandidatePolygonsMatched = Enumerable.Range(0, polygons.Count)
            .Where(index => index != mapCandidate.index)
            .All(index => matchedTriggers.ContainsKey(index));
        if (!allNonCandidatePolygonsMatched)
        {
            return null;
        }

        return mapCandidate.index;
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

    private static bool TryBuildFallbackMapOutlineGeoJson(
        IReadOnlyList<ParsedPolygon> polygons,
        out string mapOutlineGeoJson)
    {
        mapOutlineGeoJson = string.Empty;
        if (polygons.Count == 0)
        {
            return false;
        }

        var unionGeometry = UnaryUnionOp.Union(polygons.Select(polygon => (NtsGeometry)polygon.Geometry).ToArray());
        var outlinePolygon = unionGeometry switch
        {
            NtsPolygon polygon => polygon,
            MultiPolygon multiPolygon when multiPolygon.NumGeometries == 1 => (NtsPolygon)multiPolygon.GetGeometryN(0),
            _ => null
        };
        if (outlinePolygon is null || outlinePolygon.IsEmpty || !outlinePolygon.IsValid)
        {
            return false;
        }

        mapOutlineGeoJson = BuildGeoJsonPolygon(outlinePolygon);
        return true;
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
