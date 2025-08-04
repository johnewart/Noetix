namespace Codecs

open System.IO
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

type ContentBlock =  
    | TCB of TextContentBlock 
    | TUB of ToolUseBlock 
    | TRB of ToolResultBlock 
    | UnknownBlock


type TokenUsage =
    {
        InputTokens: int option
        OutputTokens: int option
        CacheCreationInputTokens: int option
        CacheReadInputTokens: int option
    }
    
module TokenUsage =
    let decoder =
        Decode.object (fun get -> {
            InputTokens = get.Optional.Field "input_tokens" Decode.int
            OutputTokens = get.Optional.Field "output_tokens" Decode.int
            CacheCreationInputTokens = get.Optional.Field "cache_creation_input_tokens" Decode.int
            CacheReadInputTokens = get.Optional.Field "cache_read_input_tokens" Decode.int
        })

type AnthropicMessage =
    {
        Content: ContentBlock list
        Role: string
    }

type CacheControl =
    {
        Type: string
    }
    
type AnthropicToolDefinition =
    {
        Name: string
        Description: string
        InputSchema: JsonValue
        CacheControl: CacheControl option
    }
    
type SystemPromptBlock =
   {
       Type: string 
       Text: string
       CacheControl: CacheControl option
   }

type AnthropicRequest =
    {
        Model: string
        MaxTokens: int
        SystemPrompt: SystemPromptBlock list option
        Tools: AnthropicToolDefinition list option
        Messages: AnthropicMessage list
        Stream: bool
    }

