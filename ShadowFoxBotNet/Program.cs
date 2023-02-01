using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Discord.Webhook;
using Discord;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections;
using Discord.Net;

namespace ShadowFoxBotNet
{
    class Program
    {
        private static DiscordSocketClient _client;
        public List<Article> articles;
        BackgroundWorker backgroundWorker;
        //const ulong newsChannelID = 887232193376702474; //debug
        const ulong newsChannelID = 436522034231640064;
        const ulong generalLoLChannelID = 887232193376702474;
        //const ulong guildID = 421901237257109523; //debug
        const ulong guildID = 338355570669256705;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            string token = File.ReadAllText("./apiKeys/discordBotToken.txt");
            StartBot(token);
            //Block this task until program is closed.
            await Task.Delay(-1);
        }

        private void UpdateChampionsList(GoogleSpreadsheetWorker spreadsheetWorker)
        {
            List<string> champions = RiotGamesAPI.GetChampionsName();
            List<object> info = champions.ToList<object>();
            spreadsheetWorker.UpdateEntries("Test!B3:B200", info);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Worker was stopped.");
        }

        private async Task<string> GetInputAsync()
        {
            return await Task.Run(() => Console.ReadLine());
        }

        public async void AutomaticCheck(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Console.WriteLine("Worker started.");
            int delay = (int)TimeSpan.FromMinutes(15).TotalMilliseconds;  //15 minutes
            while (!worker.CancellationPending)
            {
                Console.WriteLine($"Check performed at {DateTime.Now}");

                List<string> articles = CheckNews();
                articles.AddRange(CheckYT().Result);
                SendMessageInNewsChannel(articles);

                /*WebsiteScraper scraper = new WebsiteScraper("https://www.surrenderat20.net/");
                List<string> tweetsURLs = scraper.GetAllTweetsURLs();*/
                //SendMessageInLoLChannel(tweetsURLs);

                Thread.Sleep(delay);
            }
        }

        private async void SendMessageInLoLChannel(List<string> messages, MessageReference messageReference = null)
        {
            var channel = _client.GetChannel(newsChannelID) as IMessageChannel;
            foreach (string message in messages) await channel.SendMessageAsync(message, messageReference: messageReference);
        }

