using Telegram.Bot;
using VideoLibrary;
using YoutubeDownloaderBotWH;
using YoutubeDownloaderBotWH.Services;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

var configSection = configuration.GetSection(TelegramBotConfiguration.TelegramBot);
services.Configure<TelegramBotConfiguration>(configSection);

builder.Services.AddHttpClient("tgwebhook")
    .RemoveAllLoggers()
    .AddTypedClient<ITelegramBotClient>(httpClient =>
    {
        return new TelegramBotClient(configSection.Get<TelegramBotConfiguration>()!.Token, httpClient);
    });

services.AddTransient<YoutubeService>();
services.AddTransient<YoutubeDownloaderService>();
services.AddSingleton<TelegramApiService>();
services.AddSingleton<UpdateHandler>();

services.ConfigureTelegramBotMvc();

services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseStaticFiles();

app.Run();
