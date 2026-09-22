using System.Security.Claims;
using GZCTF.Middlewares;
using GZCTF.Services;
using GZCTF.Services.Cache;
using Microsoft.AspNetCore.Mvc;

namespace GZCTF.Controllers;

[ApiController, RequireAdmin]
[Route("api/edit/Games/{id:int}/Submissions")]
public class SubmissionDeletionController(AppDbContext db, CacheHelper cache,
    ILogger<SubmissionDeletionController> logger) : ControllerBase
{
    [HttpDelete("{submissionId:int}")]
    public async Task<IActionResult> Delete(int id, int submissionId, CancellationToken token)
    {
        var result = await SubmissionDeletion.DeleteAsync(db, id, submissionId, token);
        if (result == SubmissionDeletion.Result.NotFound) return NotFound();
        if (result == SubmissionDeletion.Result.Pending)
            return Conflict(new RequestResponse("This submission is still being judged. Wait for its result and try again."));
        logger.LogInformation("Admin {AdminId} deleted submission {SubmissionId} in game {GameId}",
            User.FindFirstValue(ClaimTypes.NameIdentifier), submissionId, id);
        // Both live and frozen boards rebuild from FirstSolves, including dynamic values and bloods.
        // Complete invalidation even if the HTTP client disconnects after the database commit.
        await cache.FlushScoreboardCache(id, CancellationToken.None);
        return NoContent();
    }
}
