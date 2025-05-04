using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class Definition
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlIgnore]
    internal Guid Id { get; } = Guid.NewGuid();
}