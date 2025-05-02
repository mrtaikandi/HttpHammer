using HttpHammer.Configuration;

namespace HttpHammer.Http;

public interface IHttpRequestBuilder
{
    IHttpRequestMessage BuildRequest(BaseRequestDefinition definition, IDictionary<string, string> variables);
}