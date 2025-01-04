using Microsoft.Extensions.Options;
using Telegram.Bot;
using YoutubeDownloaderBotWH;

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

builder.Services.AddSingleton<YoutubeDownloaderBotWH.Services.UpdateHandler>();

builder.Services.ConfigureTelegramBotMvc();

services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