        public async void StartBot(string token)
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            var _config = new DiscordSocketConfig { MessageCacheSize = 100 };
            _client = new DiscordSocketClient(_config);

            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += new DoWorkEventHandler(AutomaticCheck);
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;

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

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);
            Console.WriteLine($"Slash command was executed by {command.User.Username} at {command.CreatedAt.DateTime.ToString()}");
            switch (command.Data.Name)
            {
                case "update-champions-mastery":
                    await HandleUpdateChampionsMasteryCommand(command);
                    break;
            }
            await command.RespondAsync("Thonk", ephemeral: true);
        }

        private async Task HandleUpdateChampionsMasteryCommand(SocketSlashCommand command)
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
                                case 1: region = Region.EUW;
                                    break;
                                case 2: region = Region.EUNE;
                                    break;
                                case 3: region = Region.NA;
                                    break;
                                default: throw new Exception("There are no option with such value.");
                            }
                            break;
                        default:
                            throw new Exception("There are no such options.");
                    }
                };

                Summoner summoner = RiotGamesAPI.GetSummonerInfo(summonerName);
                ulong summonerDiscordID = summoner != null ? summoner.discordID : 0;
                if (summonerDiscordID != 0) if (summonerDiscordID != command.User.Id) throw new RiotGamesAPIException("Summoner with this name already linked with other discord profile.", 1322);
                worker.AddChampionsToSpreadsheet(summonerName, ownerOfLoLAccount.Username, region);
                RiotGamesAPI.SetSummonerInfo(summonerName, ownerOfLoLAccount.Id);

                var embedBuilder = new EmbedBuilder()
                    .WithAuthor(command.User)
                    .WithTitle("Добавление 7 рангов")
                    .WithDescription("Хатова")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();

                await command.ModifyOriginalResponseAsync( x =>
                {
                    x.Embed = embedBuilder.Build();
                });

            }
            catch (Exception ex)
            {
                RiotGamesAPIException exception = null;
                exception = (RiotGamesAPIException)ex.InnerException;
                if (exception == null) exception = (RiotGamesAPIException)ex;

                Console.WriteLine("[ERROR] " + exception.Message);
                EmbedBuilder embedBuilder;
                if (exception.errorCode == 404)
                {
                    embedBuilder = new EmbedBuilder()
                    .WithAuthor(command.User)
                    .WithTitle("Добавление 7 рангов")
                    .WithDescription("Призыватель с таким именем в указанном регионе не найден.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp();
                }
                if(exception.errorCode == 1322)
                {
                    embedBuilder = new EmbedBuilder()
                    .WithAuthor(command.User)
                    .WithTitle("Добавление 7 рангов")
                    .WithDescription("Кажется призыватель с таким именем уже закреплён за другим аккаунтом. Теперь обновлять информацию может только владелец аккаунта.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp();
                }
                else
                {
                    embedBuilder = new EmbedBuilder()
                    .WithAuthor(command.User)
                    .WithTitle("Добавление 7 рангов")
                    .WithDescription("Шото ужасное произошло, пинай дурака")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp();
                }

                await command.ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = embedBuilder.Build();
                });
            }
        }

        private async void DeploySlashCommands(SlashCommandBuilder[] builders)
        {
            try
            {
                foreach(var builder in builders) await _client.CreateGlobalApplicationCommandAsync(builder.Build());
            }
            catch (HttpException exception)
            {
                string json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
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

        private async void SendMessageInNewsChannel(string message, MessageReference messageReference = null)
        {
            var channel = _client.GetChannel(newsChannelID) as IMessageChannel;
            await channel.SendMessageAsync(message, messageReference: messageReference);
        }

        private async void SendMessageInNewsChannel(List<string> messages, MessageReference messageReference = null)
        {
            var channel = _client.GetChannel(newsChannelID) as IMessageChannel;
            foreach(string message in messages) await channel.SendMessageAsync(message, messageReference: messageReference);
        }

        private List<Article> GetArticles(string path)
        {
            return JsonConvert.DeserializeObject<List<Article>>(File.ReadAllText(path));
        }

        private List<Article> GetUnpublishedArticles(List<Article> articles)
        {
            List<Article> unpublishedArticles = new List<Article>();
            string root = "https://www.leagueoflegends.com/ru-ru";
            WebClient wc = new WebClient();

            string json = wc.DownloadString("https://www.leagueoflegends.com/page-data/ru-ru/news/page-data.json");
            dynamic info = JsonConvert.DeserializeObject(json);
            for (int i = 0; i < 10; i++)
            {
                string uid = info.result.data.allArticles.edges[i].node.uid;
                if (articles.Find(x => x.uid == uid) == null)
                {
                    Article article = new Article();
                    article.uid = uid;
                    article.description = info.result.data.allArticles.edges[i].node.description;
                    article.articleType = info.result.data.allArticles.edges[i].node.article_type;
                    article.title = info.result.data.allArticles.edges[i].node.title;
                    article.dateTime = info.result.data.allArticles.edges[i].node.date;
                    article.url = root + info.result.data.allArticles.edges[i].node.url.url;
                    article.banner = info.result.data.allArticles.edges[i].node.banner.url;
                    article.external_link = info.result.data.allArticles.edges[i].node.external_link;
                    article.youtube_link = info.result.data.allArticles.edges[i].node.youtube_link;
                    article.category_title = info.result.data.allArticles.edges[i].node.category[0].title;
                    unpublishedArticles.Add(article);
                }
            }

            return unpublishedArticles;
        }

        public List<string> CheckNews()
        {
            List<string> news = new List<string>();
            articles = GetArticles("./data.json");
            if (articles == null) articles = new List<Article>();

            List<Article> unpublishedArticles = GetUnpublishedArticles(articles);
            news = FormatNews(unpublishedArticles);

            SaveData(unpublishedArticles);

            return news;
        }


        private async void SaveData(List<Article> articles)
        {
            foreach (Article article in articles) article.isChecked = true;
            this.articles.AddRange(articles);

            string data = JsonConvert.SerializeObject(this.articles, Formatting.Indented);
            await File.WriteAllTextAsync("./data.json", data);
        }

        private List<string> FormatNews(List<Article> unpublishedArticles)
        {
            List<string> news = new List<string>();

            string result = string.Empty;
            string htmlbody = string.Empty;
            Regex regexImage = new Regex("<a class=.skins cboxElement. href=.(.*?)\""); //first group

            foreach (Article article in unpublishedArticles)
            {
                result = string.Empty;
                if (article.url.Contains("teamfight") && article.articleType != "Youtube") result += $"__**{article.category_title}**__\n\n__{article.title}__\n\n{article.description.Trim('\n')}\n{article.url.Trim('\n')}";
                else if (article.url.Contains("patch"))
                {
                    WebClient wc = new WebClient();
                    htmlbody = wc.DownloadString(article.url);

                    Match match = regexImage.Match(htmlbody); //url for picture
                    article.addition = match.Groups[1].Value;

                    MatchCollection headers = Regex.Matches(htmlbody, "<h2 id=.(.*?).>(.*?)</h2>");
                    List<string> highlights = new List<string>();
                    foreach (Match header in headers) if (header.Groups[2].Value != null || header.Groups[2].Value != "") highlights.Add(header.Groups[2].Value);

                    result += $"__**{article.category_title}**__\n\n__{article.title}__\n\n{article.description.Trim('\n')}" + string.Join("\n\\> ", highlights) + $"\n<{article.url.Trim('\n')}>";
                }
                else if (article.articleType == "Youtube") result += $"__**{article.category_title}**__\n\n__{article.title}__\n\n{article.description.Trim('\n')}\n{article.youtube_link.Trim('\n')}";
                else if (article.articleType == "External Link") result += $"__**{article.category_title}**__\n\n__{article.title}__\n\n{article.description.Trim('\n')}\n{article.external_link.Trim('\n')}";
                else result += $"__**{article.category_title}**__\n\n__{article.title}__\n\n{article.description.Trim('\n')}\n{article.url.Trim('\n')}";

                if (result != string.Empty) news.Add(result);
                if (article.addition != string.Empty) news.Add(article.addition);
            }

            return news;
        }

        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public async Task<List<string>> CheckYT()
        {
            List<SearchResult> res = new List<SearchResult>();
            string apiKey = File.ReadAllText("./apiKeys/youtubeAPIKey.txt");
            YouTubeService yt = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });

            string lastRecord = File.ReadAllLines("./log.txt")[File.ReadAllLines("./log.txt").Length - 1];
            File.WriteAllText("./log.txt", DateTime.Now.ToString());
            DateTime checkDateTime = Convert.ToDateTime(lastRecord);
            checkDateTime = checkDateTime.AddHours(-3);
            string checkDate = checkDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            checkDate = checkDate.Remove(checkDate.IndexOf('.'));
            checkDate += "Z";

            string nextpagetoken = "";
            while (nextpagetoken != null)
            {
                var search = yt.Search.List("snippet");
                search.ChannelId = "UC0NwzCHb8Fg89eTB5eYX17Q";
                search.MaxResults = 50;
                search.Type = "video";
                search.PublishedAfter = checkDate;
                search.PageToken = nextpagetoken;
                // Call the search.list method to retrieve results matching the specified query term.
                var searchListResponse = await search.ExecuteAsync();

                // Process  the video responses 
                res.AddRange(searchListResponse.Items);

                nextpagetoken = searchListResponse.NextPageToken;
            }

            if (res.Count == 0) return new List<string>();


            File.WriteAllText("./testss.json" ,JsonConvert.SerializeObject(res)); //TODO: Check if was posted before.

            return FormatResults(res);
        }

        int numberOfURLsInMessage = 0;
        private List<string> FormatResults(List<SearchResult> results, bool wasChecked = false)
        {
            List<string> allTitles = new List<string>();
            List<string> skinTitles = new List<string>();
            List<string> voiceTitles = new List<string>();
            List<string> abilityTitles = new List<string>();

            string root = "https://youtu.be/";
            foreach (SearchResult video in results)
            {
                string title = WebUtility.HtmlDecode(video.Snippet.Title); //WebUtility.HtmlDecode() method replaces all strange characters for its values
                if (!title.Contains("Preview")) continue;
                if (!title.Contains("Voice"))
                {
                    int index = Compare(title.IndexOf('-') - 1, title.IndexOf('|') - 1);
                    if (index > 0) title = title.Remove(index);
                }
                else continue;

                title += " — " + root + video.Id.VideoId;

                if (title.Contains("Skin") || title.Contains("Chroma")) skinTitles.Add(title);
                else if (title.Contains("Voice")) voiceTitles.Add(title); //if-else structure looks weird. Maybe it can be improved via switch-case statement?
                else if (title.Contains("Ability")) abilityTitles.Add(title);
            }

            int allVideosCountNumber = skinTitles.Count + voiceTitles.Count + abilityTitles.Count;
            if (allVideosCountNumber == 0) return new List<string>();

            List<string> result = new List<string>();

            Dictionary<string, List<string>> themesAndTitles = new Dictionary<string, List<string>>();
            RiotGamesAPI riotGamesAPI = new RiotGamesAPI();
            List<string> championsName = RiotGamesAPI.GetChampionsName();
            foreach(string title in skinTitles)
            {
                int indexOfChampion = championsName.FindLastIndex(x => title.Contains(x));
                string championName = indexOfChampion > 0 ? championsName[indexOfChampion] : null;
                string themeName = "Other Skins";
                if (championName != null) 
                {
                    int index = title.IndexOf(championName) - 1;
                    if (index > 0) themeName = title.Remove(index);
                    else themeName = title.Split(' ')[0];
                    if (themeName.Contains("Prestige")) themeName = themeName.Remove(themeName.IndexOf("Prestige"), 8).Trim();
                }

                if (themesAndTitles.ContainsKey(themeName)) 
                {
                    themesAndTitles.First(x => x.Key == themeName).Value.Add(title);
                    continue;
                }
                themesAndTitles.TryAdd(themeName, new List<string>() { title });
            }


            foreach (string theme in themesAndTitles.Keys)
            {
                if (themesAndTitles.First(x => x.Key == theme).Value.Count > 4) break;
                var originalMessage = TryFindMessage(theme);
                if (originalMessage == null) continue;

                List<string> messages = FormatMessages(themesAndTitles.First(x => x.Key == theme).Value, theme, false);
                SendMessageInNewsChannel(messages, new MessageReference(originalMessage.Id, originalMessage.Channel.Id, guildID));
                return new List<string>(); //crutch
            }


            List<string> otherSkins = new List<string>();
            foreach(var themes in themesAndTitles)
            {
                if (themes.Value.Count <= 2) otherSkins.AddRange(themes.Value);
                else result.AddRange(FormatMessages(themes.Value, themes.Key));
            }

            if(otherSkins.Count > 0) result.AddRange(FormatMessages(otherSkins, "Other Skins"));
            if (abilityTitles.Count != 0) result.AddRange(FormatMessages(abilityTitles, "Ability reveal"));
            if (voiceTitles.Count != 0) result.AddRange(FormatMessages(voiceTitles, "Voice"));
            numberOfURLsInMessage = 0;

            return result;
        }

        private List<string> FormatMessages(List<string> items, string theme, bool includeTitleAndTheme = true)
        {
            string message = "";
            if(includeTitleAndTheme) message += $"__**SkinSpotlights**__\n"; //header
            List<string> result = new List<string>();

            if(includeTitleAndTheme) message += $"\n__{theme}__\n\n";
            foreach (string item in items)
            {
                message += item + "\n";
                numberOfURLsInMessage++;
                if (numberOfURLsInMessage >= 5)
                {
                    result.Add(message);
                    message = "";
                    numberOfURLsInMessage = 0;
                }
            }
            if (!string.IsNullOrEmpty(message) || message != "") result.Add(message);
            numberOfURLsInMessage = 0;
            return result;
        }

        private IMessage TryFindMessage(string messageContent)
        {
            var messages = _client.GetGuild(guildID).GetTextChannel(newsChannelID).GetMessagesAsync().ToArrayAsync().Result.First();
            var message = messages.LastOrDefault(x => x.CleanContent.Contains(messageContent));
            if (message == default(IMessage)) return null;
            else return message;
        }

        private int Compare(int i1, int i2)
        {
            if (i1 > 0 && i2 > 0) return Math.Min(i1, i2);
            else if (i1 > 0) return i1;
            else return i2;
        }
    }
}
