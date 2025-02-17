using System.Text.Json;
using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public static class RegionHelpers
{
    public static bool IsDistrict(this Shape shape)
    {
        return shape.HasPropertyWithValue<string>("region-type", "election-district");
    }

    public static List<District> ToDistrictList(this IEnumerable<Shape>? shapes)
    {
        return shapes is null
            ? []
            : shapes
                .Where(s => s.IsDistrict())
                .Select(s => s.ToDistrict()!)
                .ToList();
    }

    /// <summary>
    ///     Call this method to check if the region has all the properties specified in the parameter.
    ///     This can be useful to check if the region is of a specific type, a district, for example.
    /// </summary>
    /// <param name="shape"></param>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    private static bool HasAllProperties(this Shape shape, params string[] propertyNames) =>
        shape.Properties.All(p => propertyNames.Contains(p.Key));

    /// <summary>
    ///     Check if the region has a property with the specified value.  
    /// </summary>
    /// <param name="shape"></param>
    /// <param name="propName"></param>
    /// <param name="expectedValue"></param>
    /// <returns></returns>
    private static bool HasPropertyWithValue<T>(this Shape shape, string propName, T expectedValue)
    {
        if (shape.Properties.TryGetValue(propName, out var value) && value is JsonElement jsonElement)
        {
            try
            {
                var actualValue = jsonElement.Deserialize<T>(jsonOptions);
                return actualValue is not null && actualValue.Equals(expectedValue);
            }
            catch (JsonException)
            {
                return false; // In case of deserialization failure
            }
        }

        return false;
    }

    private static JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    public static District? ToDistrict(this Shape shape)
    {
        if (!IsDistrict(shape))
            return null;

        var district = new District(shape.Coordinates, shape.Properties);
        return district;
    }
}