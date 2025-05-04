using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class PromptDefinition : Definition
{
    [YamlMember(Alias = "message")]
    public string Message { get; set; } = string.Empty;

    [YamlMember(Alias = "variable")]
    public string Variable { get; set; } = string.Empty;

    [YamlMember(Alias = "default")]
    public string? Default { get; set; }

    [YamlMember(Alias = "secret")]
    public bool Secret { get; set; }

    [YamlMember(Alias = "allow_empty")]
    public bool AllowEmpty { get; set; }
}