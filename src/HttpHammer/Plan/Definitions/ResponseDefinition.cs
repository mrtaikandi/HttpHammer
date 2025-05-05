using YamlDotNet.Serialization;

namespace HttpHammer.Plan.Definitions;

public class ResponseDefinition
{
    [YamlMember(Alias = "status_code")]
    public int StatusCode { get; set; } = 200;

    public Dictionary<string, string>? Content { get; set; }

    [YamlMember(Alias = "headers")]
    public IDictionary<string, string>? Headers { get; set; }
}