type AnthropicResponse =
    {
        Content: ContentBlock list
        Id: string
        Model: string
        Role: string
        StopReason: string option
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

module CacheControl =
    let decoder: Decoder<CacheControl> =
        Decode.object (fun get -> {
            Type = get.Required.Field "type" Decode.string
        })

    let encoder(cacheControl: CacheControl) =
        Encode.object [
            "type", Encode.string cacheControl.Type
        ]
        
module SystemPromptBlock =
    let decoder: Decoder<SystemPromptBlock> =
        Decode.object (fun get -> {
            Type = get.Required.Field "type" Decode.string
            Text = get.Required.Field "text" Decode.string
            CacheControl = get.Optional.Field "cache_control" (Decode.object (fun get -> {
                Type = get.Required.Field "type" Decode.string
            }))
        })

    let encoder(prompt: SystemPromptBlock) =
        Encode.object [
            "type", Encode.string prompt.Type
            "text", Encode.string prompt.Text
            "cache_control", Encode.option CacheControl.encoder prompt.CacheControl
        ]

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
    
    let decode json =
        Decode.decodeString decoder json
    
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
//
// module MessageBlock =
//     let tokenUsageDecoder =
//         Decode.object (fun get -> {
//             InputTokens = get.Required.Field "input_tokens" Decode.int
//             OutputTokens = get.Required.Field "output_tokens" Decode.int
//         })
//     let decoder =
//         Decode.object(
//             fun get -> {
//                 Id = get.Required.Field "id" Decode.string
//                 Type = get.Required.Field "type" Decode.string
//                 Role = get.Required.Field "role" Decode.string
//                 Model = get.Required.Field "model" Decode.string
//                 StopSequence = get.Optional.Field "stop_sequence" (Decode.list Decode.string)
//                 Usage = get.Required.Field "usage" TokenUsage.decoder
//                 Content = get.Required.Field "content" (Decode.list ContentBlock.decoder)
//                 StopReason = get.Required.Field "stop_reason" Decode.string
//             }
//         )


module AnthropicResponse =
    // let tokenUsageDecoder =
    //     Decode.object (fun get -> {
    //         InputTokens = get.Required.Field "input_tokens" Decode.int
    //         OutputTokens = get.Required.Field "output_tokens" Decode.int
    //     })

    let decoder : Decoder<AnthropicResponse> =
        Decode.object (fun get ->
            {
                Id = get.Required.Field "id" Decode.string
                Content = get.Required.Field "content" (Decode.list ContentBlock.decoder)
                Model = get.Required.Field "model" Decode.string
                Role =  get.Required.Field "role" Decode.string
                StopReason = get.Optional.Field "stop_reason" Decode.string
                StopSequence = get.Optional.Field "stop_sequence" (Decode.list Decode.string)
                Type = get.Required.Field "type" Decode.string
                Usage = get.Required.Field "usage" TokenUsage.decoder
            })

    let decode json =
        Decode.decodeString decoder json

module AnthropicRequest =
    let decoder : Decoder<AnthropicRequest> =
        Decode.object (fun get ->
            {
                Model = get.Required.Field "model" Decode.string
                SystemPrompt = get.Optional.Field "system" (Decode.list SystemPromptBlock.decoder)
                Messages = get.Required.Field "messages" (Decode.list (Decode.object (fun get -> {
                    Content = get.Required.Field "content" (Decode.list ContentBlock.decoder)
                    Role = get.Required.Field "role" Decode.string
                })))
                MaxTokens = get.Required.Field "max_tokens" Decode.int
                Stream = get.Required.Field "stream" Decode.bool
                Tools = get.Optional.Field "tools" (Decode.list (Decode.object (fun get -> {
                    Description = get.Required.Field "description" Decode.string
                    Name = get.Required.Field "name" Decode.string
                    InputSchema = get.Required.Field "input_schema" Decode.value
                    CacheControl = get.Optional.Field "cache_control" (Decode.object (fun get -> {
                        Type = get.Required.Field "type" Decode.string
                    }))
                })))
            })

    let decode json =
        Decode.decodeString decoder json


    let messageEncoder (message: AnthropicMessage) =
        Encode.object [
            "content", message.Content |> List.map ContentBlock.encode |> Encode.list
            "role", Encode.string message.Role
        ]
        
    let systemPromptEncoder (prompt: SystemPromptBlock list option) =
        match prompt with
        | Some p -> p |> List.map SystemPromptBlock.encoder |> Encode.list
        | None -> Encode.nil

    let toolEncoder (tool: AnthropicToolDefinition) =
        Encode.object [
            "description", Encode.string tool.Description
            "name", Encode.string tool.Name
            "input_schema", tool.InputSchema
            if tool.CacheControl.IsSome then
                "cache_control", Encode.option CacheControl.encoder tool.CacheControl             
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
            "system", request.SystemPrompt |> systemPromptEncoder
            "stream", Encode.bool request.Stream
            if request.Tools.IsSome then
                if request.Tools.Value.Length > 0 then
                    "tools", request.Tools |> optionalToolEncoder

        ]


type MessageStart =
    {
        Message: AnthropicResponse
    }
    
module MessageStart =
    let decoder =
        Decode.object (fun get -> {
            Message = get.Required.Field "message" AnthropicResponse.decoder
        })
        
type MessageStop =
    {
       Id: Option<string>
    }

module MessageStop =
    let decoder =
        Decode.object (fun get -> {
            Id = get.Optional.Field "id" Decode.string
        })
        
type ContentBlockStart =
    {
        Index: int
        ContentBlock: ContentBlock
    }
    
module ContentBlockStart =
    let decoder =
        Decode.object (fun get -> {
            Index = get.Required.Field "index" Decode.int
            ContentBlock = get.Required.Field "content_block" ContentBlock.decoder
        })
        

type JsonDelta =
    {
        Type: string
        PartialJson: string
    }

module JsonDelta =
    let decoder:Decoder<JsonDelta>  =
        Decode.object (fun get -> {
            Type = get.Required.Field "type" Decode.string
            PartialJson = get.Required.Field "partial_json" Decode.string
        })

type TextDelta =
    {
        Type: string
        Text: string
    }
    
module TextDelta =
    let decoder:Decoder<TextDelta>  =
        Decode.object (fun get -> {
            Type = get.Required.Field "type" Decode.string
            Text = get.Required.Field "text" Decode.string
        })

               
type DeltaBlock =
    | TextDelta of TextDelta
    | JsonDelta of JsonDelta

module DeltaBlock =
    let decoder : Decoder<DeltaBlock> =
        Decode.field "type" Decode.string
        |> Decode.andThen
            (function
            | "input_json_delta" -> Decode.map (fun block -> JsonDelta block) JsonDelta.decoder
            | "text_delta" -> Decode.map (fun block -> TextDelta block) TextDelta.decoder
            | invalid -> Decode.fail $"Failed to decode `%s{invalid}` it's an invalid case for `ContentBlock`")
            
type ContentBlockDelta =
    {
        Index: int
        Delta: DeltaBlock
    }
    
module ContentBlockDelta =
    let decoder =
        Decode.object (fun get -> {
            Index = get.Required.Field "index" Decode.int
            Delta = get.Required.Field "delta" DeltaBlock.decoder
        })
        
type ContentBlockStop =
    {
        Index: int
    }
    
module ContentBlockStop =
    let decoder =
        Decode.object (fun get -> {
            Index = get.Required.Field "index" Decode.int
        })
        
type Delta =
    {
        StopReason: string
        StopSequence: string list option
    }
    
module Delta =
    let decoder: Decoder<Delta> =
        Decode.object (fun get -> {
            StopReason = get.Required.Field "stop_reason" Decode.string
            StopSequence = get.Optional.Field "stop_sequence" (Decode.list Decode.string)
        })
        
type MessageDelta =
    {
        Delta: Delta
        Usage: TokenUsage
    }
        
module MessageDelta =
    let decoder =
        Decode.object (fun get -> {
            Delta = get.Required.Field "delta" (Decode.object (fun get -> {
                StopReason = get.Required.Field "stop_reason" Decode.string
                StopSequence = get.Optional.Field "stop_sequence" (Decode.list Decode.string)
            }))
            Usage = get.Required.Field "usage" TokenUsage.decoder
        })
 
 
 type Ping =
    {
        Response: Option<string>
    }
    
module Ping =
    let decoder =
        Decode.object (fun get -> {
            Response = get.Optional.Field "response" Decode.string
        })
        
               
type StreamBlock =
    | MessageStart of MessageStart
    | ContentBlockStart of ContentBlockStart
    | ContentBlockDelta of ContentBlockDelta
    | ContentBlockStop of ContentBlockStop
    | MessageDelta of MessageDelta
    | MessageStop of MessageStop
    | Ping of Ping
    

module StreamBlock =    
    let decoder: Decoder<StreamBlock> =
        Decode.field "type" Decode.string
        |> Decode.andThen
            (function
            | "message_start" -> Decode.map (fun block -> MessageStart block) MessageStart.decoder
            | "content_block_start" -> Decode.map (fun block -> ContentBlockStart block) ContentBlockStart.decoder
            | "content_block_delta" -> Decode.map (fun block -> ContentBlockDelta block) ContentBlockDelta.decoder
            | "content_block_stop" -> Decode.map (fun block -> ContentBlockStop block) ContentBlockStop.decoder
            | "message_delta" -> Decode.map (fun block -> MessageDelta block) MessageDelta.decoder
            | "message_stop" -> Decode.map (fun block -> MessageStop block) MessageStop.decoder
            | "ping" -> Decode.map (fun block -> Ping block) Ping.decoder
            | invalid -> Decode.fail $"Failed to decode `%s{invalid}` it's an invalid case for `ContentBlock`")
    
    let decode json =
        Decode.decodeString decoder json
