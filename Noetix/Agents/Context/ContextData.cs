using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace Noetix.Agents.Context;

public abstract class ContextData
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JToken Context { get; }
    
    public JsonSchema Schema => JsonSchema.FromSampleJson(JsonConvert.SerializeObject(Context));

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
    
    public string ToXmlLikeBlock()
    {
        return $@"
<contextProvider>
  <name>{Name}</name>
  <description>{Description}</description>
  <context>
    {JsonConvert.SerializeObject(this.Context, Formatting.Indented)}
  </context>
</contextProvider>";
    }
}