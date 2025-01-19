namespace Noetix.LLM.Common;
public class ModelDefinition
{
    public required string Name { get; set; }
    public required string Model { get; set; }
    public string? Family { get; set; }
    public string? ParentModel { get; set; }
    public string? Description { get; set; }
    public int? MemoryRequired { get; set; }
    public string? Format { get; set; }
    public string? Quantization { get; set; }
    public string? ParameterSize { get; set; }
    public bool? SupportsJsonOutput { get; set; }
    public bool? SupportsTextInput { get; set; }
    public bool? SupportsTextOutput { get; set; }
    public bool? SupportsImageInput { get; set; }
    public bool? SupportsImageOutput { get; set; }
    public bool? SupportsAudioInput { get; set; }
    public bool? SupportsAudioOutput { get; set; }
    public int ContextWindow { get; set; }
    public int MaxTokens { get; set; }

}