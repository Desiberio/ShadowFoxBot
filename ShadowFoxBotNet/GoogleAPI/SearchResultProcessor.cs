using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShadowFoxBotNet
{
    class SearchResultProcessor
    {
        //these all props maybe should be in another class
        public List<string> SkinTitles { get; set; } = new List<string>();
        public List<string> VoiceTitles { get; set; } = new List<string>();
        public List<string> AbilityTitles { get; set; } = new List<string>();
        public Dictionary<string, List<string>> ThemesAndTitles { get; private set; } = new Dictionary<string, List<string>>();
        public int AllTitlesCount {
            get
            {
                return SkinTitles.Count + VoiceTitles.Count + AbilityTitles.Count;
            }
        }
        public SearchResultProcessor(List<SearchResult> searchResults)
        {
            if (searchResults.Count == 0) return;
            ProcessResults(searchResults);
            GroupSkinsByThemes();
        }

        private void ProcessResults(List<SearchResult> searchResults)
        {
            string root = "https://youtu.be/";
            foreach (SearchResult video in searchResults)
            {
                string title = WebUtility.HtmlDecode(video.Snippet.Title);
                if (!title.Contains("Preview") && !title.Contains("Voice")) continue;
                if (!title.Contains("Voice"))
                {
                    int index = Compare(title.IndexOf('-') - 1, title.IndexOf('|') - 1);
                    if (index > 0) title = title.Remove(index);
                }

                title += " — " + root + video.Id.VideoId;

                if (title.Contains("Skin") || title.Contains("Chroma")) SkinTitles.Add(title);
                else if (title.Contains("Voice")) VoiceTitles.Add(title); //if-else structure looks weird. Maybe it can be improved via switch-case statement?
                else if (title.Contains("Ability") || title.Contains("Gameplay")) AbilityTitles.Add(title);
            }
        }

        private void GroupSkinsByThemes()
        {
            List<string> championsName = RiotGamesAPI.GetChampionsName();
            foreach (string title in SkinTitles)
            {
                string themeName = GetThemeName(championsName, title);

                if (ThemesAndTitles.ContainsKey(themeName))
                {
                    ThemesAndTitles.First(x => x.Key == themeName).Value.Add(title);
                    continue;
                }
                ThemesAndTitles.TryAdd(themeName, new List<string>() { title });
            }
        }

        private static string GetThemeName(List<string> championsName, string title)
        {
            //find champion name
            int indexOfChampion = championsName.FindLastIndex(x => title.Contains(x));
            string championName = indexOfChampion > 0 ? championsName[indexOfChampion] : null;

            //remove everything after theme name (i.e. Mythmaker Irelia will return theme "Mythmaker") or keep default theme "Other Skins"
            string themeName = "Other Skins";
            if (championName != null)
            {
                int index = title.IndexOf(championName) - 1;
                if (index > 0) themeName = title.Remove(index);
                else themeName = title.Split(' ')[0];
                if (themeName.Contains("Prestige")) themeName = themeName.Remove(themeName.IndexOf("Prestige"), 8).Trim();
            }

            return themeName;
        }

        private int Compare(int i1, int i2)
        {
            if (i1 > 0 && i2 > 0) return Math.Min(i1, i2);
            else if (i1 > 0) return i1;
            else return i2;
        }
    }
}
