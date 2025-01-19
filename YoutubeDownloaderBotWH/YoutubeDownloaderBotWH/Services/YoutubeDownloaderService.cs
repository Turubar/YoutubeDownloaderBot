using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TL;
using VideoLibrary;

namespace YoutubeDownloaderBotWH.Services
{
    public class YoutubeDownloaderService(
        YoutubeService _youtubeService,
        TelegramApiService _telegramApiService,
        IOptions<TelegramBotConfiguration> _telegramConfiguration,
        IConfiguration _configuration,
        ILogger<YoutubeDownloaderService> _logger)
    {
        public const long MAX_FILE_SIZE_IN_BYTES = 1_610_612_736;

        public async Task<IEnumerable<YouTubeVideo>?> GetVideoOptions(string url)
        {
            IEnumerable<YouTubeVideo>? options = null;

            bool flag = true;
            int numberAttempts = 0;

            while (flag)
            {
                if (numberAttempts < 10)
                {
                    try
                    {
                        _logger.LogInformation("Получение опций");
                        options = await _youtubeService.GetAllVideosAsync(url);

                        if (options.Count() >= 1)
                            return options;
                        else
                            continue;

                    }
                    catch
                    {
                        numberAttempts++;
                    }
                }
                else
                {
                    flag = false;
                }
            }

            return null;
        }

        public InlineKeyboardMarkup GetKeyboardMarkup(IEnumerable<YouTubeVideo> options)
        {
            var buttons = new List<List<InlineKeyboardButton>>();

            var audio = options
                .FirstOrDefault(a => a.AudioFormat == AudioFormat.Aac && a.AudioBitrate == options.Max(o => o.AudioBitrate));

            long? audioSize = 0;
            if (audio != null)
            {
                if (audio.ContentLength < MAX_FILE_SIZE_IN_BYTES)
                {
                    audioSize = audio.ContentLength;

                    buttons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton
                        .WithCallbackData($"🎧 Mp3 / {GetFileSize(audio.ContentLength)}", $"audio|{audio.AudioBitrate}")
                    });
                }
            }

            var videos = options
                .Where(v => v.AdaptiveKind == AdaptiveKind.Video && v.Format == VideoFormat.Mp4 && v.Fps == 30)
                .GroupBy(r => r.Resolution)
                .Select(s => s.OrderByDescending(v => v.ContentLength).First());

            if (videos.Count() > 0)
            {
                foreach (var video in videos)
                {
                    if (video.ContentLength + audioSize < MAX_FILE_SIZE_IN_BYTES)
                    {
                        buttons.Add(new List<InlineKeyboardButton>
                        {
                            InlineKeyboardButton
                            .WithCallbackData($"🎥 {video.Resolution} {video.Format} / {GetFileSize(video.ContentLength + audioSize)}", $"video|{video.Resolution}")
                        });
                    }
                }
            }

