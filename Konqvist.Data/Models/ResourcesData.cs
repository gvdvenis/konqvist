
namespace Konqvist.Data.Models;

public class ResourcesData
{
    public int R1 { get; set; }
    public int R2 { get; set; }
    public int R3 { get; set; }
    public int R4 { get; set; }

    public static ResourcesData Empty { get; } = new();

    /// <summary>
    ///    Adds a value to all the resources
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator +(ResourcesData a, int b)
    {
        return new ResourcesData
        {
            R1 = a.R1 + b,
            R2 = a.R2 + b,
            R3 = a.R3 + b,
            R4 = a.R4 + b
        };
    }

    public static ResourcesData operator +(ResourcesData a, ResourcesData b)
    {
        return new ResourcesData
        {
            R1 = a.R1 + b.R1,
            R2 = a.R2 + b.R2,
            R3 = a.R3 + b.R3,
            R4 = a.R4 + b.R4
        };
    }

    /// <summary>
    ///     Subtracts a value from all the resources
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator -(ResourcesData a, int b)
    {
        return new ResourcesData
        {
            R1 = a.R1 - b,
            R2 = a.R2 - b,
            R3 = a.R3 - b,
            R4 = a.R4 - b
        };
    }

    /// <summary>
    ///     subtracts two resources
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator -(ResourcesData a, ResourcesData b)
    {
        return new ResourcesData
        {
            R1 = a.R1 - b.R1,
            R2 = a.R2 - b.R2,
            R3 = a.R3 - b.R3,
            R4 = a.R4 - b.R4
        };
    }

    /// <summary>
    ///     Multiplies all resources by the same value    
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator *(ResourcesData a, int b)
    {
        return new ResourcesData
        {
            R1 = a.R1 * b,
            R2 = a.R2 * b,
            R3 = a.R3 * b,
            R4 = a.R4 * b
        };
    }

    /// <summary>
    ///    Multiplies two resources with each other
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator *(ResourcesData a, ResourcesData b)
    {
        return new ResourcesData
        {
            R1 = a.R1 * b.R1,
            R2 = a.R2 * b.R2,
            R3 = a.R3 * b.R3,
            R4 = a.R4 * b.R4
        };
    }

    /// <summary>
    ///     Divides all resources by the same value
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator /(ResourcesData a, int b)
    {
        return new ResourcesData
        {
            R1 = a.R1 / b,
            R2 = a.R2 / b,
            R3 = a.R3 / b,
            R4 = a.R4 / b
        };
    }

    /// <summary>
    ///    Divides two resources by each other
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static ResourcesData operator /(ResourcesData a, ResourcesData b)
    {
        return new ResourcesData
        {
            R1 = a.R1 / b.R1,
            R2 = a.R2 / b.R2,
            R3 = a.R3 / b.R3,
            R4 = a.R4 / b.R4
        };
    }

    public int GetScore(string? resourceName)
    {
        return resourceName switch
        {
            "R1" => R1,
            "R2" => R2,
            "R3" => R3,
            "R4" => R4,
            _ => 0
        };
    }
}