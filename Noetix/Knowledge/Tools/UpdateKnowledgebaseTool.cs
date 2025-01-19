using Noetix.Agents.Tools;
using Newtonsoft.Json.Linq;

namespace Noetix.Knowledge.Tools;

public record KnowledgeDocument
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
}
    
public record UpdateKnowledgebaseInput
{
    public KnowledgeDocument[] Documents { get; set; }
}

public record UpdateknowledgebaseOutput()
{
    public int DocumentCount { get; set; }
}

public class UpdateKnowledgebaseTool(Knowledgebase knowledgeBase) : AssistantTool
{
    public override string Id => "UpdateKnowledgebase";
    public override string Purpose => "Store new documents in the Knowledgebase.";

    public override ToolResultsSchema Results => new ToolResultsSchema(typeof(UpdateknowledgebaseOutput));
    public override ToolParamsSchema Parameters => new ToolParamsSchema(typeof(UpdateKnowledgebaseInput));

    public override string Description => @"
           Stores new documents in the Knowledgebase - these documents will be indexed and searchable and can be used to answer questions. 
           Use this to permanently store new information in the Knowledgebase for future reference.
        ";

    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.None;

    public override async Task<JToken> Exec(JToken inobj)
    {
        return await WrapExec<UpdateKnowledgebaseInput, UpdateknowledgebaseOutput>(inobj, InnerExec);
    }

    private async Task<ToolResult<UpdateknowledgebaseOutput>> InnerExec(UpdateKnowledgebaseInput input)
    {
        var documents = input.Documents;
        var count = documents.Length;
        var kbDocs = documents.Select(d => new Document(d.Content, d.Name, d.Id));
        UpdateStatus(ToolState.Running, "Adding " + count + " new documents to the Knowledgebase.", new Dictionary<string, object>());
        knowledgeBase.Store(kbDocs);
        UpdateStatus(ToolState.Completed, "Knowledgebase updated with " + count + " new documents.", new Dictionary<string, object>());
            
        return new()
        {
            Message = "Knowledgebase updated with " + count + " new documents.",
            Success = true,
            Result = new UpdateknowledgebaseOutput { DocumentCount = count }
        };
    }
}