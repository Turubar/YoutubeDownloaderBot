using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace YoutubeDownloaderBotWH.Services
{
    public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger) : IUpdateHandler
    {
        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await (update switch
            {
                { Message: { } message } => MessageTextHandler(message, cancellationToken),
                { CallbackQuery: { } query } => CallbackQueryHandler(query, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update)
            });
        }

        private async Task MessageTextHandler(Message message, CancellationToken cancellationToken)
        {
            logger.LogInformation("Receive message type: {MessageType}", message.Type);

            if (message.Text is not { } messageText)
                return;

            if (messageText.StartsWith("/start"))
            {
                string text = 
                    "<b>Вас приветствует YoutubeDownloader!</b>\n\n" +
                    "Я умею скачивать аудио и видео файлы с Youtube.\n" +
                    "Для этого просто введите ссылку на видео.";

                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: text,
                    parseMode: ParseMode.Html);
            }
            else if (Uri.IsWellFormedUriString(messageText, UriKind.Absolute))
            {
                InlineKeyboardMarkup inlineKeyboard = new([
                    [InlineKeyboardButton.WithCallbackData("1080", "1080")],
                    [InlineKeyboardButton.WithCallbackData("720", "720")],
                    [InlineKeyboardButton.WithCallbackData("360", "360")]
                ]);

                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Выберите качество видео",
                    replyMarkup: inlineKeyboard,
                    replyParameters: message.Id);
            }
            else
            {
                await bot.SendMessage(message.Chat.Id, "Пожалуйста, введите корректную ссылку на видео YouTube.");
            }
        }

        private async Task CallbackQueryHandler(CallbackQuery query, CancellationToken cancellationToken)
        {
            await bot.SendMessage(chatId: query.From.Id, "Вы выбрали: " + query.Data);
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
            return Task.CompletedTask;
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            logger.LogInformation("HandleError: {Exception}", exception);

            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
