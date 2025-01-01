using YoutubeDownloaderBot;
using YoutubeDownloaderBot.Options;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;

services.AddHostedService<TelegramBotBackgroundService>();

services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.Telegram));

var host = builder.Build();

host.Run();