using System.Text.Json;
using GZCTF.Middlewares;
using GZCTF.Models.Request.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Controllers;

[ApiController, Route("api/edit/Games/{id:int}/Discord"), RequireGameAdmin]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DiscordSettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int id, CancellationToken token)
    {
        if (!await db.Games.AnyAsync(g => g.Id == id, token)) return NotFound();
        return Ok(await DiscordSettings.Read(db, id, token));
    }

    [HttpPut]
    public async Task<IActionResult> Save(int id, DiscordSettings model, CancellationToken token)
    {
        if (!await db.Games.AnyAsync(g => g.Id == id, token)) return NotFound();
        var key = DiscordSettings.Key(id);
        var row = await db.Configs.SingleOrDefaultAsync(c => c.ConfigKey == key, token);
        var json = JsonSerializer.Serialize(model, DiscordSettings.Json);
        if (row is null) db.Configs.Add(new Models.Data.Config(key, json));
        else row.Value = json;
        await db.SaveChangesAsync(token);
        return Ok(model);
    }
}
