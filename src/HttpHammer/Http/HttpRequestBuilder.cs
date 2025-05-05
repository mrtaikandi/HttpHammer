using System.Net.Mime;
using System.Text;
using HttpHammer.Plan.Definitions;
using HttpHammer.Processors;

namespace HttpHammer.Http;

internal class HttpRequestBuilder : IHttpRequestBuilder
{
    private readonly IVariableHandler _variableHandler;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequestBuilder(IVariableHandler variableHandler, IHttpClientFactory httpClientFactory)
    {
        _variableHandler = variableHandler;
        _httpClientFactory = httpClientFactory;
    }

    public IHttpRequestMessage BuildRequest(RequestDefinition definition, IDictionary<string, string> variables)
    {
        var url = _variableHandler.Substitute(definition.Url, variables);
        var httpMethod = new HttpMethod(definition.Method.ToUpperInvariant());
        var httpRequest = new System.Net.Http.HttpRequestMessage(httpMethod, url);

        httpRequest.Options.Set(new HttpRequestOptionsKey<string?>("RequestDefinitionName"), definition.Name);

        AddHeaders(httpRequest, definition, variables);
        AddBody(httpRequest, definition, variables);

        return new HttpRequestMessage(httpRequest, _httpClientFactory);
    }

    private void AddHeaders(System.Net.Http.HttpRequestMessage httpRequest, RequestDefinition definition, IDictionary<string, string> variables)
    {
        foreach (var header in definition.Headers)
        {
            var headerValue = _variableHandler.Substitute(header.Value, variables);
            httpRequest.Headers.TryAddWithoutValidation(header.Key, headerValue);
        }
    }

    private void AddBody(System.Net.Http.HttpRequestMessage httpRequest, RequestDefinition definition, IDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(definition.Body))
        {
            return;
        }

        var body = _variableHandler.Substitute(definition.Body, variables);
        var contentType = definition.Headers.SingleOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? MediaTypeNames.Application.Json;

        httpRequest.Content = new StringContent(body, Encoding.UTF8, contentType);
    }

    private class HttpRequestMessage : IHttpRequestMessage
    {
        private readonly System.Net.Http.HttpRequestMessage _request;
        private readonly IHttpClientFactory _httpClientFactory;

        public HttpRequestMessage(System.Net.Http.HttpRequestMessage request, IHttpClientFactory httpClientFactory)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <inheritdoc />
        public async Task<HttpResponseMessage> SendAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                return await client.SendAsync(_request, cancellationToken);
            }
            finally
            {
                _request.Dispose();
            }
        }
    }
}