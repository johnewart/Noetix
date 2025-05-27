using NJsonSchema;
using Noetix.Agents.Context;
using Noetix.Agents.Tools;
using Noetix.LLM.Common;

namespace Noetix.LLM.Requests;

public record CompletionRequest
{
    public required string Model { get; set; }
    public required string SystemPrompt { get; set; }
    public required List<Message> Messages { get; set; }

    public GenerationOptions? Options { get; set; }
    public List<ToolDefinition>? ToolDefinitions { get; set; }
    public JsonSchema? ResponseSchema { get; set; }
    public List<ContextData>? ContextProviders { get; set; }

    public string GenericResponseSchemaPreamble
    {
        get
        {
            if (ResponseSchema != null)
            {
                List<string> lines =
                [
                    "There is a *very* strict format for your response - you MUST respond using JSON.",
                    "Your response should start with <response> and end with </response> and include ONLY the results of the task you were asked to do.",
                    "The schema for the response is included in the <response_schema></response_schema> tags below.",
                    "<response_schema>",
                    ResponseSchema.ToJson(),
                    "</response_schema>",
                    "Make absolutely sure that the response is in the correct format.",
                    "You are given the schema, return a valid JSON response that matches the schema - do NOT return a schema.",
                    "Remember: JSON is a strict format, and the response must be valid JSON to be accepted.",
                    "Do not add or remove any content from the response, and make sure that the response is in the correct format before submitting.",
                ];

                return string.Join("\n", lines);
            }
            else
            {
                return "";
            }
        }
    }
}