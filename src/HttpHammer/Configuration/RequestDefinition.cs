using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class RequestDefinition : Definition
{
    [YamlMember(Alias = "body")]
    public string? Body { get; set; }

    [YamlMember(Alias = "concurrent_connections")]
    public int? ConcurrentConnections { get; set; }

    [YamlMember(Alias = "concurrent_requests")]
    public int? ConcurrentRequests
    {
        get => ConcurrentConnections;
        set => ConcurrentConnections = value;
    }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [YamlMember(Alias = "max_requests")]
    public int? MaxRequests { get; set; }

    [YamlMember(Alias = "method")]
    public string Method { get; set; } = "GET";

    [YamlMember(Alias = "response")]
    public ResponseDefinition? Response { get; set; }

    [YamlMember(Alias = "url")]
    public string Url { get; set; } = null!;
}