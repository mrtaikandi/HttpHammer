using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class WarmupDefinition
{
    [YamlMember(Alias = "delay")]
    public DelayDefinition? Delay { get; set; }

    [YamlMember(Alias = "prompt")]
    public PromptDefinition? Prompt { get; set; }

    [YamlMember(Alias = "request")]
    public RequestDefinition? Request { get; set; }

    public static WarmupDefinition From(Definition definition) => definition switch
    {
        RequestDefinition request => new WarmupDefinition { Request = request },
        DelayDefinition delay => new WarmupDefinition { Delay = delay },
        PromptDefinition prompt => new WarmupDefinition { Prompt = prompt },
        _ => throw new InvalidOperationException($"Unknown definition type: {definition.GetType().Name}")
    };

    public Definition ToDefinition() => this switch
    {
        { Request: { } request } => request,
        { Delay: { } delay } => delay,
        { Prompt: { } prompt } => prompt,
        _ => throw new InvalidOperationException("No warmup definition found.")
    };
}