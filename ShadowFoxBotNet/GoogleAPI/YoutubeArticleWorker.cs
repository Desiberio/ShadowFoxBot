using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShadowFoxBotNet
{
    class YoutubeArticleWorker
    {
        readonly YouTubeService _youTubeService;
        public SearchResultProcessor Result { get; private set; }
        public YoutubeArticleWorker(string dateTime = null)
        {
            string apiKey = File.ReadAllText("./apiKeys/youtubeAPIKey.txt");
            if (string.IsNullOrWhiteSpace(apiKey)) throw new Exception("Youtube API Key is null.");

            _youTubeService = new YouTubeService(new BaseClientService.Initializer() { ApiKey = apiKey });
            Check(dateTime);
        }

        string FormatDateTime(DateTime checkDateTime)
        {
            checkDateTime = checkDateTime.AddHours(-3);
            string output = checkDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            output = output.Remove(output.IndexOf('.'));
            output += "Z";

            return output;
        }

        private void Check(string lastCheckDateTime = null)
        {
            List<SearchResult> searchResults = lastCheckDateTime == null ?
                                               Search(FormatDateTime(GetLastCheckDateTime())).Result :
                                               Search(lastCheckDateTime).Result;

            //File.WriteAllText("./testss.json", JsonConvert.SerializeObject(searchResults)); //TODO: Check if was posted before.

            Result = new SearchResultProcessor(searchResults);
        }

        private async Task<List<SearchResult>> Search(string lastCheckDateTime)
        {
            List<SearchResult> output = new List<SearchResult>();
            
            string nextpagetoken = "";
            while (nextpagetoken != null)
            {
                var search = _youTubeService.Search.List("snippet");
                search.ChannelId = "UC0NwzCHb8Fg89eTB5eYX17Q";
                search.MaxResults = 50;
                search.Type = "video";
                search.PublishedAfter = lastCheckDateTime;
                search.PageToken = nextpagetoken;
                // Call the search.list method to retrieve results matching the specified query term.
                var searchListResponse = await search.ExecuteAsync();

                output.AddRange(searchListResponse.Items);

                nextpagetoken = searchListResponse.NextPageToken;
            }

            return output;
        }

        public List<string> GetNews(Dictionary<string, List<string>> themesAndTitles = null)
        {
            if (themesAndTitles == null) themesAndTitles = Result.ThemesAndTitles;

            List<string> output = new List<string>();

            List<string> otherSkins = new List<string>();
            foreach (var themes in themesAndTitles)
            {
                if (themes.Value.Count <= 2) otherSkins.AddRange(themes.Value);
                else output.AddRange(FormatMessages(themes.Value, themes.Key));
            }

            if (otherSkins.Count > 0) output.AddRange(FormatMessages(otherSkins, "Other Skins"));
            if (Result.AbilityTitles.Count > 0) output.AddRange(FormatMessages(Result.AbilityTitles, "Ability reveal"));
            if (Result.VoiceTitles.Count > 0) output.AddRange(FormatMessages(Result.VoiceTitles, "Voice"));

            return output;
        }

        private DateTime GetLastCheckDateTime(string pathToRead = "./log.txt")
        {
            string lastRecord = File.ReadAllLines(pathToRead)[^1];
            LogCurrentDateTime();
            return Convert.ToDateTime(lastRecord);
        }

        private async void LogCurrentDateTime(string pathToSave = "./log.txt")
        {
            await File.WriteAllTextAsync(pathToSave, DateTime.Now.ToString());
        }

        public static List<string> FormatMessages(List<string> items, string theme, bool includeTitleAndTheme = true)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (includeTitleAndTheme) stringBuilder.Append($"__**SkinSpotlights**__\n"); //header
            List<string> output = new List<string>();
            int numberOfURLsInMessage = 0;

            if (includeTitleAndTheme) stringBuilder.Append($"\n__{theme}__\n\n");
            foreach (string item in items)
            {
                stringBuilder.Append(item + "\n");
                numberOfURLsInMessage++;
                if (numberOfURLsInMessage >= 5)
                {
                    output.Add(stringBuilder.ToString());
                    stringBuilder.Clear();
                    numberOfURLsInMessage = 0;
                }
            }
            if (stringBuilder.Length != 0) output.Add(stringBuilder.ToString());
            return output;
        }

    }
}
