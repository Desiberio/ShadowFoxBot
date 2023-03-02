using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShadowFoxBotNet
{
    class RiotGamesArticleWorker
    {
        private static List<Article> _articles;

        public static List<string> CheckNews()
        {
            List<string> news = new List<string>();
            _articles = GetArticles("./data.json");
            if (_articles == null) _articles = new List<Article>();

            List<Article> unpublishedArticles = GetUnpublishedArticles(_articles);
            news = FormatNews(unpublishedArticles);

            SaveData(unpublishedArticles);

            return news;
        }

        private static List<Article> GetArticles(string path)
        {
            return JsonConvert.DeserializeObject<List<Article>>(File.ReadAllText(path));
        }

        private  static List<Article> GetUnpublishedArticles(List<Article> articles)
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

        private static async void SaveData(List<Article> articles)
        {
            foreach (Article article in articles) article.isChecked = true;
            _articles.AddRange(articles);

            string data = JsonConvert.SerializeObject(_articles, Formatting.Indented);
            await File.WriteAllTextAsync("./data.json", data);
        }

        private  static List<string> FormatNews(List<Article> unpublishedArticles)
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
    }
}
