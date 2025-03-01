using Noetix.Agents.Tools;
using Newtonsoft.Json.Linq;

namespace Noetix.Knowledge.Tools;

public class SearchKnowledgebaseInput(string query, int? limit = null)
{
    public string Query { get; set; } = query;
    public int? Limit { get; set; } = limit;
}

public class SearchKnowledgebaseOutput(List<Document> documents)
{
    public List<Document> Documents { get; set; } = documents;
}

public class SearchKnowledgebaseTool(Knowledgebase knowledgeBase) : AssistantTool
{
    public override string Id => "SearchKnowledgebase";
    public override string Purpose => "Search Knowledgebase";

    public override ToolResultsSchema Results => new(typeof(SearchKnowledgebaseOutput));
    public override ToolParamsSchema Parameters => new(typeof(SearchKnowledgebaseInput));

    public override string Description => @"
            Searches the Knowledgebase for documents matching the specified query, returning up to the specified limit.
            If you need to find information on a specific topic, you can use this tool to search the Knowledgebase.
            You can invoke this tool a number of times to search for different topics or to refine your search.
        ";

    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.None;

    public override async Task<JToken> Exec(JToken inobj)
    {
        return await WrapExec<SearchKnowledgebaseInput, SearchKnowledgebaseOutput>(inobj, InnerExec);
    }

    private async Task<ToolResult<SearchKnowledgebaseOutput>> InnerExec(SearchKnowledgebaseInput input)
    {
        string query = input.Query;
        int limit = input.Limit ?? 10;
        string id = Guid.NewGuid().ToString();
        UpdateStatus(
            ToolState.Running, $"Searching knowledgebase for up to {limit} documents about '{query}'",
            new Dictionary<string, object>()
        );

        var docs = (await knowledgeBase.Search(query, limit)).ToList();
        UpdateStatus(
            ToolState.Completed, $"Found {docs.Count} documents for the query '{query}'",
            new Dictionary<string, object>()
        );
        var searchResult = new SearchKnowledgebaseOutput(docs);


        return new()
        {
            Message =
                $"Knowledgebase search for '{query}' successful - yielded {searchResult.Documents.Count} results.",
            Result = searchResult
        };
    }
}