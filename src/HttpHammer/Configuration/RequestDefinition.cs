using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

public class RequestDefinition : BaseRequestDefinition
{
    public RequestDefinition()
    {
        MaxRequests = 100;
    }

    [YamlMember(Alias = "concurrent_connections")]
    public int ConcurrentConnections { get; set; } = 10;

    [YamlMember(Alias = "concurrent_requests")]
    public int ConcurrentRequests
    {
        get => ConcurrentConnections;
        set => ConcurrentConnections = value;
    }
}