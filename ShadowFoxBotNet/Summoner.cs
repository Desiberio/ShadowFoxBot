namespace ShadowFoxBotNet
{
    class Summoner
    {
        Summoner() { }
        public string name { get; set; }
        public ulong discordID { get; set; } = 0;
        public string summonerLevel { get; set; }
        public string accountId { get; set; }
        public int profileIconId { get; set; }
        public string id { get; set; }
        public string puuid { get; set; }
        public int masteryScore { get; set; }
        public string region { get; set; }
    }
}
