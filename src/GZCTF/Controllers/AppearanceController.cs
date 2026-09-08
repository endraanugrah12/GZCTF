using System.Text.Json;
using GZCTF.Middlewares;
using GZCTF.Models.Request.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Controllers;

[ApiController]
[Route("api/appearance")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AppearanceController(AppDbContext db) : ControllerBase
{
    private const string DraftKey = "Appearance:Draft";
    private const string PublishedKey = "Appearance:Published";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<AppearanceModel> Read(string key, CancellationToken token)
    {
        var value = await db.Configs.Where(c => c.ConfigKey == key).Select(c => c.Value).SingleOrDefaultAsync(token);
        return string.IsNullOrEmpty(value) ? new() : JsonSerializer.Deserialize<AppearanceModel>(value, Json) ?? new();
    }

    private async Task Write(string key, AppearanceModel model, CancellationToken token)
    {
        var config = await db.Configs.SingleOrDefaultAsync(c => c.ConfigKey == key, token);
        var value = JsonSerializer.Serialize(model, Json);
        if (config is null) db.Configs.Add(new Models.Data.Config(key, value));
        else config.Value = value;
    }

    [HttpGet]
    public Task<AppearanceModel> Published(CancellationToken token) => Read(PublishedKey, token);

    [RequireAdmin]
    [HttpGet("editor")]
    public async Task<IActionResult> Editor(CancellationToken token)
        => Ok(new { draft = await Read(DraftKey, token), published = await Read(PublishedKey, token) });

    [RequireAdmin]
    [HttpPut("draft")]
    public async Task<IActionResult> SaveDraft(AppearanceModel model, CancellationToken token)
    {
        await Write(DraftKey, model, token);
        await db.SaveChangesAsync(token);
        return Ok(model);
    }

    [RequireAdmin]
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(AppearanceModel model, CancellationToken token)
    {
        await Write(DraftKey, model, token);
        await Write(PublishedKey, model, token);
        await db.SaveChangesAsync(token);
        return Ok(model);
    }

    [RequireAdmin]
    [HttpDelete]
    public async Task<IActionResult> Reset(CancellationToken token)
    {
        await Write(DraftKey, new(), token);
        await Write(PublishedKey, new(), token);
        await db.SaveChangesAsync(token);
        return Ok(new AppearanceModel());
    }
}
