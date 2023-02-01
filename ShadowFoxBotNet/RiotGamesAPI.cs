using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Dynamic;
using Newtonsoft.Json.Converters;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;

namespace ShadowFoxBotNet
{
    class RiotGamesAPI
    {
        private string apiKey = File.ReadAllText("./apiKeys/riotGamesAPIKey.txt");
        private static Dictionary<long, string> championsIDs = new Dictionary<long, string>();
        private static Dictionary<string, List<Champion>> summonersMastery = new Dictionary<string, List<Champion>>();
        private static HttpClient httpClient = null;
        private static List<Summoner> summoners = new List<Summoner>();

        public RiotGamesAPI() 
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("X-Riot-Token", apiKey);
            if(championsIDs.Count == 0) FillChampionsInfo();
            if (summoners.Count == 0) FillSummonersData();
        }

        private static void FillSummonersData()
        {
            string json = null;
            if (File.Exists("./summonersMasteryData.json")) json = File.ReadAllText("./summonersMasteryData.json");
            if (!string.IsNullOrEmpty(json)) summonersMastery = JsonConvert.DeserializeObject<Dictionary<string, List<Champion>>>(json);
            if (File.Exists("./summonersData.json")) json = File.ReadAllText("./summonersData.json");
            if (!string.IsNullOrEmpty(json)) summoners = JsonConvert.DeserializeObject<List<Summoner>>(json);
        }

        public static List<string> GetChampionsName()
        {
            List<string> champions = new List<string>();

            string jsonFile = GetChampionsData();
            dynamic json = JsonConvert.DeserializeObject<ExpandoObject>(jsonFile, new ExpandoObjectConverter());
            foreach (var champion in json.data) champions.Add(champion.Value.name);

            return champions;
        }

        private void FillChampionsInfo()
        {
            string jsonFile = GetChampionsData();
            dynamic json = JsonConvert.DeserializeObject<ExpandoObject>(jsonFile, new ExpandoObjectConverter());
            foreach (var champion in json.data)
            {
                championsIDs.Add(Convert.ToInt64(champion.Value.key), champion.Value.name);
            }
        }

        private static string GetChampionsData()
        {
            WebClient webClient = new WebClient();
            dynamic versions = JsonConvert.DeserializeObject(webClient.DownloadString("https://ddragon.leagueoflegends.com/api/versions.json"));
            string newestVersion = versions[0];
            string championsJSONUrl = $"http://ddragon.leagueoflegends.com/cdn/{newestVersion}/data/en_US/champion.json";
            return webClient.DownloadString(championsJSONUrl);
        }

        public async Task<string> GetSummonerIdByName(string name, Region region = Region.RU)
        {
            string summonerID = null;
            Summoner currentSummoner = summoners.Find(x => x.name == name);
            if (currentSummoner != null) return currentSummoner.id;

            string url = string.Empty;
            switch (region)
            {
                case Region.RU:
                    url = $"https://ru.api.riotgames.com/lol/summoner/v4/summoners/by-name/{name}";
                    break;
                case Region.EUW:
                    url = $"https://euw1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{name}";
                    break;
                case Region.EUNE:
                    url = $"https://eun1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{name}";
                    break;
                case Region.NA:
                    url = $"https://na1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{name}";
                    break;
            }

            HttpResponseMessage response = null;
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Get;
                response = await httpClient.SendAsync(request);
            }

