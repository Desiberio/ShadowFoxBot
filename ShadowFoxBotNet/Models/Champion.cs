namespace ShadowFoxBotNet
{
    class Champion
    {
        public string Name { get; set; }
        public int ChampionLevel { get; set; }
        public int ChampionPoints { get; set; }
        public long LastPlayTime { get; set; }
        public long ChampionId { get; set; }
        public string SummonerId { get; set; }
        public bool WasAddedToGoogleDocs { get; set; } = false;
    }
}
