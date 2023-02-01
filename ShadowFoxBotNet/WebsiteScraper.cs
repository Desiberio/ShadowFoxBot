using HtmlAgilityPack;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ShadowFoxBotNet
{
    class WebsiteScraper
    {
        private List<HtmlNode> nodes = null;
        private List<string> postsUrls = null;
        private List<ExternalSiteArticle> previousArticles = null;
        public WebsiteScraper(string url)
        {
            nodes = Parse(url, "h1", "class", "news-title");
            postsUrls = FindURLs();
        }
        public WebsiteScraper(string url, List<ExternalSiteArticle> previousArticles)
        {
            this.previousArticles = previousArticles;
            nodes = Parse(url, "h1", "class", "news-title");
            postsUrls = FindURLs();
        }

        public List<string> GetAllTweetsURLs()
        {
            if (postsUrls == null) throw new System.Exception("Posts URLs list is empty.");

            List<string> tweetsURLs = new List<string>();

            foreach(string postUrl in postsUrls)
            {
                List<HtmlNode> htmlNodes = Parse(postUrl, "blockquote", "class", "twitter-tweet");
                foreach(HtmlNode node in htmlNodes)
                {
                    string url = node.ChildNodes["a"]?.Attributes["href"]?.Value;
                    if(url == null) continue;
                    int questionMarkIndex = url.IndexOf('?');
                    if(questionMarkIndex != -1) tweetsURLs.Add(url.Remove(questionMarkIndex));
                    else tweetsURLs.Add(url);
                }
            }

            if(previousArticles != null)
            {

            }

            return tweetsURLs;
        }

        private List<string> FindURLs()
        {
            if (nodes == null) throw new System.Exception("Nodes list is empty. Maybe URL isn't valid?");

            List<string> urls = new List<string>();             
            foreach(HtmlNode node in nodes) urls.Add(node.ChildNodes.FindFirst("a").Attributes["href"].Value);

            return urls;
        }

        private static List<HtmlNode> Parse(string url, string nodeName, string atributeName, string atributeValue)
        {
            var web = new HtmlWeb();
            HtmlDocument document = web.Load(url);
            
            List<HtmlNode> results = document.DocumentNode.Descendants(nodeName)
                .Where(node => {

                    /*var nodeAtributeName = node.Attributes.FirstOrDefault(x => x.Name == atributeName);
                    if (nodeAtributeName == default(HtmlAttribute)) return false;
                    var nodeAtributeValue = nodeAtributeName.Value;*/
                    return node.Attributes[atributeName]?.Value == atributeValue;
                    /*if (nodeAtributeValue == null || nodeAtributeValue == "") return false;
                    else return node.Attributes[atributeName].Value == atributeValue;*/
                })
                .ToList();

            return results;
        }


    }
}
