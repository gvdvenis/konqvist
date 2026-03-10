using System.Security.Cryptography;

namespace Konqvist.Infrastructure.Tokens;

public static class LoginTokenGenerator
{
    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int RandomSuffixLength = 4;

    public static string GenerateRunnerToken(string teamName)
    {
        return $"{GetTeamInitial(teamName)}R{GenerateRandomSuffix()}";
    }

    public static string GenerateTeamCaptainToken(string teamName)
    {
        return $"{GetTeamInitial(teamName)}TC{GenerateRandomSuffix()}";
    }

    public static string GenerateGmToken()
    {
        return $"GM{GenerateRandomSuffix()}";
    }

    private static char GetTeamInitial(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentException("Team name must contain at least one non-whitespace character.", nameof(teamName));
        }

        return char.ToUpperInvariant(teamName.Trim()[0]);
    }

    private static string GenerateRandomSuffix()
    {
        Span<char> buffer = stackalloc char[RandomSuffixLength];
        for (var index = 0; index < RandomSuffixLength; index++)
        {
            var randomCharacterIndex = RandomNumberGenerator.GetInt32(AllowedCharacters.Length);
            buffer[index] = AllowedCharacters[randomCharacterIndex];
        }

        return new string(buffer);
    }
}
