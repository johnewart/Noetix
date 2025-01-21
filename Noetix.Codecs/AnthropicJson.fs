namespace Codecs

open Newtonsoft.Json.Linq
open Thoth.Json.Net

type ToolUseBlock =
    {
            Id: string
            Name: string
            Input: JsonValue
    }
    
type ToolResultBlock =
    {
        ToolUseId: string
        Content: string
    }


type UnknownBlock = {
    Type : string
}

type TextContentBlock =
    {
        Text: string
    }
    
type ContentBlock =  TCB of TextContentBlock | TUB of ToolUseBlock | TRB of ToolResultBlock | UnknownBlock
    
type TokenUsage =
    {
        InputTokens: int
        OutputTokens: int
    }
    
type AnthropicMessage =
    {
        Content: ContentBlock list
        Role: string
    }
    
type AnthropicToolDefinition =
    {
        Name: string
        Description: string
        InputSchema: JsonValue
    }
    
type AnthropicRequest =
    {
        Model: string
        Messages: AnthropicMessage list
        MaxTokens: int
        SystemPrompt: string option
        Tools: AnthropicToolDefinition list option
    }
type AnthropicResponse =
    {
        Content: ContentBlock list
        Id: string
        Model: string
        Role: string
        StopReason: string
        StopSequence: string list option 
        Type: string
        Usage: TokenUsage 
    }
    
    member this.ToolBlocks: List<ToolUseBlock> =
       this.Content |> List.filter( function | TUB _ -> true | _ -> false) |> List.map (function | TUB block -> block | _ -> failwith "This should never happen")

    member this.TextBlocks: List<TextContentBlock> =
        this.Content |> List.filter( function | TCB _ -> true | _ -> false) |> List.map (function | TCB block -> block | _ -> failwith "This should never happen")
    
type DecodeBuilder() =
    member _.Bind(decoder, f) : Decoder<_> =
        Decode.andThen f decoder
    member _.Return(value) =
        Decode.succeed value
    member _.ReturnFrom(decoder : Decoder<_>) =
        decoder

module UnknownBlock =
    let decoder: Decoder<UnknownBlock> =
        Decode.object (fun get -> {
            Type = get.Required.Field "type" Decode.string
        })
        
module TextContentBlock =
    let decoder: Decoder<TextContentBlock> =
        Decode.object(fun get -> {
            Text = get.Required.Field "text" Decode.string
        })
    let encoder(block: TextContentBlock) =
        Encode.object [
            "text", Encode.string block.Text
            "type", Encode.string "text"
        ]

module ToolUseBlock =
    let decoder: Decoder<ToolUseBlock> =
        Decode.object(fun get -> {
            Id = get.Required.Field "id" Decode.string
            Name = get.Required.Field "name" Decode.string
            Input = get.Required.Field "input" Decode.value
        })
        
    let inputEncoder (input: JsonValue) =
        let isEmptyObject = input.Type = JTokenType.Object && input.HasValues = false   
        let isEmptyArray = input.Type = JTokenType.Array && input.HasValues = false
      
        match isEmptyObject, isEmptyArray with
        | true, _ -> Encode.nil
        | _, true -> Encode.nil
        | _ -> input 
        
    let encoder(block: ToolUseBlock) =
        Encode.object [
            "id", Encode.string block.Id
            "name", Encode.string block.Name
            "input", inputEncoder block.Input
            "type", Encode.string "tool_use"
        ]
       
module ToolResultBlock =
    let encoder(block: ToolResultBlock) =
        Encode.object [
            "tool_use_id", Encode.string block.ToolUseId
            "content", Encode.string block.Content
            "type", Encode.string "tool_result"
        ]
            
module ContentBlock =
    let decode = DecodeBuilder()
    
    let decoder =
        Decode.field "type" Decode.string
        |> Decode.andThen
            (function            
            | "text" -> Decode.map (fun text -> TCB text) TextContentBlock.decoder
            | "tool_use" -> Decode.map (fun toolUse -> TUB toolUse) ToolUseBlock.decoder
            | invalid -> Decode.fail $"Failed to decode `%s{invalid}` it's an invalid case for `ContentBlock`")

    let encode = function
        | TCB text ->  TextContentBlock.encoder text
        | TUB toolUse -> ToolUseBlock.encoder toolUse
        | TRB toolResult -> ToolResultBlock.encoder toolResult
  
module AnthropicResponse =
    let tokenUsageDecoder =
        Decode.object (fun get -> {
            InputTokens = get.Required.Field "input_tokens" Decode.int
            OutputTokens = get.Required.Field "output_tokens" Decode.int
        })
        
    let decoder : Decoder<AnthropicResponse> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" Decode.string
                Content = get.Required.Field "content" (Decode.list ContentBlock.decoder)
                Model = get.Required.Field "model" Decode.string
                Role =  get.Required.Field "role" Decode.string
                StopReason = get.Required.Field "stop_reason" Decode.string
                StopSequence = get.Optional.Field "stop_sequence" (Decode.list Decode.string)
                Type = get.Required.Field "type" Decode.string
                Usage = get.Required.Field "usage" tokenUsageDecoder
            })
        
    let decode json =
        Decode.decodeString decoder json
        
module AnthropicRequest =
    let decoder : Decoder<AnthropicRequest> =
        Decode.object (fun get ->
            {
                Model = get.Required.Field "model" Decode.string
                Messages = get.Required.Field "messages" (Decode.list (Decode.object (fun get -> {
                    Content = get.Required.Field "content" (Decode.list ContentBlock.decoder)
                    Role = get.Required.Field "role" Decode.string
                })))
                MaxTokens = get.Required.Field "max_tokens" Decode.int
                SystemPrompt = get.Optional.Field "system" Decode.string
                Tools = get.Optional.Field "tools" (Decode.list (Decode.object (fun get -> {
                    Description = get.Required.Field "description" Decode.string
                    Name = get.Required.Field "name" Decode.string
                    InputSchema = get.Required.Field "input_schema" Decode.value
                })))
            })
        
    let decode json =
        Decode.decodeString decoder json
        
        
    let messageEncoder (message: AnthropicMessage) =
        Encode.object [
            "content", message.Content |> List.map ContentBlock.encode |> Encode.list
            "role", Encode.string message.Role
        ]
        
    let toolEncoder (tool: AnthropicToolDefinition) =
        Encode.object [
            "description", Encode.string tool.Description
            "name", Encode.string tool.Name
            "input_schema", tool.InputSchema
        ]
    let optionalToolEncoder (tools: AnthropicToolDefinition list option) =
        let noToolsDefined = tools.Value.Length = 0   
        match noToolsDefined with
        | true -> Encode.nil
        | _ -> tools.Value |> List.map toolEncoder |> Encode.list
        
    let encode (request: AnthropicRequest  )=
        Encode.object [
            "model", Encode.string request.Model
            "messages", request.Messages |> List.map messageEncoder |> Encode.list
            "max_tokens", Encode.int request.MaxTokens
            "system", Encode.option Encode.string request.SystemPrompt
            if request.Tools.IsSome then
                if request.Tools.Value.Length > 0 then
                    "tools", request.Tools |> optionalToolEncoder 
           
        ]