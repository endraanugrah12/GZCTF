using GZCTF.Models.Internal;
using GZCTF.Services.Container.Manager;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class ChallengeRouteTests
{
    [Theory]
    [InlineData("Hello, CTF!", "hello-ctf")]
    [InlineData("---", "challenge")]
    [InlineData("Mixed___Separators", "mixed-separators")]
    public void Slugify_ProducesDnsLabel(string input, string expected) =>
        Assert.Equal(expected, ChallengeRoute.Slugify(input));

    [Fact]
    public void GetHost_UsesSameRouteShapeForEveryProvider()
    {
        var config = new ContainerConfig
        {
            ChallengeSlug = "Web Challenge",
            ChallengeId = 42,
            TeamId = "7"
        };

        Assert.Equal("web-challenge-c42-t7.chall.ctf.example.com",
            ChallengeRoute.GetHost(config, "chall.ctf.example.com"));
    }

    [Fact]
    public void NormalizeBaseDomain_RemovesDotsAndNormalizesCase() =>
        Assert.Equal("chall.ctf.example.com",
            ChallengeRoute.NormalizeBaseDomain(".CHALL.CTF.EXAMPLE.COM."));

    [Fact]
    public void GetHost_LimitsFirstDnsLabelTo63Characters()
    {
        var config = new ContainerConfig
        {
            ChallengeSlug = new string('a', 100),
            ChallengeId = int.MaxValue,
            TeamId = int.MaxValue.ToString()
        };

        var host = ChallengeRoute.GetHost(config, "chall.ctf.example.com");

        Assert.Equal(63, host.Split('.')[0].Length);
    }
}
