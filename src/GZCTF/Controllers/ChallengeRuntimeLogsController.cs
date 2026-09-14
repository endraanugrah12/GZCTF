using System.ComponentModel.DataAnnotations;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using GZCTF.Middlewares;
using GZCTF.Services.Container.Provider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Controllers;

[ApiController, RequireGameAdmin]
[Route("api/edit/Games/{id:int}/Challenges/{cId:int}/RuntimeLogs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ChallengeRuntimeLogsController(AppDbContext db, IServiceProvider services,
    ILogger<ChallengeRuntimeLogsController> logger) : ControllerBase
{
    // Scope every lookup by both game and challenge; never accept an arbitrary Docker ID.
    internal IQueryable<Models.Data.Container> Containers(int id, int cId) => db.Containers.AsNoTracking()
        .Where(c => db.GameChallenges.Any(ch => ch.Id == cId && ch.GameId == id &&
            (ch.TestContainerId == c.Id || ch.SharedContainerId == c.Id ||
             db.GameInstances.Any(i => i.ChallengeId == cId && i.ContainerId == c.Id) ||
             db.AdTeamServices.Any(s => s.ChallengeId == cId && s.ContainerId == c.Id) ||
             db.KothTargets.Any(t => t.ChallengeId == cId && t.ContainerId == c.Id))));

    [HttpGet]
    public async Task<IActionResult> List(int id, int cId, CancellationToken token)
    {
        if (!await db.GameChallenges.AnyAsync(c => c.Id == cId && c.GameId == id, token)) return NotFound();
        var supported = services.GetService<IContainerProvider<DockerClient, DockerMetadata>>() is not null;
        var containers = await Containers(id, cId).OrderByDescending(c => c.StartedAt).Take(200)
            .Select(c => new { c.Id, c.ContainerId, c.StartedAt, c.Status,
                Team = c.GameInstance != null ? c.GameInstance.Participation.Team.Name : "Test / shared / A&D" })
            .ToArrayAsync(token);
        return Ok(new { supported, containers });
    }

    [HttpGet("{containerId:guid}")]
    public async Task<IActionResult> Logs(int id, int cId, Guid containerId,
        [FromQuery, Range(10, 1000)] int tail = 200, CancellationToken token = default)
    {
        var container = await Containers(id, cId).SingleOrDefaultAsync(c => c.Id == containerId, token);
        if (container is null) return NotFound();
        var provider = services.GetService<IContainerProvider<DockerClient, DockerMetadata>>();
        if (provider is null) return StatusCode(501, new { message = "Runtime log viewing currently supports Docker containers." });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var client = provider.GetProvider();
            var inspect = await client.Containers.InspectContainerAsync(container.ContainerId, timeout.Token);
            var output = new BoundedLog();
            await client.Containers.GetContainerLogsAsync(container.ContainerId, new ContainerLogsParameters
            {
                ShowStdout = true, ShowStderr = true, Timestamps = true, Follow = false,
                Tail = tail.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, output, timeout.Token);
            return Ok(new { text = output.ToString(), output.Truncated, state = inspect.State.Status,
                exitCode = inspect.State.ExitCode, oomKilled = inspect.State.OOMKilled, error = inspect.State.Error });
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        { return NotFound(new { message = "Docker no longer has this container; its runtime logs were removed with it." }); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return StatusCode(504, new { message = "Docker log request timed out. Try a smaller tail." }); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read runtime logs for challenge {ChallengeId}, container {ContainerId}", cId, containerId);
            return StatusCode(502, new { message = "Docker could not provide logs. Check the daemon connection and logging driver." });
        }
    }

    internal sealed class BoundedLog : IProgress<string>
    {
        private readonly StringBuilder buffer = new();
        internal bool Truncated { get; private set; }
        public void Report(string value)
        {
            lock (buffer)
            {
                var remaining = 65536 - buffer.Length;
                if (value.Length > remaining) Truncated = true;
                buffer.Append(value.AsSpan(0, Math.Min(value.Length, remaining)));
            }
        }
        public override string ToString() { lock (buffer) return buffer.ToString(); }
    }
}