            InlineKeyboardMarkup inlineKeyboard = new(buttons);
            return inlineKeyboard;
        }

        public string GetFileSize(long? size)
        {
            double sizeB = Convert.ToDouble(size);

            double sizeKB = sizeB / 1024;
            double sizeMB = sizeKB / 1024;
            double sizeGB = sizeMB / 1024;

            if (sizeGB >= 1)
                return $"{sizeGB:F2} ГБ";

            else if (sizeMB >= 1)
                return $"{sizeMB:F2} MБ";

            else
                return $"{sizeKB:F2} KБ";
        }

        public async Task<Result<string>> GetFileFromUrl(string url, string[] info, string? pathToDirectory)
        {
            _logger.LogInformation("Начало скачивания файла");
            string kindFile = info[0];
            int bitRes = Convert.ToInt32(info[1]);

            if (string.IsNullOrEmpty(pathToDirectory))
                return Result.Failure<string>("Не удалось найти путь к директории!");

            var options = await GetVideoOptions(url);
            if (options == null)
                return Result.Failure<string>("Не удалось получить доступ к видео!");

            var audioFile = options
                .FirstOrDefault(a => a.AdaptiveKind == AdaptiveKind.Audio && a.AudioFormat == AudioFormat.Aac && a.AudioBitrate == options.Max(a => a.AudioBitrate));

            string? ffmpegPath = _configuration.GetSection("PathToFfmpeg").Value;

            if (kindFile == "audio")
            {
                if (audioFile == null)
                    return Result.Failure<string>("Не удалось найти аудиодорожку!");

                _logger.LogInformation("Аудио файл найден!");

                string nameAudioFile = audioFile.FullName;

                string pathToAudioFile = Path.Combine(pathToDirectory, nameAudioFile);
                string pathToMp3File = pathToAudioFile + ".mp3";

                try
                {
                    await _youtubeService.CreateDownloadAsync(
                        new Uri(audioFile.Uri),
                        pathToAudioFile,
                        new Progress<Tuple<long, long>>());
                }
                catch
                {
                    return Result.Failure<string>("Не удалось скачать аудидорожку!");
                }

                string audioArguments = $"-i \"{pathToAudioFile}\" -codec:a mp3 \"{pathToMp3File}\"";

                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = ffmpegPath;
                        process.StartInfo.Arguments = audioArguments;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        //process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                        //process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        await process.WaitForExitAsync();
                    }

                    if (File.Exists(pathToAudioFile))
                        File.Delete(pathToAudioFile);

                    _logger.LogInformation($"Аудио файл доступен по пути [{pathToMp3File}]");
                    return Result.Success(pathToMp3File);
                }
                catch
                {
                    return Result.Failure<string>("Ошибка конвертации аудидорожки!");
                }
            }
            else
            {
                var videoFile = options
                    .Where(v => v.AdaptiveKind == AdaptiveKind.Video && v.Format == VideoFormat.Mp4 && v.Fps == 30 && v.Resolution == bitRes)
                    .OrderByDescending(s => s.ContentLength)
                    .FirstOrDefault();

                if (videoFile == null)
                    return Result.Failure<string>("Не удалось найти видео!");

                _logger.LogInformation("Видео файл найден");

                string nameVideoFile = Guid.NewGuid().ToString() + videoFile.FileExtension;
                string pathToVideoFile = Path.Combine(pathToDirectory, nameVideoFile);

                string nameMp4File = videoFile.FullName;
                string pathToMp4File = Path.Combine(pathToDirectory, nameMp4File);

                if (audioFile == null)
                {
                    _logger.LogInformation("Аудио файл не найден!");

                    try
                    {
                        await _youtubeService.CreateDownloadAsync(
                            new Uri(videoFile.Uri),
                            pathToMp4File,
                            new Progress<Tuple<long, long>>());
                    }
                    catch
                    {
                        return Result.Failure<string>("Не удалось скачать видео!");
                    }

                    _logger.LogInformation($"Видео файл без звука доступен по пути [{pathToMp4File}]");

                    return Result.Success(pathToMp4File);
                }
                else
                {
                    string pathToAudioFile = Path.Combine(pathToDirectory, audioFile.FullName);

                    _logger.LogInformation("Аудио файл найден!");

                    try
                    {
                        await _youtubeService.CreateDownloadAsync(
                            new Uri(videoFile.Uri),
                            pathToVideoFile,
                            new Progress<Tuple<long, long>>());
                    }
                    catch
                    {
                        return Result.Failure<string>("Не удалось скачать видео!");
                    }

                    try
                    {
                        await _youtubeService.CreateDownloadAsync(
                            new Uri(audioFile.Uri),
                            pathToAudioFile,
                            new Progress<Tuple<long, long>>());
                    }
                    catch
                    {
                        return Result.Failure<string>("Не удалось скачать аудидорожку!");
                    }

                    var mergeArguments = $"-i \"{pathToVideoFile}\" -i \"{pathToAudioFile}\" -c:v copy -c:a aac -strict experimental \"{pathToMp4File}\"";

                    try
                    {
                        using (var process = new Process())
                        {
                            process.StartInfo.FileName = ffmpegPath;
                            process.StartInfo.Arguments = mergeArguments;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;

                            //process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                            //process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();
                        }
                    }
                    catch
                    {
                        return Result.Failure<string>("Ошибка конвертации видео!");
                    }

                    if (File.Exists(pathToVideoFile))
                        File.Delete(pathToVideoFile);

                    if (File.Exists(pathToAudioFile))
                        File.Delete(pathToAudioFile);

                    _logger.LogInformation($"Видео файл со звуком доступен по пути [{pathToMp4File}]");

                    return Result.Success(pathToMp4File);
                }
            }
        }

        public async Task<Result<string>> UploadFileToBot(string pathToFile, long chatId)
        {
            var client = _telegramApiService.GetClient();

            if (client == null)
            {
                await _telegramApiService.Authorization();
                client = _telegramApiService.GetClient();
            }

            if (client == null)
                return Result.Failure<string>("Не удалось подключиться к клиенту!");

            _logger.LogInformation("Начало загрузки файла в телеграм");

            string botName = _telegramConfiguration.Value.BotName; 

            try
            {
                var inputFile = await client.UploadFileAsync(pathToFile);

                var dialogs = await client.Messages_GetAllDialogs();
                InputPeer inputPeer = dialogs.users.FirstOrDefault(u => u.Value.MainUsername == botName).Value;

                await client.SendMediaAsync(inputPeer, $"{chatId}", inputFile);
            }
            catch
            {
                return Result.Failure<string>("Не удалось загрузить видео!");
            }

            if (File.Exists(pathToFile))
                File.Delete(pathToFile);

            _logger.LogInformation("Файл загружен в телеграм!");

            return Result.Success("");
        }
    }
}
