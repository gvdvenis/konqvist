using System.Text.RegularExpressions;
using Konqvist.Infrastructure.Tokens;

namespace Konqvist.Infrastructure.Tests;

public class LoginTokenGeneratorTests
{
    [Fact]
    public void GenerateRunnerToken_UsesTeamInitialAndRunnerPrefix()
    {
        var token = LoginTokenGenerator.GenerateRunnerToken("Delta");

        Assert.Matches(new Regex("^DR[a-zA-Z0-9]{4}$"), token);
    }

    [Fact]
    public void GenerateTeamCaptainToken_UsesTeamInitialAndCaptainPrefix()
    {
        var token = LoginTokenGenerator.GenerateTeamCaptainToken("Echo");

        Assert.Matches(new Regex("^ETC[a-zA-Z0-9]{4}$"), token);
    }

    [Fact]
    public void GenerateGmToken_UsesGmPrefix()
    {
        var token = LoginTokenGenerator.GenerateGmToken();

        Assert.Matches(new Regex("^GM[a-zA-Z0-9]{4}$"), token);
    }

    [Fact]
    public void GenerateRunnerToken_UsesTrimmedUppercaseInitial()
    {
        var token = LoginTokenGenerator.GenerateRunnerToken("  alpha ");

        Assert.StartsWith("AR", token);
    }

    [Fact]
    public void GenerateRunnerToken_ThrowsForBlankTeamName()
    {
        Assert.Throws<ArgumentException>(() => LoginTokenGenerator.GenerateRunnerToken(" "));
    }
}
