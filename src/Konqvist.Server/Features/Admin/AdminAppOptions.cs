namespace Konqvist.Server.Features.Admin;

public sealed class AdminAppOptions
{
    public const string SectionName = "AdminApp";

    public string BaseUrl { get; init; } = "http://localhost:5071";
}
