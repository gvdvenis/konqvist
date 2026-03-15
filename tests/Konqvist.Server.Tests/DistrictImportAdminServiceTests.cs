using System.IO.Compression;
using System.Net;
using Konqvist.Admin.Features.Districts;
using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class DistrictImportAdminServiceTests
{
    [Fact]
    public async Task ImportAsync_KmlReplacesExistingDistrictsAndAppliesDefaults()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        await SeedExistingDistrictAsync(harness, templateId);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("Imported District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Trigger", "0.5,0.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
        Assert.Equal(0, result.Summary.TriggerCentersDerived);
        Assert.Equal(DistrictMapOutlineStatus.GeneratedFallback, result.Summary.MapOutlineStatus);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .ToListAsync();
        var district = Assert.Single(districts);
        Assert.NotNull(template.MapOutlineGeoJson);
        Assert.Equal("Imported District", district.Name);
        Assert.Equal(0, district.Gold);
        Assert.Equal(0, district.Voters);
        Assert.Equal(0, district.Likes);
        Assert.Equal(0, district.Oil);
        Assert.Equal(15d, district.TriggerRadiusMeters);
        Assert.Equal(0.5d, district.TriggerLat, 6);
        Assert.Equal(0.5d, district.TriggerLng, 6);
        Assert.Contains("\"type\":\"Polygon\"", district.GeoJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_KmzImportsDistricts()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("A Trigger", "0.4,0.4,0"));
        var kmzBytes = BuildKmz(kml);

        var result = await ImportKmzAsync(service, templateId, "districts.kmz", kmzBytes);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
        Assert.Equal(DistrictMapOutlineStatus.GeneratedFallback, result.Summary.MapOutlineStatus);
    }

    [Fact]
    public async Task ImportAsync_KmlImportsDistrictsFromAsyncOnlyReadStream()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("A Trigger", "0.4,0.4,0"));
        var kmlBytes = System.Text.Encoding.UTF8.GetBytes(kml);
        await using var stream = new AsyncOnlyReadStream(new MemoryStream(kmlBytes));

        var result = await service.ImportAsync(templateId, stream, "districts.kml");

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
        Assert.Equal(DistrictMapOutlineStatus.GeneratedFallback, result.Summary.MapOutlineStatus);
    }

    [Fact]
    public async Task ImportAsync_KmzImportsDistrictsFromAsyncOnlyReadStream()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("A Trigger", "0.4,0.4,0"));
        var kmzBytes = BuildKmz(kml);
        await using var stream = new AsyncOnlyReadStream(new MemoryStream(kmzBytes));

        var result = await service.ImportAsync(templateId, stream, "districts.kmz");

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
    }

    [Fact]
    public async Task ImportAsync_UsesFirstPointAndCenterFallback()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PolygonPlacemark("District B", "2,0,0 2,1,0 3,1,0 3,0,0 2,0,0"),
            PointPlacemark("A Trigger First", "0.2,0.2,0"),
            PointPlacemark("A Trigger Second", "0.8,0.8,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
        Assert.Equal(1, result.Summary.TriggerCentersDerived);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .ToListAsync();
        Assert.Equal(2, districts.Count);

        var districtA = districts.Single(entity => entity.Name == "District A");
        Assert.Equal(0.2d, districtA.TriggerLat, 6);
        Assert.Equal(0.2d, districtA.TriggerLng, 6);

        var districtB = districts.Single(entity => entity.Name == "District B");
        Assert.Equal(0.5d, districtB.TriggerLat, 6);
        Assert.Equal(2.5d, districtB.TriggerLng, 6);
    }

    [Fact]
    public async Task ImportAsync_PrefersSmallestAreaPolygonWhenPointOverlapsMultiple()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("Big District", "0,0,0 0,4,0 4,4,0 4,0,0 0,0,0"),
            PolygonPlacemark("Small District", "1,1,0 1,2,0 2,2,0 2,1,0 1,1,0"),
            PointPlacemark("Overlap Trigger", "1.5,1.5,0"),
            PointPlacemark("Big Trigger", "3.5,3.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, result.Summary.DistrictsImported);
        Assert.Equal(2, result.Summary.TriggerCirclesMatched);
        Assert.Equal(0, result.Summary.TriggerCentersDerived);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync();
        var smallDistrict = districts.Single(entity => entity.Name == "Small District");
        Assert.Equal(1.5d, smallDistrict.TriggerLat, 6);
        Assert.Equal(1.5d, smallDistrict.TriggerLng, 6);
    }

    [Fact]
    public async Task ImportAsync_TwoPolygonsWithMapWithoutTrigger_StoresMapOutline()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("Map Outline", "-1,-1,0 -1,3,0 3,3,0 3,-1,0 -1,-1,0"),
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("A Trigger", "0.5,0.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(1, result.Summary.DistrictsImported);
        Assert.Equal(1, result.Summary.TriggerCirclesMatched);
        Assert.Equal(0, result.Summary.TriggerCentersDerived);
        Assert.Equal(DistrictMapOutlineStatus.Imported, result.Summary.MapOutlineStatus);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.NotNull(template.MapOutlineGeoJson);

        var districtCount = await dbContext.DistrictTemplates
            .CountAsync(entity => entity.GameTemplateId == templateId);
        Assert.Equal(1, districtCount);
    }

    [Fact]
    public async Task ImportAsync_LargestPolygonContainingAllTriggers_IsStoredAsMapOutline()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("Map Outline", "-1,-1,0 -1,5,0 5,5,0 5,-1,0 -1,-1,0"),
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PolygonPlacemark("District B", "2,0,0 2,1,0 3,1,0 3,0,0 2,0,0"),
            PointPlacemark("A Trigger", "0.5,0.5,0"),
            PointPlacemark("B Trigger", "2.5,0.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, result.Summary.DistrictsImported);
        Assert.Equal(2, result.Summary.TriggerCirclesMatched);
        Assert.Equal(0, result.Summary.TriggerCentersDerived);
        Assert.Equal(DistrictMapOutlineStatus.Imported, result.Summary.MapOutlineStatus);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.NotNull(template.MapOutlineGeoJson);
        Assert.Contains("\"type\":\"Polygon\"", template.MapOutlineGeoJson, StringComparison.Ordinal);

        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .OrderBy(entity => entity.Name)
            .Select(entity => entity.Name)
            .ToListAsync();
        Assert.Equal(["District A", "District B"], districts);
    }

    [Fact]
    public async Task ImportAsync_WhenLargestPolygonDoesNotContainAllTriggers_DoesNotStoreMapOutline()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("Large District", "0,0,0 0,4,0 4,4,0 4,0,0 0,0,0"),
            PolygonPlacemark("Far District", "10,10,0 10,11,0 11,11,0 11,10,0 10,10,0"),
            PointPlacemark("Far Trigger", "10.5,10.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, result.Summary.DistrictsImported);
        Assert.Equal(DistrictMapOutlineStatus.NotAvailable, result.Summary.MapOutlineStatus);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Null(template.MapOutlineGeoJson);
    }

    [Fact]
    public async Task ImportAsync_WhenNoExplicitMapOutline_GeneratesFallbackFromUnionBoundary()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PolygonPlacemark("District B", "1,0,0 1,1,0 2,1,0 2,0,0 1,0,0"),
            PointPlacemark("A Trigger", "0.5,0.5,0"),
            PointPlacemark("B Trigger", "1.5,0.5,0"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(DistrictMapOutlineStatus.GeneratedFallback, result.Summary.MapOutlineStatus);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.NotNull(template.MapOutlineGeoJson);
        Assert.Contains("\"type\":\"Polygon\"", template.MapOutlineGeoJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ReturnsInvalidFileTypeForUnsupportedExtension()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await service.ImportAsync(templateId, stream, "districts.txt");

        Assert.Equal(DistrictImportStatus.InvalidFileType, result.Status);
    }

    [Fact]
    public async Task ImportAsync_ReturnsTemplateNotFoundWhenTemplateMissing()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var service = CreateService(harness.DbFactory);
        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"));

        var result = await ImportKmlAsync(service, int.MaxValue, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.TemplateNotFound, result.Status);
    }

    [Fact]
    public async Task ImportAsync_WhenNetworkLinkIsPresent_ImportsFromLinkedSourceAndPersistsSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/districts.kml";
        var linkedKml = BuildKml(
            PolygonPlacemark("Linked District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Linked Trigger", "0.3,0.3,0"));
        var requestedUrls = new List<string>();
        var service = CreateService(
            harness.DbFactory,
            (request, _) =>
            {
                requestedUrls.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(linkedKml)
                };
                return response;
            });

        var uploadedKml = BuildKml(NetworkLink(sourceUrl));
        var result = await ImportKmlAsync(service, templateId, "districts.kml", uploadedKml);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.Single(requestedUrls);
        Assert.Equal(sourceUrl, requestedUrls[0]);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);

        var district = await dbContext.DistrictTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.GameTemplateId == templateId);
        Assert.Equal("Linked District", district.Name);
    }

    [Fact]
    public async Task ImportAsync_WhenNetworkLinkIsPresentInKmz_ImportsFromLinkedSourceAndPersistsSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/districts.kml";
        var linkedKml = BuildKml(
            PolygonPlacemark("Linked District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Linked Trigger", "0.3,0.3,0"));
        var requestedUrls = new List<string>();
        var service = CreateService(
            harness.DbFactory,
            (request, _) =>
            {
                requestedUrls.Add(request.RequestUri?.AbsoluteUri ?? string.Empty);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(linkedKml)
                };
            });

        var uploadedKml = BuildKml(NetworkLink(sourceUrl));
        var uploadedKmz = BuildKmz(uploadedKml);
        var result = await ImportKmzAsync(service, templateId, "districts.kmz", uploadedKmz);

        Assert.Equal(DistrictImportStatus.Imported, result.Status);
        Assert.Single(requestedUrls);
        Assert.Equal(sourceUrl, requestedUrls[0]);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);

        var district = await dbContext.DistrictTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.GameTemplateId == templateId);
        Assert.Equal("Linked District", district.Name);
    }

    [Fact]
    public async Task ClassifyUploadAsync_WhenUploadContainsDirectData_ReturnsDirectData()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var service = CreateService(harness.DbFactory);
        var kml = BuildKml(
            PolygonPlacemark("District A", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("A Trigger", "0.4,0.4,0"));
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(kml));

        var result = await service.ClassifyUploadAsync(stream, "districts.kml");

        Assert.Equal(DistrictUploadClassificationStatus.DirectData, result.Status);
        Assert.Null(result.SourceUrl);
    }

    [Fact]
    public async Task ClassifyUploadAsync_WhenUploadContainsNetworkLink_ReturnsNetworkLinkWithSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var service = CreateService(harness.DbFactory);
        const string sourceUrl = "https://cdn.example.com/districts.kml";
        var kml = BuildKml(NetworkLink(sourceUrl));
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(kml));

        var result = await service.ClassifyUploadAsync(stream, "districts.kml");

        Assert.Equal(DistrictUploadClassificationStatus.NetworkLink, result.Status);
        Assert.Equal(sourceUrl, result.SourceUrl);
    }

    [Fact]
    public async Task ClassifyUploadAsync_WhenKmzContainsNetworkLink_ReturnsNetworkLinkWithSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var service = CreateService(harness.DbFactory);
        const string sourceUrl = "https://cdn.example.com/districts.kml";
        var kml = BuildKml(NetworkLink(sourceUrl));
        var kmzBytes = BuildKmz(kml);
        await using var stream = new MemoryStream(kmzBytes);

        var result = await service.ClassifyUploadAsync(stream, "districts.kmz");

        Assert.Equal(DistrictUploadClassificationStatus.NetworkLink, result.Status);
        Assert.Equal(sourceUrl, result.SourceUrl);
    }

    [Fact]
    public async Task ClassifyUploadAsync_WhenNetworkLinkIsNotHttps_StillReturnsNetworkLinkForPrefill()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var service = CreateService(harness.DbFactory);
        const string sourceUrl = "http://example.com/districts.kml";
        var kml = BuildKml(NetworkLink(sourceUrl));
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(kml));

        var result = await service.ClassifyUploadAsync(stream, "districts.kml");

        Assert.Equal(DistrictUploadClassificationStatus.NetworkLink, result.Status);
        Assert.Equal(sourceUrl, result.SourceUrl);
    }

    [Fact]
    public async Task ImportAsync_WhenNetworkLinkIsNotHttps_ReturnsInvalidSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);
        var uploadedKml = BuildKml(NetworkLink("http://example.com/districts.kml"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", uploadedKml);

        Assert.Equal(DistrictImportStatus.InvalidSourceUrl, result.Status);
        Assert.Equal("Source URL must be an absolute HTTPS URL.", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_WhenNetworkLinkDownloadFails_ReturnsSourceDownloadFailed()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(
            harness.DbFactory,
            (_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var uploadedKml = BuildKml(NetworkLink("https://cdn.example.com/districts.kml"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", uploadedKml);

        Assert.Equal(DistrictImportStatus.SourceDownloadFailed, result.Status);
        Assert.Contains("502", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_WhenNetworkLinkDownloadIsEmpty_ReturnsSourceContentEmpty()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(
            harness.DbFactory,
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            });
        var uploadedKml = BuildKml(NetworkLink("https://cdn.example.com/districts.kml"));

        var result = await ImportKmlAsync(service, templateId, "districts.kml", uploadedKml);

        Assert.Equal(DistrictImportStatus.SourceContentEmpty, result.Status);
    }

    [Fact]
    public async Task SourceUrlManagement_SaveRefreshAndClearSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/source.kml";
        var linkedKml = BuildKml(
            PolygonPlacemark("Refresh District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"));
        var service = CreateService(
            harness.DbFactory,
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(linkedKml)
            });

        var saveResult = await service.SaveSourceUrlAsync(templateId, sourceUrl);
        Assert.Equal(DistrictImportSourceUrlStatus.Updated, saveResult.Status);
        Assert.Equal(sourceUrl, saveResult.SourceUrl);

        var refreshResult = await service.RefreshFromSourceAsync(templateId);
        Assert.Equal(DistrictImportStatus.Imported, refreshResult.Status);

        var clearResult = await service.ClearSourceUrlAsync(templateId);
        Assert.Equal(DistrictImportSourceUrlStatus.Cleared, clearResult.Status);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Null(template.DistrictImportSourceUrl);
    }

    [Fact]
    public async Task SaveSourceUrlAsync_WhenSourceUrlIsNotHttps_ReturnsInvalidSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var result = await service.SaveSourceUrlAsync(templateId, "http://example.com/districts.kml");

        Assert.Equal(DistrictImportSourceUrlStatus.InvalidSourceUrl, result.Status);
        Assert.Equal("Source URL must be an absolute HTTPS URL.", result.ErrorMessage);
    }

    [Fact]
    public async Task RefreshFromSourceAsync_ReplacesExistingDistrictsAndKeepsStoredSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        await SeedExistingDistrictAsync(harness, templateId);
        const string sourceUrl = "https://cdn.example.com/source.kml";
        var linkedKml = BuildKml(
            PolygonPlacemark("Refreshed District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Refreshed Trigger", "0.2,0.2,0"));
        var service = CreateService(
            harness.DbFactory,
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(linkedKml)
            });

        var saveResult = await service.SaveSourceUrlAsync(templateId, sourceUrl);
        Assert.Equal(DistrictImportSourceUrlStatus.Updated, saveResult.Status);

        var refreshResult = await service.RefreshFromSourceAsync(templateId);

        Assert.Equal(DistrictImportStatus.Imported, refreshResult.Status);
        Assert.NotNull(refreshResult.Summary);
        Assert.Equal(1, refreshResult.Summary.DistrictsImported);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);

        var districts = await dbContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync();
        var district = Assert.Single(districts);
        Assert.Equal("Refreshed District", district.Name);
    }

    [Fact]
    public async Task RefreshFromSourceAsync_WithProvidedSourceUrl_ImportsAndPersistsSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/implicit-source.kml";
        var linkedKml = BuildKml(
            PolygonPlacemark("Implicit Source District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"));
        var service = CreateService(
            harness.DbFactory,
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(linkedKml)
            });

        var refreshResult = await service.RefreshFromSourceAsync(templateId, sourceUrl);

        Assert.Equal(DistrictImportStatus.Imported, refreshResult.Status);
        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);
    }

    [Fact]
    public async Task RefreshFromSourceAsync_WhenNestedNetworkLinkIsResolved_PersistsProvidedSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/outer-source.kml";
        const string nestedSourceUrl = "https://cdn.example.com/nested-source.kml";
        var requestedUrls = new List<string>();
        var outerKml = BuildKml(NetworkLink(nestedSourceUrl));
        var nestedKml = BuildKml(
            PolygonPlacemark("Nested District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Nested Trigger", "0.4,0.4,0"));
        var service = CreateService(
            harness.DbFactory,
            (request, _) =>
            {
                var requestUrl = request.RequestUri?.AbsoluteUri ?? string.Empty;
                requestedUrls.Add(requestUrl);
                return requestUrl switch
                {
                    sourceUrl => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(outerKml)
                    },
                    nestedSourceUrl => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(nestedKml)
                    },
                    _ => new HttpResponseMessage(HttpStatusCode.NotFound)
                };
            });

        var refreshResult = await service.RefreshFromSourceAsync(templateId, sourceUrl);

        Assert.Equal(DistrictImportStatus.Imported, refreshResult.Status);
        Assert.Equal([sourceUrl, nestedSourceUrl], requestedUrls);
        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);
    }

    [Fact]
    public async Task ImportAsync_WhenDirectDataImportedAfterSourceSaved_KeepsSavedSourceUrl()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        const string sourceUrl = "https://cdn.example.com/saved-source.kml";
        var service = CreateService(harness.DbFactory);

        var saveResult = await service.SaveSourceUrlAsync(templateId, sourceUrl);
        Assert.Equal(DistrictImportSourceUrlStatus.Updated, saveResult.Status);

        var kml = BuildKml(
            PolygonPlacemark("Direct District", "0,0,0 0,1,0 1,1,0 1,0,0 0,0,0"),
            PointPlacemark("Direct Trigger", "0.5,0.5,0"));
        var importResult = await ImportKmlAsync(service, templateId, "districts.kml", kml);

        Assert.Equal(DistrictImportStatus.Imported, importResult.Status);
        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == templateId);
        Assert.Equal(sourceUrl, template.DistrictImportSourceUrl);
    }

    [Fact]
    public async Task RefreshFromSourceAsync_WithoutSavedSourceUrl_ReturnsSourceUrlNotConfigured()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var result = await service.RefreshFromSourceAsync(templateId);

        Assert.Equal(DistrictImportStatus.SourceUrlNotConfigured, result.Status);
    }

    [Fact]
    public async Task GetDistrictPreviewAsync_ReturnsOrderedDistrictsWithConfiguredFields()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            dbContext.DistrictTemplates.AddRange(
                new DistrictTemplate
                {
                    GameTemplateId = templateId,
                    Name = "Beta",
                    GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[3,2],[3,3],[4,3],[4,2],[3,2]]]}",
                    TriggerLat = 2.5d,
                    TriggerLng = 3.5d,
                    TriggerRadiusMeters = 55d,
                    Gold = 6,
                    Voters = 7,
                    Likes = 8,
                    Oil = 9
                },
                new DistrictTemplate
                {
                    GameTemplateId = templateId,
                    Name = "Alpha",
                    GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[0,1],[1,1],[1,0],[0,0]]]}",
                    TriggerLat = 0.4d,
                    TriggerLng = 0.3d,
                    TriggerRadiusMeters = 45d,
                    Gold = 1,
                    Voters = 2,
                    Likes = 3,
                    Oil = 4
                });
            await dbContext.SaveChangesAsync();
        }

        var service = CreateService(harness.DbFactory);

        var preview = await service.GetDistrictPreviewAsync(templateId);

        Assert.Collection(
            preview,
            first =>
            {
                Assert.Equal("Alpha", first.Name);
                Assert.Equal(1, first.Gold);
                Assert.Equal(2, first.Voters);
                Assert.Equal(3, first.Likes);
                Assert.Equal(4, first.Oil);
                Assert.Equal(45d, first.TriggerRadiusMeters);
            },
            second =>
            {
                Assert.Equal("Beta", second.Name);
                Assert.Equal(6, second.Gold);
                Assert.Equal(7, second.Voters);
                Assert.Equal(8, second.Likes);
                Assert.Equal(9, second.Oil);
                Assert.Equal(55d, second.TriggerRadiusMeters);
            });
    }

    [Fact]
    public async Task GetDistrictPreviewAsync_WhenNoDistrictsConfigured_ReturnsEmptyList()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var preview = await service.GetDistrictPreviewAsync(templateId);

        Assert.Empty(preview);
    }

    [Fact]
    public async Task UpdateDistrictResourcesAsync_SavesEditedValuesForSelectedDistrict()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        int districtId;
        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            var district = new DistrictTemplate
            {
                GameTemplateId = templateId,
                Name = "Alpha",
                GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[0,1],[1,1],[1,0],[0,0]]]}",
                TriggerLat = 0.5d,
                TriggerLng = 0.5d,
                TriggerRadiusMeters = 20d,
                Gold = 0,
                Voters = 0,
                Likes = 0,
                Oil = 0
            };
            dbContext.DistrictTemplates.Add(district);
            await dbContext.SaveChangesAsync();
            districtId = district.Id;
        }

        var service = CreateService(harness.DbFactory);
        var result = await service.UpdateDistrictResourcesAsync(
            templateId,
            districtId,
            new DistrictResourceEditorInput
            {
                Gold = 4,
                Voters = 5,
                Likes = 6,
                Oil = 7,
                TriggerRadiusMeters = 35d
            });

        Assert.Equal(SaveDistrictResourcesResult.Saved, result);
        await using var updatedContext = await harness.DbFactory.CreateDbContextAsync();
        var updatedDistrict = await updatedContext.DistrictTemplates
            .SingleAsync(entity => entity.Id == districtId);
        Assert.Equal(4, updatedDistrict.Gold);
        Assert.Equal(5, updatedDistrict.Voters);
        Assert.Equal(6, updatedDistrict.Likes);
        Assert.Equal(7, updatedDistrict.Oil);
        Assert.Equal(35d, updatedDistrict.TriggerRadiusMeters);
    }

    [Fact]
    public async Task UpdateDistrictResourcesAsync_WithInvalidInput_ReturnsInvalidInput()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var result = await service.UpdateDistrictResourcesAsync(
            templateId,
            districtId: 1,
            new DistrictResourceEditorInput
            {
                Gold = -1,
                Voters = 2,
                Likes = 3,
                Oil = 4,
                TriggerRadiusMeters = 25d
            });

        Assert.Equal(SaveDistrictResourcesResult.InvalidInput, result);
    }

    [Fact]
    public async Task RandomizeDistrictResourcesAsync_AssignsValuesWithinInclusiveRangeInTens()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            dbContext.DistrictTemplates.AddRange(
                new DistrictTemplate
                {
                    GameTemplateId = templateId,
                    Name = "Alpha",
                    GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[0,1],[1,1],[1,0],[0,0]]]}",
                    TriggerLat = 0.5d,
                    TriggerLng = 0.5d,
                    TriggerRadiusMeters = 20d,
                    Gold = 0,
                    Voters = 0,
                    Likes = 0,
                    Oil = 0
                },
                new DistrictTemplate
                {
                    GameTemplateId = templateId,
                    Name = "Beta",
                    GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[2,2],[2,3],[3,3],[3,2],[2,2]]]}",
                    TriggerLat = 2.5d,
                    TriggerLng = 2.5d,
                    TriggerRadiusMeters = 25d,
                    Gold = 0,
                    Voters = 0,
                    Likes = 0,
                    Oil = 0
                });
            await dbContext.SaveChangesAsync();
        }

        var service = CreateService(harness.DbFactory);
        var result = await service.RandomizeDistrictResourcesAsync(templateId, minValue: 10, maxValue: 60);

        Assert.Equal(RandomizeDistrictResourcesStatus.Randomized, result.Status);
        Assert.Equal(2, result.DistrictsUpdated);
        await using var updatedContext = await harness.DbFactory.CreateDbContextAsync();
        var districts = await updatedContext.DistrictTemplates
            .Where(entity => entity.GameTemplateId == templateId)
            .ToListAsync();
        Assert.All(districts, district =>
        {
            Assert.InRange(district.Gold, 10, 60);
            Assert.InRange(district.Voters, 10, 60);
            Assert.InRange(district.Likes, 10, 60);
            Assert.InRange(district.Oil, 10, 60);
            Assert.Equal(0, district.Gold % 10);
            Assert.Equal(0, district.Voters % 10);
            Assert.Equal(0, district.Likes % 10);
            Assert.Equal(0, district.Oil % 10);
        });
    }

    [Fact]
    public async Task RandomizeDistrictResourcesAsync_WithInvalidRange_ReturnsInvalidRange()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var result = await service.RandomizeDistrictResourcesAsync(templateId, minValue: 7, maxValue: 3);

        Assert.Equal(RandomizeDistrictResourcesStatus.InvalidRange, result.Status);
    }

    [Fact]
    public async Task RandomizeDistrictResourcesAsync_WithoutAnyTenValueInRange_ReturnsInvalidRange()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 4);
        var service = CreateService(harness.DbFactory);

        var result = await service.RandomizeDistrictResourcesAsync(templateId, minValue: 1, maxValue: 9);

        Assert.Equal(RandomizeDistrictResourcesStatus.InvalidRange, result.Status);
    }

    private static DistrictImportAdminService CreateService(
        TestDbContextFactory dbFactory,
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? responder = null)
    {
        var httpClient = responder is null
            ? new HttpClient(new UnexpectedHttpMessageHandler())
            : new HttpClient(new DelegateHttpMessageHandler(responder));
        return new DistrictImportAdminService(dbFactory, httpClient);
    }

    private static async Task SeedExistingDistrictAsync(RoundConfigurationTestHarness harness, int templateId)
    {
        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        dbContext.DistrictTemplates.Add(new DistrictTemplate
        {
            GameTemplateId = templateId,
            Name = "Old District",
            GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[]}",
            TriggerLat = 0d,
            TriggerLng = 0d,
            TriggerRadiusMeters = 50d,
            Gold = 9,
            Voters = 9,
            Likes = 9,
            Oil = 9
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ImportDistrictTemplatesResult> ImportKmlAsync(
        DistrictImportAdminService service,
        int templateId,
        string fileName,
        string kml)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(kml);
        await using var stream = new MemoryStream(bytes);
        return await service.ImportAsync(templateId, stream, fileName);
    }

    private static async Task<ImportDistrictTemplatesResult> ImportKmzAsync(
        DistrictImportAdminService service,
        int templateId,
        string fileName,
        byte[] kmzBytes)
    {
        await using var stream = new MemoryStream(kmzBytes);
        return await service.ImportAsync(templateId, stream, fileName);
    }

    private static byte[] BuildKmz(string kml)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("doc.kml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(kml);
        }

        return stream.ToArray();
    }

    private static string BuildKml(params string[] placemarks)
    {
        return
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
            """
            + string.Join(Environment.NewLine, placemarks)
            +
            """
              </Document>
            </kml>
            """;
    }

    private static string PolygonPlacemark(string name, string coordinates)
    {
        return
            $"""
             <Placemark>
               <name>{name}</name>
               <Polygon>
                 <outerBoundaryIs>
                   <LinearRing>
                     <coordinates>{coordinates}</coordinates>
                   </LinearRing>
                 </outerBoundaryIs>
               </Polygon>
             </Placemark>
             """;
    }

    private static string PointPlacemark(string name, string coordinate)
    {
        return
            $@"<Placemark>
  <name>{name}</name>
  <Point>
    <coordinates>{coordinate}</coordinates>
  </Point>
</Placemark>";
    }

    private static string NetworkLink(string href)
    {
        return
            $"""
             <NetworkLink>
               <Link>
                 <href>{href}</href>
               </Link>
             </NetworkLink>
             """;
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request, cancellationToken));
        }
    }

    private sealed class UnexpectedHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new Xunit.Sdk.XunitException("HTTP should not be called for this test.");
        }
    }

    private sealed class AsyncOnlyReadStream(Stream innerStream) : Stream
    {
        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Synchronous reads are not supported.");
        }

        public override int Read(Span<byte> buffer)
        {
            throw new NotSupportedException("Synchronous reads are not supported.");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return innerStream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            return innerStream.DisposeAsync();
        }
    }
}
