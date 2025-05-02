using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class ExecutionPlan
{
    [YamlIgnore]
    public string FilePath { get; set; } = string.Empty;

    [YamlMember(Alias = "variables")]
    public Dictionary<string, string> Variables { get; set; } = new();

    [YamlMember(Alias = "warmup")]
    public Dictionary<string, WarmupDefinition> WarmupRequests { get; set; } = new();

    [YamlMember(Alias = "requests")]
    public Dictionary<string, RequestDefinition> Requests { get; set; } = new();
}