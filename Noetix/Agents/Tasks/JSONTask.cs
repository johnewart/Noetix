using NJsonSchema;
using NLog;
using Noetix.Common;

namespace Noetix.Agents.Tasks;

public abstract class JSONTask<I, O> : AssistantTask<I, O> where I : TaskParams
{
    public override string ContentType { get; } = "json";
    public abstract JsonSchema ResponseSchema { get; }
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    public override List<TaskExample<I, O>> Examples => [];

    public override List<string> AdditionalInstructions
    {
        get
        {
            if (ResponseSchema != null)
            {
                return new List<string>
                {
                    "<response_schema>",
                    ResponseSchema.ToJson(),
                    "</response_schema>",
                    "Make absolutely sure that the response is in the correct format.",
                    "You are given the schema, return a valid JSON response that matches the schema - do NOT return a schema.",
                    "The schema for the response is included in the <response_format></response_format> tags.",
                    "Put your response in <response></response> tags so it can be extracted easily.",
                    "Remember: JSON is a strict format, and the response must be valid JSON to be accepted.",
                    "Do not add or remove any content from the response, and make sure that the response is in the correct format before submitting."
                };
            }
            return new List<string>();
        }
    }

    public override O ParseResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            throw new Exception("Empty response");
        }

        string jsonText = response.Contains("<response>") && response.Contains("</response>")
            ? response.Substring(response.IndexOf("<response>") + "<response>".Length, response.IndexOf("</response>") - response.IndexOf("<response>") - "<response>".Length)
            : response;

        if (string.IsNullOrEmpty(jsonText))
        {
            throw new Exception("Empty JSON response between <response> tags");
        }

        var extractedJSON = JsonHandling.ExtractJSON(jsonText);
        if (!extractedJSON.Valid)
        {
            throw new Exception($"Invalid JSON response: {extractedJSON.Error}");
        }

        var result = JsonHandling.DeserializeJson<O>(extractedJSON.Json);
        if (result == null)
        {
            throw new Exception("Invalid JSON response");
        }

        if (ResponseSchema != null)
        {
            // Validate the JSON response against the ObjectSchema
            var errors = ResponseSchema.Validate(extractedJSON.Json).ToList();
            if (errors.Any())
            {
                throw new Exception($"Invalid JSON response: {string.Join(", ", errors)}");
            }
        }

        logger.Info($"Result: {result}");
        return result;
    }
}