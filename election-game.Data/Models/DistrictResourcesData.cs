namespace election_game.Data.Models;

public class DistrictResourcesData

{
    public int R1 { get; set; }
    public int R2 { get; set; }
    public int R3 { get; set; }
    public int R4 { get; set; }

    //Operator for adding two resources together
    public static DistrictResourcesData operator +(DistrictResourcesData a, DistrictResourcesData b)
    {
        return new DistrictResourcesData
        {
            R1 = a.R1 + b.R1,
            R2 = a.R2 + b.R2,
            R3 = a.R3 + b.R3,
            R4 = a.R4 + b.R4
        };
    }
}