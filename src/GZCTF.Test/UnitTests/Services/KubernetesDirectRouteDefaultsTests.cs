using GZCTF.Models.Internal;
using GZCTF.Services.Container;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class KubernetesDirectRouteDefaultsTests
{
    [Fact]
    public void KubernetesOverridesLegacyPlatformProxyAndDerivesRouteDomain()
    {
        var provider = new ContainerProvider
        {
            Type = ContainerProviderType.Kubernetes,
            PortMappingType = ContainerPortMappingType.PlatformProxy,
            PublicEntry = "ctf.hackitbraw.site"
        };
        var route = new PublicChallengeRouteConfig();

        KubernetesDirectRouteDefaults.ApplyPortMapping(provider);
        KubernetesDirectRouteDefaults.ApplyRouteBaseDomain(route, provider);

        Assert.Equal(ContainerPortMappingType.Default, provider.PortMappingType);
        Assert.Equal("chall.ctf.hackitbraw.site", route.BaseDomain);
    }

    [Fact]
    public void KubernetesPreservesExplicitRouteDomain()
    {
        var provider = new ContainerProvider
        {
            Type = ContainerProviderType.Kubernetes,
            PublicEntry = "ctf.example.com"
        };
        var route = new PublicChallengeRouteConfig { BaseDomain = "instances.example.net" };

        KubernetesDirectRouteDefaults.ApplyRouteBaseDomain(route, provider);

        Assert.Equal("instances.example.net", route.BaseDomain);
    }

    [Fact]
    public void DockerConfigurationIsUnchanged()
    {
        var provider = new ContainerProvider
        {
            Type = ContainerProviderType.Docker,
            PortMappingType = ContainerPortMappingType.PlatformProxy,
            PublicEntry = "ctf.example.com"
        };
        var route = new PublicChallengeRouteConfig();

        KubernetesDirectRouteDefaults.ApplyPortMapping(provider);
        KubernetesDirectRouteDefaults.ApplyRouteBaseDomain(route, provider);

        Assert.Equal(ContainerPortMappingType.PlatformProxy, provider.PortMappingType);
        Assert.Empty(route.BaseDomain);
    }
}
