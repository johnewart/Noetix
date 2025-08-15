using System.ComponentModel;
using Noetix.Agents.Tools;
using Newtonsoft.Json.Linq;

namespace Noetix.Knowledge.Tools;

public record SearchResult(string DocumentId, string Title, string Content, double Score);

public class SearchKnowledgebaseInput(string query, int? limit = null)
{
    [Description("The search query to find relevant documents.")]
    public string Query { get; set; } = query;
    
    [Description("Maximum number of results to return. The default is 5, maximum is 20.")]
    public int? Limit { get; set; } = limit;
    
    [Description("Minimum score threshold for results. The default is 0.1 (maximum is 1.0).")]
    public double? MinimumScore { get; set; } = null;
}

public class SearchKnowledgebaseOutput(List<SearchResult> results)
{
    public List<SearchResult> SearchResults { get; set; } = results;
}

public class SearchKnowledgebaseTool(Knowledgebase knowledgeBase) : AssistantTool
{
    public override string Id => "SearchKnowledgebase";
    public override string Purpose => "Search the knowledge base for documents";

    public override ToolResultsSchema Results => new(typeof(SearchKnowledgebaseOutput));
    public override ToolParamsSchema Parameters => new(typeof(SearchKnowledgebaseInput));

    public override string Description => @"
            Searches the knowledge base for documents matching the specified query, returning up to the specified limit.
            If you need to find information on a specific topic, you can use this tool to search the knowledge base.
            You can invoke this tool a number of times to search for different topics or to refine your search. If you do not 
            get any results you can try changing the query, or lowering the minimum score threshold required for a match.
            The range for search score is from 0.0 to 1.0, with 1.0 being the most relevant match and 0.0 being a non-match.
        ";

    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.None;

    public override async Task<JToken> Exec(JToken inobj)
    {
        return await WrapExec<SearchKnowledgebaseInput, SearchKnowledgebaseOutput>(inobj, InnerExec);
    }

    private async Task<ToolResult<SearchKnowledgebaseOutput>> InnerExec(SearchKnowledgebaseInput input)
    {
        var minScore = input.MinimumScore ?? 0.1;
        if (minScore < 0.0 || minScore > 1.0)
        {
            return new ToolResult<SearchKnowledgebaseOutput>
            {
                Message = $"Minimum score threshold must be between 0.0 and 1.0, but was {minScore}.",
                Result = null,
                Success = false
            };
        }
        
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Length < 3)
        {
            return new ToolResult<SearchKnowledgebaseOutput>
            {
                Message = "Query must be at least 3 characters long.",
                Result = null,
                Success = false
            };
        }
        
        if (input.Limit.HasValue && (input.Limit < 1 || input.Limit > 20))
        {
            return new ToolResult<SearchKnowledgebaseOutput>
            {
                Message = $"Limit must be between 1 and 20, but was {input.Limit}.",
                Result = null,
                Success = false
            };
        }
        
        string query = input.Query;
        int limit = input.Limit ?? 5;
        string id = Guid.NewGuid().ToString();
        UpdateStatus(
            ToolState.Running, $"Searching knowledgebase for up to {limit} documents about '{query}'",
            new Dictionary<string, object>()
        );

        var rankedResults = (await knowledgeBase.RankedSearch(query, limit)).ToList();
        UpdateStatus(
            ToolState.Completed, $"Found {rankedResults.Count} documents for the query '{query}'",
            new Dictionary<string, object>()
        );
        
        var omittedCount = rankedResults.Count(r => r.Score < (minScore));
        
        var searchResults = rankedResults.Where(r => r.Score >= minScore).Select(r => new SearchResult(
            DocumentId: r.Document.Id, Title: r.Document.Name, Content: r.Document.CleanedContent, Score: r.Score
        )).ToList();
        
        var response = new SearchKnowledgebaseOutput(searchResults);


        return new()
        {
            Message =
                $"Knowledgebase search for '{query}' successful - yielded {response.SearchResults.Count} results. {omittedCount} results omitted that were below the minimum score threshold of {minScore}.",
            Result = response
        };
    }
}