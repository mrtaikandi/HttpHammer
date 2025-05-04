using HttpHammer.Configuration;

namespace HttpHammer.Http;

public interface IHttpRequestBuilder
{
    IHttpRequestMessage BuildRequest(RequestDefinition definition, IDictionary<string, string> variables);
}