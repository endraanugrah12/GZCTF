using GZCTF.Hubs;
using GZCTF.Hubs.Clients;
using GZCTF.Repositories.Interface;
using GZCTF.Services.Webhook;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Repositories;

public class GameNoticeRepository(
    IHubContext<UserHub, IUserClient> hub,
    ISendWebhookService webhookService,
    AppDbContext context) : RepositoryBase(context), IGameNoticeRepository
{
    public async Task<GameNotice> AddNotice(GameNotice notice, bool broadcast = true, CancellationToken token = default)
    {
        await Context.AddAsync(notice, token);
        await SaveAsync(token);

        var game = await Context.Games.AsNoTracking().SingleAsync(g => g.Id == notice.GameId, token);
        var now = DateTimeOffset.UtcNow;
        var frozen = game.FreezeTimeUtc is { } freeze && now >= freeze && now < game.EndTimeUtc;
        var blood = notice.Type is NoticeType.FirstBlood or NoticeType.SecondBlood or NoticeType.ThirdBlood;
        if (broadcast || (frozen && blood))
            await hub.Clients.Group($"Game_{notice.GameId}").ReceivedGameNotice(frozen ? notice.AnonymizeBlood() : notice);

        // Freeze suppresses the public feed, but Discord can send an anonymized blood notice.
        if (broadcast || blood)
            _ = webhookService.SendNoticeAsync(notice);

        return notice;
    }

    public Task<GameNotice[]> GetNormalNotices(int gameId, CancellationToken token = default) =>
        Context.GameNotices
            .Where(n => n.GameId == gameId && n.Type == NoticeType.Normal)
            .ToArrayAsync(token);

    public Task<GameNotice?> GetNoticeById(int gameId, int noticeId, CancellationToken token = default) =>
        Context.GameNotices.FirstOrDefaultAsync(e => e.Id == noticeId && e.GameId == gameId, token);

    public async Task<DataWithModifiedTime<GameNotice[]>> GetLatestNotices(int gameId, CancellationToken token = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Read from the database so newly-created blood notices are visible even
        // if distributed-cache invalidation is unavailable.
        var notices = await Context.GameNotices
            .AsNoTracking()
            .Where(e => e.GameId == gameId &&
                        (e.Type != NoticeType.Normal || e.PublishTimeUtc <= now))
            .OrderByDescending(e => e.PublishTimeUtc)
            .Take(300)
            .ToArrayAsync(token);

        return new DataWithModifiedTime<GameNotice[]>(notices, now);
    }

    public async Task RemoveNotice(GameNotice notice, CancellationToken token = default)
    {
        Context.Remove(notice);
        await SaveAsync(token);
    }

    public async Task<GameNotice> UpdateNotice(GameNotice notice, CancellationToken token = default)
    {
        await SaveAsync(token);
        return notice;
    }
}
