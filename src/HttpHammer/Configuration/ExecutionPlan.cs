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
    public Definition[] WarmupRequests { get; set; } = [];

    [YamlMember(Alias = "requests")]
    public RequestDefinition[] Requests { get; set; } = [];
}