using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class BaseRequestDefinition
{
    [YamlMember(Alias = "body")]
    public string? Body { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [YamlMember(Alias = "max_requests")]
    public int MaxRequests { get; set; }

    [YamlMember(Alias = "method")]
    public string Method { get; set; } = "GET";

    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "response")]
    public ResponseDefinition? Response { get; set; }

    [YamlMember(Alias = "url")]
    public string Url { get; set; } = null!;
}