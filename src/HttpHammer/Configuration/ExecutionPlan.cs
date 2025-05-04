using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class ExecutionPlan
{
    [YamlIgnore]
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "YamlDotNet requires setters for deserialization.")]
    public string FilePath { get; set; } = string.Empty;

    [YamlMember(Alias = "variables")]
    public Dictionary<string, string> Variables { get; set; } = new();

    [YamlMember(Alias = "warmup")]
    public WarmupDefinition[] WarmupDefinitions { get; set; } = [];

    [YamlIgnore]
    public Definition[] Warmups
    {
        get => WarmupDefinitions.Length == 0 ? [] : WarmupDefinitions.Select(item => item.ToDefinition()).ToArray()!;
        init => WarmupDefinitions = value.Select(WarmupDefinition.From).ToArray();
    }

    [YamlMember(Alias = "requests")]
    public RequestDefinition[] Requests { get; set; } = [];
}