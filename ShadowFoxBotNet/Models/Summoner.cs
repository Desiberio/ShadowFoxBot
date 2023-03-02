namespace ShadowFoxBotNet
{
    class Summoner
    {
        public string Name { get; set; }
        public ulong DiscordID { get; set; } = 0;
        public string SummonerLevel { get; set; }
        public string AccountId { get; set; }
        public int ProfileIconId { get; set; }
        public string ID { get; set; }
        public string PUUID { get; set; }
        public int MasteryScore { get; set; }
        public Region ServerRegion { get; set; }
    }
}
