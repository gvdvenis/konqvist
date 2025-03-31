namespace Konqvist.Data.Contracts;

public record DistrictOwner(string TeamName, string DistrictName)
{
    public static DistrictOwner Empty { get; } = new (string.Empty, string.Empty);
}