using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace HttpHammer.Plan.Definitions;

public class ExecutionPlan
{
    [YamlIgnore]
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "YamlDotNet requires setters for deserialization.")]
    public string FilePath { get; set; } = string.Empty;

    [YamlMember(Alias = "variables")]
    public Dictionary<string, string> Variables { get; set; } = new();

    [YamlMember(Alias = "warmup")]
    public WarmupDefinition[] Warmup { get; set; } = [];

    [YamlIgnore]
    public Definition[] WarmupDefinitions
    {
        get => Warmup.Length == 0 ? [] : Warmup.Select(item => item.ToDefinition()).ToArray()!;
        init => Warmup = value.Select(WarmupDefinition.From).ToArray();
    }

    [YamlMember(Alias = "requests")]
    public RequestDefinition[] RequestDefinitions { get; set; } = [];
}