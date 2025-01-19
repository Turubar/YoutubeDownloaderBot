using Microsoft.Extensions.Options;
using WTelegram;

namespace YoutubeDownloaderBotWH.Services
{
    public class TelegramApiService(
        IOptions<TelegramBotConfiguration> _telegramConfiguration,
        IConfiguration _configuration)
    {
        private Client? _client;

        public async Task Authorization()
        {
            int apiId = _telegramConfiguration.Value.ApiId;
            string apiHash = _telegramConfiguration.Value.ApiHash;
            string? phoneNumber = _telegramConfiguration.Value.PhoneNumber;

            string? pathToSession = _configuration.GetSection("PathToSession").Value;

            _client = new Client(apiId, apiHash, pathToSession);

            while (_client.User == null)
            {
                switch (await _client.Login(phoneNumber))
                {
                    case "verification_code": 
                        Console.Write("Введите код: ");
                        phoneNumber = Console.ReadLine();
                        break;
                    default:
                        phoneNumber = null;
                        break;
                }
            }
        }

        public Client? GetClient()
        {
            return _client;
        }
    }
}
