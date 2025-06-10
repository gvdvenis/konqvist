namespace Konqvist.Data.Models;

public enum ScoreType
{
    Vote,
    Resource
}

public record ScoreData(int Amount, int Round, ScoreType Type);
