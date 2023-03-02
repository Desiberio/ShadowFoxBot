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
        private readonly string apiKey = File.ReadAllText("./apiKeys/riotGamesAPIKey.txt");
        private Dictionary<long, string> championsIDs = new Dictionary<long, string>();
        private static Dictionary<string, List<Champion>> summonersMastery = new Dictionary<string, List<Champion>>();
        private static List<Summoner> summoners = new List<Summoner>();
        private HttpClient httpClient = null;

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
            string championsJsonUrl = $"http://ddragon.leagueoflegends.com/cdn/{newestVersion}/data/en_US/champion.json";
            return webClient.DownloadString(championsJsonUrl);
        }

        public string GetSummonerIdByName(string summonerName, Region region = Region.RU)
        {
            Summoner currentSummoner = summoners.Find(x => x.Name == summonerName.ToLower());
            if (currentSummoner != null) return currentSummoner.ID;

            string url = GetRoutingURL(summonerName, region, RoutingType.Summoner);

            string json = MakeAPICall(url).Result;

            if (string.IsNullOrEmpty(json)) throw new RiotGamesAPIException("Responce string from API was empty. Maybe this summonerName doesn't exists?", ErrorCode.NotFound);
            Summoner summoner = JsonConvert.DeserializeObject<Summoner>(json);
            summoner.ServerRegion = region;
            summoner.Name = summoner.Name.ToLower();
            summoners.Add(summoner);

            return summoner.ID;
        }

        private async Task<string> MakeAPICall(string url)
        {
            HttpResponseMessage response = null;
            using (HttpRequestMessage request = new HttpRequestMessage())
            {
                request.RequestUri = new Uri(url);
                request.Method = HttpMethod.Get;
                response = await httpClient.SendAsync(request);
            }

            if (response.StatusCode == HttpStatusCode.NotFound) throw new RiotGamesAPIException("Summoner with such name wasn't found.", ErrorCode.NotFound);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private string GetRoutingURL(string value, Region region, RoutingType routingType)
        {
            return routingType switch
            {
                RoutingType.Summoner => region switch
                {
                    Region.RU => $"https://ru.api.riotgames.com/lol/summoner/v4/summoners/by-name/{value}",
                    Region.EUW => $"https://euw1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{value}",
                    Region.EUNE => $"https://eun1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{value}",
                    Region.NA => $"https://na1.api.riotgames.com/lol/summoner/v4/summoners/by-name/{value}",
                    _ => throw new RiotGamesAPIException("There is no routing value for this region.", ErrorCode.RoutingValueNotFound),
                },
                RoutingType.ChampionMastery => region switch
                {
                    Region.RU => $"https://ru.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{value}",
                    Region.EUW => $"https://euw1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{value}",
                    Region.EUNE => $"https://eun1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{value}",
                    Region.NA => $"https://na1.api.riotgames.com/lol/champion-mastery/v4/champion-masteries/by-summoner/{value}",
                    _ => throw new RiotGamesAPIException("There is no routing value for this region.", ErrorCode.RoutingValueNotFound),
                },
                RoutingType.ChampionMasteryScores => region switch
                {
                    Region.RU => $"https://ru.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{value}",
                    Region.EUW => $"https://euw1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{value}",
                    Region.EUNE => $"https://eun1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{value}",
                    Region.NA => $"https://na1.api.riotgames.com/lol/champion-mastery/v4/scores/by-summoner/{value}",
                    _ => throw new RiotGamesAPIException("There is no routing value for this region.", ErrorCode.RoutingValueNotFound),
                },
                _ => throw new RiotGamesAPIException("There is no routing value for this type.", ErrorCode.RoutingValueNotFound),
            };
        }

        public List<Champion> GetChampionsMasteryInfo(string encryptedSummonerID, Region region = Region.RU, bool saveFile = false)
        {
            List<Champion> champions = new List<Champion>();

            Summoner currentSummoner = summoners.Find(x => x.ID == encryptedSummonerID);

            int masteryScore = GetTotalMasteryScore(encryptedSummonerID, region);
            if (summonersMastery.TryGetValue(currentSummoner.Name, out champions) && currentSummoner.MasteryScore == masteryScore) return champions;
            currentSummoner.MasteryScore = masteryScore;

            string url = GetRoutingURL(encryptedSummonerID, region, RoutingType.ChampionMastery);
            string json = MakeAPICall(url).Result;
            champions = JsonConvert.DeserializeObject<List<Champion>>(json);


            foreach (Champion champion in champions)
            {
                championsIDs.TryGetValue(champion.ChampionId, out string name);
                champion.Name = name;
            }

            if (saveFile) { SaveChampionsData(GetChampions7Mastery(champions)); SaveSummonersData(); }

            return champions;
        }

        private int GetTotalMasteryScore(string encryptedSummonerID, Region region = Region.RU)
        {
            string url = GetRoutingURL(encryptedSummonerID, region, RoutingType.ChampionMasteryScores);
            return Convert.ToInt32(MakeAPICall(url));
        }

        public static Summoner GetSummonerInfo(string summonerName)
        {
            FillSummonersData();
            return summoners.Find(x => x.Name == summonerName.ToLower());
        }

        public static void SetSummonerInfo(string summonerName, ulong discordID)
        {
            FillSummonersData();
            summoners.Find(x => x.Name == summonerName.ToLower()).DiscordID = discordID;
            SaveSummonersData();
        }

        public void SaveChampionsData(List<Champion> champions)
        {
            string summonerName = summoners.Find(x => x.ID == champions[0].SummonerId).Name;
            if (summonersMastery.ContainsKey(summonerName)) summonersMastery.Remove(summonerName);         
            summonersMastery.Add(summonerName, GetChampions7Mastery(champions));
            File.WriteAllText("./summonersMasteryData.json" ,JsonConvert.SerializeObject(summonersMastery, Formatting.Indented));
        }

        private static void SaveSummonersData()
        {
            File.WriteAllText("./summonersData.json", JsonConvert.SerializeObject(summoners, Formatting.Indented));
        }

        public List<Champion>GetChampions7Mastery(string enryptedSummonerID, Region region = Region.RU)
        {
            List<Champion> allChampions = GetChampionsMasteryInfo(enryptedSummonerID, region , true);
            string summonersName = summoners.Find(x => x.ID == enryptedSummonerID).Name;

            if (summonersMastery.TryGetValue(summonersName, out List<Champion> allSummonersChampions))
            {
                allChampions = allChampions.FindAll(x => x.ChampionLevel == 7);
                if (allChampions.Count == allSummonersChampions.Count) return allChampions;
                foreach(Champion champion in allSummonersChampions)
                {
                    allChampions.Find(x => x.ChampionId == champion.ChampionId).WasAddedToGoogleDocs = champion.WasAddedToGoogleDocs;
                }
                return allChampions;
            }
            return allChampions.FindAll(x => x.ChampionLevel == 7);
        }

        public List<Champion> GetChampions7Mastery(List<Champion> champions)
        {
            string summonersName = summoners.Find(x => x.ID == champions[0].SummonerId).Name;

            if (!summonersMastery.TryGetValue(summonersName, out List<Champion> allSummonersChampions)) return champions.FindAll(x => x.ChampionLevel == 7);

            champions = champions.FindAll(x => x.ChampionLevel == 7);
            if (champions.Count == allSummonersChampions.Count) return allSummonersChampions;
            
            foreach (Champion champion in allSummonersChampions)
            {
                champions.Find(x => x.ChampionId == champion.ChampionId).WasAddedToGoogleDocs = champion.WasAddedToGoogleDocs;
            }

            return champions;
        }
    }
}
