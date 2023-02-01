namespace ShadowFoxBotNet
{
    class Champion
    {
        public Champion() { }
        public string name { get; set; }
        public int championLevel { get; set; }
        public int championPoints { get; set; }
        public long lastPlayTime { get; set; }
        public long championId { get; set; }
        public string summonerId { get; set; }
        public bool wasAddedToGoogleDocs { get; set; } = false;
    }
}
