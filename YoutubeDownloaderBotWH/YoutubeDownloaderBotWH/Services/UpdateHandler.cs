using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Options;

namespace YoutubeDownloaderBotWH.Services
{
    public class UpdateHandler(
        ITelegramBotClient _botClient,
        YoutubeDownloaderService _youtubeDownloaderService,
        IOptions<TelegramBotConfiguration> _telegramBotConfiguration,
        IConfiguration _configuration,
        ILogger<UpdateHandler> _logger): IUpdateHandler
    {
        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await (update switch
            {
                { Message: { } message } => MessageHandler(message, cancellationToken),
                { CallbackQuery: { } query } => CallbackQueryHandler(query, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update)
            });
        }

        private async Task MessageHandler(Message message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Получено сообщение");

            if (message.Type == MessageType.Text)
            {
                await MessageTextHandler(message, cancellationToken);
            }
            else if (message.Type == MessageType.Audio || message.Type == MessageType.Video || message.Type == MessageType.Document)
            {
                await MessageFileHandler(message, cancellationToken);
            }
            else
            {
                await MessageUnknownHandler(message, cancellationToken);
            }
        }

        private async Task MessageTextHandler(Message message, CancellationToken cancellationToken)
        {
            if (message.Text is not { } messageText)
                return;

            // /start
            if (messageText.StartsWith("/start"))
            {
                string text =
                    "<b>Вас приветствует YoutubeDownloader!</b>\n\n" +
                    "Я умею скачивать аудио и видео файлы с Youtube.\n" +
                    "Для этого просто введите ссылку на видео.";

                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: text,
                    parseMode: ParseMode.Html);

                return;
            }
            // ссылка на youtube и возврат доступных опций
            else if (Uri.IsWellFormedUriString(messageText, UriKind.Absolute))
            {
                var options = _youtubeDownloaderService.GetVideoOptions(messageText).Result;
                if (options == null)
                {
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Данное видео недоступно",
                        replyParameters: message.Id);

                    return;
                }

                var inlineKeyboard = _youtubeDownloaderService.GetKeyboardMarkup(options);

                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"Выберите файл, который хотите получить\n{messageText}",
                    replyMarkup: inlineKeyboard,
                    replyParameters: message.Id);

                return;
            }
            // некорректная ссылка на youtube
            else
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Пожалуйста, введите корректную ссылку на видео",
                    replyParameters: message.Id);

                return;
            }
        }

        private async Task MessageFileHandler(Message message, CancellationToken cancellationToken)
        {
            if (message.Chat.Username is not { } userName)
                return;

            string senderMessage = userName;
            string sender = _telegramBotConfiguration.Value.UserName;

            if (sender != senderMessage)
                return;

            long chatId = Convert.ToInt64(message.Caption);

            if (message.Type == MessageType.Audio)
            {
                if (message.Audio is not { } messageAudio)
                    return;

                await _botClient.SendAudio(chatId, messageAudio.FileId);
                return;
            }
            else if (message.Type == MessageType.Video)
            {
                if (message.Video is not { } messageVideo)
                    return;

                await _botClient.SendVideo(chatId, messageVideo.FileId);
                return;
            }
            else if (message.Type == MessageType.Document)
            {
                if (message.Document is not { } messageDocument)
                    return;

                await _botClient.SendDocument(chatId, messageDocument.FileId);
                return;
            }
        }

        private Task MessageUnknownHandler(Message message, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Неизвестный тип сообщения: {message.Type}");
            return Task.CompletedTask;
        }


        private async Task CallbackQueryHandler(CallbackQuery query, CancellationToken cancellationToken)
        {
            if (query.Message is not { } message)
                return;

            if (query.Data is not { } queryData)
                return;

            if (message.ReplyToMessage is not { } reply)
                return;

            if (reply.Text is not { } text)
                return;

            string url = text;
            string[] info = queryData.Split("|");

            string? pathToDirectory = _configuration.GetSection("PathToFiles").Value;

            var pathToFile = await _youtubeDownloaderService.GetFileFromUrl(url, info, pathToDirectory);
            if (pathToFile.IsFailure)
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: pathToFile.Value,
                    replyParameters: message.Id);

                return;
            }

            await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Файл скоро будет загружен, нужно еще немного подождать...",
                    replyParameters: message.Id);

            var upload = await _youtubeDownloaderService.UploadFileToBot(pathToFile.Value, message.Chat.Id);
            if (upload.IsFailure)
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: pathToFile.Value,
                    replyParameters: message.Id);

                return;
            }
        }



        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation($"Неизвестный тип обновления: {update.Type}");
            return Task.CompletedTask;
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Ошибка обновления: {exception}");

            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
