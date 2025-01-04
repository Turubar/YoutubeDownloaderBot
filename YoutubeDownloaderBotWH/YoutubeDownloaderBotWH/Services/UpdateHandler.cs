using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace YoutubeDownloaderBotWH.Services
{
    public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger) : IUpdateHandler
    {
        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            logger.LogInformation("HandleError: {Exception}", exception);
            
            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await (update switch
            {
                { Message: { } message } => OnMessage(message),
                _ => UnknownUpdateHandlerAsync(update)
            });
        }

        private async Task OnMessage(Message msg)
        {
            logger.LogInformation("Receive message type: {MessageType}", msg.Type);

            if (msg.Text is not { } messageText)
                return;

            if (messageText.StartsWith("/start"))
            {
                await bot.SendMessage(msg.Chat.Id, "Введите ссылку на видео YouTube:");
            }
            else if (Uri.IsWellFormedUriString(messageText, UriKind.Absolute))
            {
                // Здесь вы можете добавить логику для получения доступных качеств видео
                var videoQualities = new[] { "1080", "720", "360" }; // Пример доступных качеств
                var keyboard = new InlineKeyboardMarkup(videoQualities.Select(q => InlineKeyboardButton.WithCallbackData(q)).ToArray());

                await bot.SendMessage(msg.Chat.Id, "Выберите качество видео:", replyMarkup: keyboard);
            }
            else
            {
                await bot.SendMessage(msg.Chat.Id, "Пожалуйста, введите корректную ссылку на видео YouTube.");
            }
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }
    }
}
