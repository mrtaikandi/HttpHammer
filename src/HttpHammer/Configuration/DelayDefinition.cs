using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class DelayDefinition : Definition
{
    [YamlMember(Alias = "duration")]
    public int Duration { get; set; }
}