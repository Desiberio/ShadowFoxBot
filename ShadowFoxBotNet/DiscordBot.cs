using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ShadowFoxBotNet
{
    class DiscordBot
    {
        DiscordSocketClient _client;
        BackgroundWorker backgroundWorker;

        public DiscordBot()
        {
            var _config = new DiscordSocketConfig { MessageCacheSize = 100 };
            _client = new DiscordSocketClient(_config);
            _client.Log += Log;
            SetupBackgroundWorker();
        }

        private void SetupBackgroundWorker()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += new DoWorkEventHandler(AutomaticCheck);
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        public async void Start(string token)
        {
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += async () =>
            {
                Console.WriteLine("Bot is connected!");
                BuildCommands();
                _client.SlashCommandExecuted += SlashCommandHandler;
                backgroundWorker.RunWorkerAsync();
            };
        }

        private void AutomaticCheck(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Console.WriteLine("Worker started.");
            while (!worker.CancellationPending)
            {
                Console.WriteLine($"Check started at {DateTime.Now}");

                List<string> news = RiotGamesArticleWorker.CheckNews();
                SendMessagesInNewsChannel(news);

                YoutubeArticleWorker youtubeArticleWorker = new YoutubeArticleWorker();
                Dictionary<string, List<string>> themesAndTitles = youtubeArticleWorker.Result.ThemesAndTitles;
                if (themesAndTitles.Count != 0)
                {
                    themesAndTitles = CheckIfThemeLineWasPostedBefore(themesAndTitles);
                }
                news = youtubeArticleWorker.GetNews(themesAndTitles);

                SendMessagesInNewsChannel(news);

                /*WebsiteScraper scraper = new WebsiteScraper("https://www.surrenderat20.net/");
                List<string> tweetsURLs = scraper.GetAllTweetsURLs();*/
                //SendMessageInLoLChannel(tweetsURLs);

                Console.WriteLine($"Check performed at {DateTime.Now}");
                Thread.Sleep(TimeSpan.FromMinutes(15));
            }
        }

        private Dictionary<string, List<string>> CheckIfThemeLineWasPostedBefore(Dictionary<string, List<string>> themesAndTitles)
        {
            foreach (string theme in themesAndTitles.Keys)
            {
                if (themesAndTitles.First(x => x.Key == theme).Value.Count > 4) continue;
                var originalMessage = TryFindMessage(theme);
                if (originalMessage == null) continue;

                List<string> messages;
                themesAndTitles.Remove(theme, out messages);
                messages = YoutubeArticleWorker.FormatMessages(messages, theme, false);

                SendMessagesInNewsChannel(messages, new MessageReference(originalMessage.Id, originalMessage.Channel.Id, (ulong)GuildInfo.guildID));
            }

            return themesAndTitles;
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            Console.WriteLine($"[INFO] Slash command was executed by {command.User.Username} at {command.CreatedAt.DateTime}.");
            switch (command.Data.Name)
            {
                case "update-champions-mastery":
                    HandleUpdateChampionsMasteryCommand(command);
                    break;
            }
        }

        //TODO:refactor
        private async void HandleUpdateChampionsMasteryCommand(SocketSlashCommand command)
        {
            GoogleSpreadsheetWorker worker = new GoogleSpreadsheetWorker();
            try
            {
                var options = command.Data.Options;
                if (options.Count == 0) throw new Exception("No options provided.");
                SocketUser ownerOfLoLAccount = command.User;
                string summonerName = string.Empty;
                Region region = Region.RU;

                foreach (var option in options)
                {
                    switch (option.Name)
                    {
                        case "summoner-name":
                            summonerName = option.Value.ToString();
                            break;
                        case "discord-account":
                            ownerOfLoLAccount = (SocketUser)option.Value;
                            break;
                        case "region":
                            switch (Convert.ToInt32(option.Value.ToString()))
                            {
                                case 0: continue;
                                case 1:
                                    region = Region.EUW;
                                    break;
                                case 2:
                                    region = Region.EUNE;
                                    break;
                                case 3:
                                    region = Region.NA;
                                    break;
                                default: throw new Exception("There are no option with such value.");
                            }
                            break;
                        default:
                            throw new Exception("There are no such options.");
                    }
                };

                Summoner summoner = RiotGamesAPI.GetSummonerInfo(summonerName);
                ulong summonerDiscordID = summoner != null ? summoner.DiscordID : 0;
                if (summonerDiscordID != 0) if (summonerDiscordID != command.User.Id) throw new RiotGamesAPIException("Summoner with this name already linked with other discord profile.", ErrorCode.AlreadyOwned);
                worker.AddChampionsToSpreadsheet(summonerName, ownerOfLoLAccount.Username, region);
                RiotGamesAPI.SetSummonerInfo(summonerName, ownerOfLoLAccount.Id);

                var embedBuilder = new EmbedBuilder()
                    .WithAuthor(command.User)
                    .WithTitle("Добавление 7 рангов")
                    .WithDescription("Хатова")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();

                await command.ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = embedBuilder.Build();
                });

            }
            catch (Exception exception)
            {
                HandleException(command, exception);
            }
        }

        private static async void HandleException(SocketSlashCommand command, Exception exception)
        {
            RiotGamesAPIException riotGamesAPIException = exception.InnerException == null ?
                                                          (RiotGamesAPIException)exception :
                                                          (RiotGamesAPIException)exception.InnerException;
            if (riotGamesAPIException == null) throw new Exception("Unhandled exception occured.", exception);
            Console.WriteLine("[ERROR] " + exception.Message);
            EmbedBuilder embedBuilder;

            switch (riotGamesAPIException.Error)
            {
                case ErrorCode.NotFound:
                    embedBuilder = new EmbedBuilder()
                        .WithAuthor(command.User)
                        .WithTitle("Добавление 7 рангов")
                        .WithDescription("Призыватель с таким именем в указанном регионе не найден.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();
                    break;
                case ErrorCode.AlreadyOwned:
                    embedBuilder = new EmbedBuilder()
                        .WithAuthor(command.User)
                        .WithTitle("Добавление 7 рангов")
                        .WithDescription("Кажется призыватель с таким именем уже закреплён за другим аккаунтом. Теперь обновлять информацию может только владелец аккаунта.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();
                    break;
                default:
                    embedBuilder = new EmbedBuilder()
                        .WithAuthor(command.User)
                        .WithTitle("Добавление 7 рангов")
                        .WithDescription("Шото ужасное произошло, пинай дурака")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();
                    break;
            }

            await command.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = embedBuilder.Build();
            });
        }

        private async void DeploySlashCommands(SlashCommandBuilder[] builders)
        {
            try
            {
                foreach (var builder in builders) await _client.CreateGlobalApplicationCommandAsync(builder.Build());
            }
            catch (HttpException exception)
            {
                string json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
        }

        public async void SendMessageInNewsChannel(string message, MessageReference messageReference = null)
        {
            var channel = _client.GetChannel((ulong)GuildInfo.newsChannelID) as IMessageChannel;
            await channel.SendMessageAsync(message, messageReference: messageReference);
        }

        public async void SendMessagesInNewsChannel(List<string> messages, MessageReference messageReference = null)
        {
            var channel = _client.GetChannel((ulong)GuildInfo.newsChannelID) as IMessageChannel;
            foreach (string message in messages) await channel.SendMessageAsync(message, messageReference: messageReference);
        }

        private void BuildCommands()
        {
            List<SlashCommandBuilder> builders = new List<SlashCommandBuilder>();

            var updateChampionsMasteryCommand = new SlashCommandBuilder()
                .WithName("update-champions-mastery")
                .WithDescription("Добавляет 7 ранги в Гугл таблицу")
                .AddOption("summoner-name", ApplicationCommandOptionType.String, "Никнейм игрока, который будет добавлен в таблицу.", isRequired: true)
                .AddOption("discord-account", ApplicationCommandOptionType.User, "Аккаунт человека в дискорде, владеющий аккаунтом (если владелец вы - оставьте поле пустым).", isRequired: false)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("region")
                    .WithDescription("Регион, на котором находится аккаунт (EUW, NA, EUNE, etc; по умолчанию - RU).")
                    .WithRequired(false)
                    .AddChoice("RU", 0)
                    .AddChoice("EUW", 1)
                    .AddChoice("EUNE", 2)
                    .AddChoice("NA", 3)
                    .WithType(ApplicationCommandOptionType.Integer));
            builders.Add(updateChampionsMasteryCommand);

            DeploySlashCommands(builders.ToArray());
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Worker was stopped.");
        }

        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        //TODO: make automatic check for new champions.
        private void UpdateChampionsList(GoogleSpreadsheetWorker spreadsheetWorker)
        {
            List<string> champions = RiotGamesAPI.GetChampionsName();
            List<object> info = champions.ToList<object>();
            spreadsheetWorker.UpdateEntries("Test!B3:B200", info);
        }

        private IMessage TryFindMessage(string messageContent)
        {
            var messages = _client.GetGuild((ulong)GuildInfo.guildID)
                                  .GetTextChannel((ulong)GuildInfo.newsChannelID)
                                  .GetMessagesAsync().FlattenAsync().Result;
            var message = messages.LastOrDefault(x => x.CleanContent.Contains(messageContent));
            if (message == default(IMessage)) return null;
            else return message;
        }
    }
}
