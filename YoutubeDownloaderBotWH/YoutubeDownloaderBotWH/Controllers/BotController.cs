﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using YoutubeDownloaderBotWH.Services;

namespace YoutubeDownloaderBotWH.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BotController(IOptions<TelegramBotConfiguration> _telegramConfiguration) : ControllerBase
    {

        [HttpGet("setWebhook")]
        public async Task<string> SetWebHook([FromServices] ITelegramBotClient bot, CancellationToken ct)
        {
            var webhookUrl = _telegramConfiguration.Value.Url;
            await bot.SetWebhook(webhookUrl, allowedUpdates: [], dropPendingUpdates: true, secretToken: _telegramConfiguration.Value.Secret, cancellationToken: ct);

            return $"Webhook set to {webhookUrl}";
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Update update, [FromServices] ITelegramBotClient bot, [FromServices] UpdateHandler handleUpdateService, CancellationToken ct)
        {
            if (Request.Headers["X-Telegram-Bot-Api-Secret-Token"] != _telegramConfiguration.Value.Secret)
                return Forbid();

            try
            {
                await handleUpdateService.HandleUpdateAsync(bot, update, ct);
            }
            catch (Exception exception)
            {
                await handleUpdateService.HandleErrorAsync(bot, exception, Telegram.Bot.Polling.HandleErrorSource.HandleUpdateError, ct);
            }

            return Ok();
        }
    }
}
