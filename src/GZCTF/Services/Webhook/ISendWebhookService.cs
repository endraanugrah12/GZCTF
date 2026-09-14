using GZCTF.Models.Data;

namespace GZCTF.Services.Webhook;

public interface ISendWebhookService
{
    Task SendGameEventAsync(GameEvent gameEvent);
    Task SendNoticeAsync(GameNotice notice);
}
