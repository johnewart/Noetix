using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Noetix.Agents.Context;

namespace Noetix.Knowledge;

public record KnowledgeRecord
{
    public string Title { get; init; }
    public string Description { get; init; }
    public string Content { get; init; }
}

public class KnowledgeContextData(List<KnowledgeRecord> documents) : ContextData
{
    public override string Name => "Knowledgebase Documents";
    public override string Description => "A list of documents from the knowledgebase";

    public override JObject Context
    {
        get
        {
            var json = JsonConvert.SerializeObject(documents);
            return JObject.Parse(json);
        }
    }
}