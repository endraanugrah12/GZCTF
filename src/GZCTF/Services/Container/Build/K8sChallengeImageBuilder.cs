using System.Diagnostics;
using System.Text;
using GZCTF.Models.Internal;
using GZCTF.Services.Container.Provider;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;

namespace GZCTF.Services.Container.Build;

/// <summary>
/// Streams repository-bound challenge contexts to the rootless BuildKit sidecar,
/// pushes the resulting image, and provisions pull credentials for challenge pods.
/// </summary>
public sealed class K8sChallengeImageBuilder(
    IContainerProvider<Kubernetes, KubernetesMetadata> provider,
    IOptionsMonitor<BuildRegistryConfig> registryConfig,
    IConfiguration configuration,
    ILogger<K8sChallengeImageBuilder> logger) : IChallengeImageBuilder
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(15);
    private const int LogTailBytes = 32 * 1024;
    private readonly byte[] _xorKey = configuration["XorKey"]?.ToUTF8Bytes() ?? [];

    public async Task<ChallengeBuildResult> BuildAsync(
        ChallengeBuildRequest req,
        CancellationToken token,
        Action<string>? onProgress = null)
    {
        var reg = registryConfig.CurrentValue;
        if (!reg.IsConfigured)
            return Failure(
                "Kubernetes auto-build requires an image registry. In Admin Settings, " +
                "enable 'Push built images', then configure the registry host and namespace.");

        if (!Directory.Exists(req.ContextDir))
            return Failure($"Build context does not exist: {req.ContextDir}");

        if (!File.Exists(Path.Combine(req.ContextDir, req.Dockerfile)))
            return Failure($"Dockerfile does not exist in the build context: {req.Dockerfile}");

        var logTail = new StringBuilder();
        var hashTar = Path.Combine(Path.GetTempPath(), $"gzctf-k8s-build-{Guid.NewGuid():N}.tar.gz");
        var dockerConfigDir = Path.Combine(Path.GetTempPath(), $"gzctf-docker-config-{Guid.NewGuid():N}");

        try
        {
            var digest = await DockerChallengeImageBuilder.WriteContextTarAsync(
                req.ContextDir, hashTar, token);
            var kindSuffix = req.Kind == ChallengeBuildKind.Checker ? "-checker" : string.Empty;
            var slug = $"{req.ChallengeId}-{DockerChallengeImageBuilder.NormalizeSlug(req.ChallengeSlug)}{kindSuffix}";
            var target = DockerChallengeImageBuilder.GetRegistryTarget(reg, req.GameId, slug, digest[..12]);
            var password = DockerChallengeImageBuilder.DecryptXorPassword(reg.Password, _xorKey);

            Directory.CreateDirectory(dockerConfigDir);
            if (!string.IsNullOrWhiteSpace(reg.Username))
            {
                var dockerConfig = CreateDockerConfig(reg.Server!, reg.Username, password);
                await File.WriteAllBytesAsync(Path.Combine(dockerConfigDir, "config.json"), dockerConfig, token);
            }

            Append(logTail, $"[buildkit] building {target.ImageTag}\n", onProgress);
            var startInfo = CreateBuildctlStartInfo(
                provider.GetMetadata().Config.BuildkitAddress,
                req,
                target.ImageTag,
                dockerConfigDir);

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => AppendLine(logTail, e.Data, onProgress);
            process.ErrorDataReceived += (_, e) => AppendLine(logTail, e.Data, onProgress);

            if (!process.Start())
                return Failure("Failed to start buildctl.", Snapshot(logTail));

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = new CancellationTokenSource(BuildTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
                process.WaitForExit(); // drain asynchronous stdout/stderr callbacks
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (token.IsCancellationRequested)
                    return Failure("Build cancelled by host.", Snapshot(logTail));
                return Failure($"Build timed out after {BuildTimeout.TotalMinutes:0} minutes.", Snapshot(logTail));
            }

            if (process.ExitCode != 0)
            {
                var tail = Snapshot(logTail);
                logger.LogWarning("BuildKit build failed for {Image} with exit code {Code}: {Tail}",
                    target.ImageTag, process.ExitCode, tail);
                return Failure($"BuildKit exited with code {process.ExitCode}.", tail);
            }

            await EnsurePullSecretAsync(reg.Server!, reg.Username, password, token);
            Append(logTail, $"[buildkit] pushed {target.ImageTag}\n", onProgress);
            return new ChallengeBuildResult(true, target.ImageTag, digest, Snapshot(logTail), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Kubernetes challenge build failed for challenge {ChallengeId}", req.ChallengeId);
            Append(logTail, $"[buildkit] error: {ex.Message}\n", onProgress);
            return Failure(ex.Message, Snapshot(logTail));
        }
        finally
        {
            TryDeleteFile(hashTar);
            TryDeleteDirectory(dockerConfigDir);
        }
    }

    internal static ProcessStartInfo CreateBuildctlStartInfo(
        string address,
        ChallengeBuildRequest req,
        string imageTag,
        string dockerConfigDir)
    {
        var info = new ProcessStartInfo("buildctl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.Environment["DOCKER_CONFIG"] = dockerConfigDir;

        foreach (var arg in new[]
                 {
                     "--addr", address,
                     "build",
                     "--progress=plain",
                     "--frontend=dockerfile.v0",
                     $"--local=context={req.ContextDir}",
                     $"--local=dockerfile={req.ContextDir}",
                     $"--opt=filename={req.Dockerfile}",
                     "--opt=platform=linux/amd64",
                     $"--output=type=image,name={imageTag},push=true"
                 })
            info.ArgumentList.Add(arg);

        return info;
    }

    internal static byte[] CreateDockerConfig(string server, string? username, string? password)
    {
        server = server.Trim().TrimEnd('/');
        return KubernetesRegistrySecret.Create(
            server, username ?? string.Empty, password ?? string.Empty,
            "docker-config", "unused").Data[".dockerconfigjson"];
    }

    private async Task EnsurePullSecretAsync(
        string server, string? username, string password, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(username))
            return; // anonymous registry; no pull secret required

        server = server.Trim().TrimEnd('/');
        var metadata = provider.GetMetadata();
        var secretName = KubernetesRegistrySecret.GetName(server, username);
        var secret = KubernetesRegistrySecret.Create(
            server, username, password, secretName, metadata.Config.Namespace);

        var client = provider.GetProvider();
        try
        {
            var existing = await client.CoreV1.ReadNamespacedSecretAsync(
                secretName, metadata.Config.Namespace, cancellationToken: token);
            secret.Metadata.ResourceVersion = existing.Metadata.ResourceVersion;
            await client.CoreV1.ReplaceNamespacedSecretAsync(
                secret, secretName, metadata.Config.Namespace, cancellationToken: token);
        }
        catch (HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await client.CoreV1.CreateNamespacedSecretAsync(
                secret, metadata.Config.Namespace, cancellationToken: token);
        }

        metadata.AuthSecretNames[server] = secretName;
    }

    public Task<bool> TryRestoreImageAsync(string imageTag, CancellationToken token) =>
        Task.FromResult(false);

    public Task<int> DeleteGameImagesAsync(int gameId, CancellationToken token) =>
        Task.FromResult(0);

    private static ChallengeBuildResult Failure(string message, string log = "") =>
        new(false, null, null, log, message);

    private static void AppendLine(StringBuilder tail, string? line, Action<string>? onProgress)
    {
        if (line is not null)
            Append(tail, line + "\n", onProgress);
    }

    private static void Append(StringBuilder tail, string text, Action<string>? onProgress)
    {
        text = DockerChallengeImageBuilder.ScrubSecrets(text);
        lock (tail)
        {
            tail.Append(text);
            if (tail.Length > LogTailBytes)
                tail.Remove(0, tail.Length - LogTailBytes);
        }
        try { onProgress?.Invoke(text); } catch { /* progress sinks are best-effort */ }
    }

    private static string Snapshot(StringBuilder tail)
    {
        lock (tail)
            return tail.ToString();
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best-effort cancellation */ }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
