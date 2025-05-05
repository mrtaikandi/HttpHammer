using YamlDotNet.Serialization;

namespace HttpHammer.Plan.Definitions;

public class DelayDefinition : Definition
{
    [YamlMember(Alias = "duration")]
    public int Duration { get; set; }
}