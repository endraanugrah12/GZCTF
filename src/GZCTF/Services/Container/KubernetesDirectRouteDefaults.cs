using GZCTF.Models.Internal;

namespace GZCTF.Services.Container;

internal static class KubernetesDirectRouteDefaults
{
    /// <summary>
    /// Kubernetes challenge routing in this fork is handled by the external
    /// wildcard ingress controller. PlatformProxy would return UUID entries to
    /// the client and bypass that controller, so it is never valid here.
    /// </summary>
    internal static void ApplyPortMapping(ContainerProvider provider)
    {
        if (provider.Type == ContainerProviderType.Kubernetes)
            provider.PortMappingType = ContainerPortMappingType.Default;
    }

    /// <summary>
    /// Preserve an explicitly configured route suffix, otherwise derive the
    /// same default used by the deployment wizard.
    /// </summary>
    internal static void ApplyRouteBaseDomain(
        PublicChallengeRouteConfig route,
        ContainerProvider provider)
    {
        if (provider.Type != ContainerProviderType.Kubernetes ||
            !string.IsNullOrWhiteSpace(route.BaseDomain))
            return;

        var entry = provider.PublicEntry.Trim();
        if (string.IsNullOrEmpty(entry))
            return;

        var candidate = entry.Contains("://", StringComparison.Ordinal)
            ? entry
            : $"https://{entry}";

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
            route.BaseDomain = $"chall.{uri.Host.Trim('.').ToLowerInvariant()}";
    }
}
