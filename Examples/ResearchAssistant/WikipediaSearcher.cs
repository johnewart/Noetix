using System.Text.Json;
using System.Web;
using HtmlAgilityPack;

namespace ResearchAssistant;

public class WikipediaSearcher
{  
    const string API_ENDPOINT = "https://en.wikipedia.org/w/api.php";

    public async Task<List<WikipediaArticle>> search(string SearchTerm, int MaxResults, Action<string>?  statusCallback = null)
    {
        HttpClient _client = new HttpClient();
        var articles = new List<WikipediaArticle>();
        
        // Build the search query
        var searchQuery = $"{API_ENDPOINT}?action=query&format=json&list=search" +
                          $"&srsearch={HttpUtility.UrlEncode(SearchTerm)}" +
                          $"&srlimit={MaxResults}";

        statusCallback?.Invoke($"Searching Wikipedia for '{SearchTerm}' with a limit of {MaxResults} results...");
        // Get search results
        var searchResponse = await _client.GetStringAsync(searchQuery);
        var searchJson = JsonSerializer.Deserialize<JsonElement>(searchResponse);
        var searchResults = searchJson.GetProperty("query").GetProperty("search");
        statusCallback?.Invoke($"Found {searchResults.GetArrayLength()} results for '{SearchTerm}'.");
        
        foreach (var result in searchResults.EnumerateArray())
        {
            statusCallback?.Invoke($"Getting content for '{result.GetProperty("title").GetString()}'...");
            
            var pageId = result.GetProperty("pageid").GetInt32();
            var title = result.GetProperty("title").GetString();

            // Get full content for each article
            var contentQuery = $"{API_ENDPOINT}?action=parse&format=json&pageid={pageId}&prop=text";
            var contentResponse = await _client.GetStringAsync(contentQuery);
            var contentJson = JsonSerializer.Deserialize<JsonElement>(contentResponse);
            var content = contentJson.GetProperty("parse")
                .GetProperty("text")
                .GetProperty("*")
                .GetString();
            
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var chunks = new List<string>();
            doc.DocumentNode.DescendantsAndSelf()
                .Where(n => n.Name == "script" || n.Name == "style")
                .ToList()
                .ForEach(n => n.Remove());
            
            foreach (var item in doc.DocumentNode.DescendantsAndSelf())
            {
                if (item.NodeType == HtmlNodeType.Text)
                {
                    if (item.InnerText.Trim() != "")
                    {
                        chunks.Add(item.InnerText.Trim());
                    }
                }
            }
            var parsedText = string.Join(" ", chunks);
            
            articles.Add(new WikipediaArticle
            {
                PageId = pageId,
                Title = title,
                Content = parsedText 
            });
        }

        statusCallback?.Invoke($"Finished searching Wikipedia for '{SearchTerm}' - found {articles.Count} articles.");
        return articles;
    }
}