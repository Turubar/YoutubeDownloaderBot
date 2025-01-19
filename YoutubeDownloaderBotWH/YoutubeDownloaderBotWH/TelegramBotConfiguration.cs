namespace YoutubeDownloaderBotWH
{
    public class TelegramBotConfiguration
    {
        public const string TelegramBot = nameof(TelegramBot);
        public string Token { get; init; } = default!;

        public string Url { get; init; } = default!;

        public string Secret { get; init; } = default!;

        public int ApiId { get; init; } = default!;

        public string ApiHash { get; init; } = default!;

        public string PhoneNumber { get; init; } = default!;

        public string BotName { get; init; } = default!;

        public string UserName { get; init; } = default!;
    }
}
