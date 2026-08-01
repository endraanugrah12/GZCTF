using System;
using System.Text;
using System.Text.Json;
using GZCTF.Models.Internal;
using GZCTF.Services.Container.Build;
using Xunit;

namespace GZCTF.Test.UnitTests.Container.Build;

public class K8sChallengeImageBuilderStaticsTest
{
    [Fact]
    public void RegistryTarget_NormalizesNamespaceAndKeepsRegistryHost()
    {
        var registry = new BuildRegistryConfig
        {
            Server = "GHCR.io/",
            Namespace = "Endraanugrah12"
        };

        var target = DockerChallengeImageBuilder.GetRegistryTarget(
            registry, 7, "42-web-checker", "abcdef123456");

        Assert.Equal("GHCR.io/endraanugrah12/gzctf-auto/7/42-web-checker", target.Repository);
        Assert.Equal(target.Repository + ":abcdef123456", target.ImageTag);
    }

    [Fact]
    public void DockerConfig_ContainsRegistryCredentials()
    {
        var bytes = K8sChallengeImageBuilder.CreateDockerConfig(
            "ghcr.io/", "builder", "secret-token");
        using var json = JsonDocument.Parse(bytes);
        var entry = json.RootElement.GetProperty("auths").GetProperty("ghcr.io");

        Assert.Equal("builder", entry.GetProperty("username").GetString());
        Assert.Equal("secret-token", entry.GetProperty("password").GetString());
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("builder:secret-token")),
            entry.GetProperty("auth").GetString());
    }

    [Fact]
    public void BuildctlArguments_PreservePathsWithoutShellParsing()
    {
        var request = new ChallengeBuildRequest(
            42, 7, "Web", "/tmp/context with spaces", "nested/Dockerfile");

        var info = K8sChallengeImageBuilder.CreateBuildctlStartInfo(
            "unix:///run/buildkit/buildkitd.sock",
            request,
            "ghcr.io/owner/gzctf-auto/7/42-web:abcdef123456",
            "/tmp/docker config");

        Assert.Equal("buildctl", info.FileName);
        Assert.Contains("--local=context=/tmp/context with spaces", info.ArgumentList);
        Assert.Contains("--opt=filename=nested/Dockerfile", info.ArgumentList);
        Assert.Equal("/tmp/docker config", info.Environment["DOCKER_CONFIG"]);
    }
}
