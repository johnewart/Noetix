using System.Text.RegularExpressions;
using Noetix.Common;
using Noetix.LLM.Tools;
using Newtonsoft.Json;

namespace Noetix.Agents.Tools;

public class ToolProcessor(IEnumerable<AssistantTool> tools)
{
    private readonly List<AssistantTool> tools = tools.ToList();
    private readonly List<ToolStatusCallback> statusCallbacks = new List<ToolStatusCallback>();

    public void AddStatusCallback(ToolStatusCallback callback)
    {
        statusCallbacks.Add(callback);
    }

    public record ToolResponse
    {
        public string Tool { get; set; }
        public ToolResults Result { get; set; }
    }
    
    
    public async Task<ToolResponse[]> Process(string content, List<ToolInvocationRequest> toolRequests, Action<ToolStatusUpdate>? onToolStatusUpdate = null)
    {
        if (!toolRequests.Any())
        {
            return
            [
                new ToolResponse{
                    Tool = "no_tools_requested",
                    Result = new ToolResults(
                        id: "no_tools_requested",
                        success: false,
                        error: "No tools were requested"
                    )}
            ];
        }

        var toolResults = toolRequests.Select<ToolInvocationRequest, Task<ToolResponse>>(async (request) =>
            {
                var tool = tools.FirstOrDefault(t => t.Id == request.Tool);
                if (tool != null)
                {
                    onToolStatusUpdate?.Invoke(new ToolStatusUpdate
                    {
                        ToolId = request.Tool,
                        State = ToolState.Running,
                        Message = $"Invoking tool {tool.Id}",
                    });

                    try
                    {
                        var results = await tool.Invoke(request.Id, request.Parameters);

                        if (results.Success)
                        {
                            onToolStatusUpdate?.Invoke(new ToolStatusUpdate
                            {
                                ToolId = request.Tool,
                                State = ToolState.Completed,
                                Message = $"Tool {tool.Id} completed",
                                Data = new Dictionary<string, object> { { "result", results } }
                            });
                            return new ToolResponse { Tool = request.Tool, Result = results };
                        }

                        onToolStatusUpdate?.Invoke(new ToolStatusUpdate
                        {
                            ToolId = request.Tool,
                            State = ToolState.Failed,
                            Message = $"Tool {tool.Id} failed to execute: {results.Error}",
                        });
                        return new ToolResponse
                        {
                            Tool = request.Tool,
                            Result = new ToolResults(id: request.Id, false,
                                $"Tool {request.Tool} failed to execute: {results.Error}")
                        };
                    }
                    catch (Exception e)
                    {
                        onToolStatusUpdate?.Invoke(new ToolStatusUpdate
                        {
                            ToolId = request.Tool,
                            State = ToolState.Failed,
                            Message = $"Tool {tool.Id} failed to execute: {e.Message}"
                        });
                        return new ToolResponse
                        {
                            Tool = request.Tool,
                            Result = new ToolResults(id: request.Id, false,
                                $"Tool {request.Tool} failed to execute: {e.Message}")
                        };
                    }
                }

                onToolStatusUpdate?.Invoke(new ToolStatusUpdate
                {
                    ToolId = request.Tool,
                    State = ToolState.Failed,
                    Message = $"I'm sorry, I couldn't find the tool you requested ({request.Tool})."
                });
                return new ToolResponse
                {
                    Tool = request.Tool,
                    Result = new ToolResults(id: request.Id, false,
                        $"I'm sorry, I couldn't find the tool you requested ({request.Tool}).")
                };
            }
        );
            
        return await Task.WhenAll(toolResults);
    }

    private IEnumerable<string> ExtractToolHelpRequests(string input)
    {
        var helpBlocks = Regex.Matches(input, "<tool_help tool_id=\"([\\s\\S]*?)\"/>");
        var toolIds = new List<string>();

        foreach (Match match in helpBlocks)
        {
            var toolId = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(toolId))
            {
                toolIds.Add(toolId);
            }
        }

        return toolIds;
    }

    protected record ToolHelpResponse {
        public string Tool { get; set; }
        public string Help { get; set; }
    }
    public string ProcessToolHelp(string content)
    {
        var toolIds = ExtractToolHelpRequests(content);
        var helpResults = toolIds.Select(toolId =>
        {
            var tool = tools.FirstOrDefault(t => t.Id == toolId);
            if (tool != null)
            {
                return JsonConvert.SerializeObject( new ToolHelpResponse { Tool = tool.Id, Help = tool.Instructions() });
            }
            else
            {
                return JsonConvert.SerializeObject( new ToolHelpResponse { Tool = toolId, Help = $"I'm sorry, I couldn't find the tool you requested help with ({toolId})."  });
            }
        }).ToList();

        return $@"
            <tool_help_results>
            {string.Join("\n", helpResults)}
            </tool_help_results>
            ";
    }

    private void EmitToolStatus(ToolStatusUpdate update)
    {
        foreach (var callback in statusCallbacks)
        {
            callback(update);
        }
    }

    public IEnumerable<ToolInvocationRequest> ExtractToolsRequests(string input)
    {
        var toolMatches = Regex.Matches(input, "<tools>([\\s\\S]*?)</tools>");
        if (toolMatches.Count == 0)
        {
            throw new Exception("No valid tools block(s) found in input");
        }

        var toolRequests = new List<ToolInvocationRequest>();
        var toolRequestErrors = new List<string>();

        foreach (Match match in toolMatches)
        {
            var block = match.Groups[1].Value;
            var toolsJSON = JsonHandling.ExtractJSON(block);
            if (!toolsJSON.Valid)
            {
                throw new ToolParseError($"Error parsing JSON: {toolsJSON.Error}", block);
            }

            try
            {
                var toolRequestList = JsonConvert.DeserializeObject<List<ToolInvocationRequest>>(toolsJSON.Json);

                if (toolRequestList == null)
                {
                    throw new ToolParseError("Error deserializing tool request", block);
                }

                foreach (var toolRequest in toolRequestList)
                {
                    if (string.IsNullOrEmpty(toolRequest.Tool))
                    {
                        toolRequestErrors.Add($"A tool request is missing a tool ID: {block}");
                    }
                        
                    toolRequests.Add(toolRequest);
                }
            }
            catch (Exception e)
            {
                throw new ToolParseError($"Error extracting tools: {e.Message}", block);
            }
        }

        return toolRequests;
    }
}