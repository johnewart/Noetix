using Noetix.Agents.Tools;
using Newtonsoft.Json.Linq;

namespace ResearchAssistant;

public class WikipediaArticle
{
    public int PageId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
}

public class WikipediaSearchInput
{
    public string? SearchTerm { get; init; }
    public int MaxResults { get; init; }
}

public class WikipediaSearchOutput
{
    public List<WikipediaArticle>? Articles { get; set; }
}

public class WikipediaSearchTool() : AssistantTool
{

    public override string Id => "WikipediaSearchTool";
    public override string Purpose => "Search wikipedia for articles.";

    public override string Description =>
        "This tool allows you to query Wikipedia and will give back a number of articles in the results.";

    public override ToolParamsSchema Parameters => new(typeof(WikipediaSearchInput));
    public override ToolResultsSchema Results => new(typeof(WikipediaSearchOutput));
    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.None;

    public override async Task<JToken> Exec(JToken input)
    {
        return await WrapExec<WikipediaSearchInput, WikipediaSearchOutput>(input, ExecuteAction);
    }

    private async Task<ToolResult<WikipediaSearchOutput>> ExecuteAction(WikipediaSearchInput input)
    {
        var searcher = new WikipediaSearcher();
        var articles = await searcher.search(input.SearchTerm, input.MaxResults, (message) =>
        {
            UpdateStatus(
                ToolState.Running, message, new Dictionary<string, object>()
            );
        });

        return new ToolResult<WikipediaSearchOutput>
        {
            Success = true,
            Error = null,
            Message = $"Succesfully found {articles.Count} articles when searching for '{input.SearchTerm}'.",
            Result = new WikipediaSearchOutput { Articles = articles }
        };
    }

}