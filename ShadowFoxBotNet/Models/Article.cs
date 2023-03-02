using System;

namespace ShadowFoxBotNet
{
    class Article
    {
        public Article() { }
        public string title { get; set; }
        public string description { get; set; }
        public string articleType { get; set; }
        public string uid { get; set; }
        public DateTime dateTime { get; set; }
        public string url { get; set; }
        public bool isChecked { get; set; } 
        public string external_link { get; set; }
        public string category_title { get; set; }
        public string youtube_link { get; set; }
        public string banner { get; set; }
        public string addition { get; set; } = string.Empty;

    }
}
