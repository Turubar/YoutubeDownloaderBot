namespace YoutubeDownloaderBotWH
{
    public class TelegramBotConfiguration
    {
        public const string TelegramBot = nameof(TelegramBot);
        public string Token { get; init; } = default!;

        public string Url { get; init; } = default!;

        public string Secret { get; init; } = default!;
    }
}