            if (response.StatusCode == HttpStatusCode.NotFound) throw new RiotGamesAPIException("Summoner with such name wasn't found.", errorCode: (int)response.StatusCode);
            string responceString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(responceString)) throw new RiotGamesAPIException("Responce string from API 'GetSummonerIdByName' was empty. Maybe this summonerName doesn't exists?");
            Summoner summoner = JsonConvert.DeserializeObject<Summoner>(responceString);
            summoner.region = region == Region.RU ? "RU" : region == Region.EUW ? "EUW" : region == Region.EUNE ? "EUNE" : "NA";
            summoners.Add(summoner);
            summonerID = summoner.id;

            return summonerID;
        }

        //TODO: exceptions
        public async Task<List<Champion>> GetChampionsMasteryInfo(string encryptedSummonerID, Region region = Region.RU, bool saveFile = false)
        {
            string url = string.Empty;
            switch (region)
            {
                case Region.RU:
                    url = $"https://ru.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.EUW:
                    url = $"https://euw1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.EUNE:
                    url = $"https://eun1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.NA:
                    url = $"https://na1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{encryptedSummonerID}";
                    break;
            }

            List<Champion> champions = new List<Champion>();
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                Summoner currentSummoner = summoners.Find(x => x.id == encryptedSummonerID);
                request.RequestUri = new Uri(url);
                HttpResponseMessage response = await httpClient.SendAsync(request);
                string responceString = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == HttpStatusCode.BadRequest) throw new RiotGamesAPIException(responceString, 400);
                int masteryScore = GetTotalMasteryScore(encryptedSummonerID, region).Result;
                if (summonersMastery.TryGetValue(currentSummoner.name, out champions) && currentSummoner.masteryScore == masteryScore) return champions;
                currentSummoner.masteryScore = masteryScore;
            }

            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Get;
                HttpResponseMessage response = await httpClient.SendAsync(request);
                string responceString = await response.Content.ReadAsStringAsync();
                champions = JsonConvert.DeserializeObject<List<Champion>>(responceString);
            }

            foreach (Champion champion in champions)
            {
                string name = null;
                championsIDs.TryGetValue(champion.championId, out name);
                champion.name = name;
            }

            if (saveFile) { SaveChampionsData(GetChampions7Mastery(champions)); saveSummonersData(); }

            return champions;
        }

        private async Task<int> GetTotalMasteryScore(string encryptedSummonerID, Region region = Region.RU)
        {
            string url = "";
            switch (region)
            {
                case Region.RU:
                    url = $"https://ru.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.EUW:
                    url = $"https://euw1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.EUNE:
                    url = $"https://eun1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{encryptedSummonerID}";
                    break;
                case Region.NA:
                    url = $"https://na1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{encryptedSummonerID}";
                    break;
            }
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Get;
                HttpResponseMessage response = await httpClient.SendAsync(request);
                string responceString = await response.Content.ReadAsStringAsync();
                return Convert.ToInt32(responceString);
            }
        }

        internal static Summoner GetSummonerInfo(string summonerName)
        {
            FillSummonersData();
            return summoners.Find(x => x.name == summonerName);
        }

        internal static void SetSummonerInfo(string summonerName, ulong discordID)
        {
            FillSummonersData();
            summoners.Find(x => x.name == summonerName).discordID = discordID;
            saveSummonersData();
        }

        public void SaveChampionsData(List<Champion> champions)
        {
            string summonerName = summoners.Find(x => x.id == champions[0].summonerId).name;
            if (summonersMastery.ContainsKey(summonerName)) summonersMastery.Remove(summonerName);         
            summonersMastery.Add(summonerName, GetChampions7Mastery(champions));
            File.WriteAllText("./summonersMasteryData.json" ,JsonConvert.SerializeObject(summonersMastery, Formatting.Indented));
        }

        private static void saveSummonersData()
        {
            File.WriteAllText("./summonersData.json", JsonConvert.SerializeObject(summoners, Formatting.Indented));
        }

        public async Task<List<Champion>> GetChampions7Mastery(string enryptedSummonerID, Region region = Region.RU)
        {
            List<Champion> allChampions = await GetChampionsMasteryInfo(enryptedSummonerID, region , true);
            List<Champion> allSummonersChampions = null;
            string summonersName = summoners.Find(x => x.id == enryptedSummonerID).name;
            if (summonersMastery.TryGetValue(summonersName, out allSummonersChampions))
            {
                allChampions = allChampions.FindAll(x => x.championLevel == 7);
                if (allChampions.Count == allSummonersChampions.Count) return allChampions;
                foreach(Champion champion in allSummonersChampions)
                {
                    allChampions.Find(x => x.championId == champion.championId).wasAddedToGoogleDocs = champion.wasAddedToGoogleDocs;
                }
                return allChampions;
            }
            return allChampions.FindAll(x => x.championLevel == 7);
        }

        public List<Champion> GetChampions7Mastery(List<Champion> champions)
        {
            List<Champion> allSummonersChampions = null;
            string summonersName = summoners.Find(x => x.id == champions[0].summonerId).name;
            if (summonersMastery.TryGetValue(summonersName, out allSummonersChampions))
            {
                champions = champions.FindAll(x => x.championLevel == 7);
                if (champions.Count == allSummonersChampions.Count) return allSummonersChampions;
                foreach (Champion champion in allSummonersChampions)
                {
                    champions.Find(x => x.championId == champion.championId).wasAddedToGoogleDocs = champion.wasAddedToGoogleDocs;
                }
                return champions;
            }
            return champions.FindAll(x => x.championLevel == 7);
        }
    }
}
