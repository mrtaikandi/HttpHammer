using System.Net;
using HttpHammer.Http;
using HttpHammer.Plan.Definitions;
using HttpHammer.Processors;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpHammer.Tests.Processors;

public class RequestProcessorTests
{
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IVariableHandler _variableHandler;
    private readonly RequestProcessor _sut;
    private readonly IHttpRequestMessage _httpRequestMessage;

    public RequestProcessorTests()
    {
        var logger = NullLogger<RequestProcessor>.Instance;
        _requestBuilder = Substitute.For<IHttpRequestBuilder>();
        _variableHandler = Substitute.For<IVariableHandler>();
        _sut = new RequestProcessor(logger, _requestBuilder, _variableHandler);

        _httpRequestMessage = Substitute.For<IHttpRequestMessage>();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDefinition_ThrowsProcessorException()
    {
        // Arrange
        var definition = new DelayDefinition { Name = "Test Delay" };
        var context = new ProcessorContext(definition, new Dictionary<string, string>());

        // Act & Assert
        var exception = await Should.ThrowAsync<ProcessorException>(() => _sut.ExecuteAsync(context));
        exception.Message.ShouldContain("RequestProcessor can only process RequestDefinition");
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroMaxRequests_ReturnsSuccessWithoutSendingRequests()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Zero Requests Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 0
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        _requestBuilder.DidNotReceive().BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSynchronousRequest_SendsRequestCorrectly()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Sync Request Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 2,
            ConcurrentConnections = 1
        };

        var variables = new Dictionary<string, string>();
        var context = new ProcessorContext(request, variables);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        await _httpRequestMessage.Received(2).SendAsync(Arg.Any<CancellationToken>());
        variables.ShouldContainKey("iteration");
        variables["iteration"].ShouldBe("2"); // Last iteration value
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentRequests_SendsAllRequestsInParallel()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Concurrent Requests Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 5,
            ConcurrentConnections = 3
        };

        var variables = new Dictionary<string, string>();
        var context = new ProcessorContext(request, variables);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        await _httpRequestMessage.Received(5).SendAsync(Arg.Any<CancellationToken>());
        variables.ShouldContainKey("iteration");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_StopsProcessingRequests()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Cancellation Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 100,
            ConcurrentConnections = 10
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        var cts = new CancellationTokenSource();

        // Configure the message to cancel after first call
        var callCount = 0;
        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_ => _httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++callCount == 1)
                {
                    cts.Cancel();
                }

                if (cts.Token.IsCancellationRequested && callCount > 1)
                {
                    throw new TaskCanceledException();
                }

                return responseMessage;
            });

        // Act & Assert
        await Should.ThrowAsync<TaskCanceledException>(async () => await _sut.ExecuteAsync(context, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WithRequestException_ReturnsFailure()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Exception Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Request failed"));

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<ErrorProcessorResult>();
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithResponseStatusCodeMismatch_ReturnsFailure()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Status Code Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1,
            Response = new ResponseDefinition { StatusCode = 201 }
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK); // 200, not 201

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<ErrorProcessorResult>();
        ((ErrorProcessorResult)result).Errors.ShouldContain(e => e.Contains("failed with status code"));
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaderVariableExtraction_ExtractsAndStoresVariables()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Header Extraction Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1,
            Response = new ResponseDefinition
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "X-Token", "${token}" },
                    { "X-Session-Id", "${sessionId}" }
                }
            }
        };

        var variables = new Dictionary<string, string>();
        var context = new ProcessorContext(request, variables);

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        responseMessage.Headers.Add("X-Token", "abc123");
        responseMessage.Headers.Add("X-Session-Id", "xyz789");

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Important: Set up the variable handler mock to properly handle both variables
        _variableHandler.TryGetAssignmentVariableName("${token}", out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "token";
                return true;
            });

        _variableHandler.TryGetAssignmentVariableName("${sessionId}", out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "sessionId";
                return true;
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        variables.ShouldContainKey("token");
        variables["token"].ShouldBe("abc123");
        variables.ShouldContainKey("sessionId");
        variables["sessionId"].ShouldBe("xyz789");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingHeaderForVariableExtraction_ReturnsFailure()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Missing Header Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1,
            Response = new ResponseDefinition
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "X-Missing-Header", "${missingValue}" }
                }
            }
        };

        var variables = new Dictionary<string, string>();
        var context = new ProcessorContext(request, variables);

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        // Deliberately not adding the expected header
        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<ErrorProcessorResult>();
        ((ErrorProcessorResult)result).Errors.ShouldContain(e => e.Contains("Failed to extract 'X-Missing-Header'"));
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressTracking_UpdatesProgress()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Progress Test",
            Url = "https://example.com",
            Method = "GET",
            MaxRequests = 3,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(
            request,
            new Dictionary<string, string>(),
            new FakeProgressContext());

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        var progress = (context.Progress as FakeProgressContext).ShouldNotBeNull().Progress;
        progress.CurrentValue.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithPostMethod_SendsRequestBodyCorrectly()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "POST Request Test",
            Url = "https://example.com/api",
            Method = "POST",
            Body = "{\"key\":\"value\"}",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            },
            MaxRequests = 1,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.Created);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        _requestBuilder.Received(1).BuildRequest(
            Arg.Is<RequestDefinition>(r =>
                r.Method == "POST" &&
                r.Body == "{\"key\":\"value\"}" &&
                r.Headers.ContainsKey("Content-Type") &&
                r.Headers["Content-Type"] == "application/json"),
            Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_WithContentVariableExtraction_ExtractsAndStoresVariables()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Content Extraction Test",
            Url = "https://example.com/api",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1,
            Response = new ResponseDefinition
            {
                StatusCode = 200,
                Content = new Dictionary<string, string>
                {
                    { "$.id", "${userId}" },
                    { "$.token", "${authToken}" }
                }
            }
        };

        var variables = new Dictionary<string, string>();
        var context = new ProcessorContext(request, variables);

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"12345\",\"token\":\"bearer-xyz-789\"}")
        };

        responseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Set up variable handler mock
        _variableHandler.TryGetAssignmentVariableName("${userId}", out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "userId";
                return true;
            });

        _variableHandler.TryGetAssignmentVariableName("${authToken}", out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "authToken";
                return true;
            });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        variables.ShouldContainKey("userId");
        variables["userId"].ShouldBe("12345");
        variables.ShouldContainKey("authToken");
        variables["authToken"].ShouldBe("bearer-xyz-789");
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryParameters_SendsUrlWithParameters()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Query Parameters Test",
            Url = "https://example.com/api?param1=value1&param2=value2",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        _requestBuilder.Received(1).BuildRequest(
            Arg.Is<RequestDefinition>(r => r.Url.Contains("?param1=value1&param2=value2")),
            Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAuthenticationHeaders_SendsRequestWithAuthHeader()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Authentication Test",
            Url = "https://example.com/secure-api",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" }
            },
            MaxRequests = 1,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        _requestBuilder.Received(1).BuildRequest(
            Arg.Is<RequestDefinition>(r =>
                r.Headers.ContainsKey("Authorization") &&
                r.Headers["Authorization"] == "Bearer token123"),
            Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_With401Unauthorized_ReturnsFailureWithSpecificMessage()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Unauthorized Test",
            Url = "https://example.com/secure-api",
            Method = "GET",
            MaxRequests = 1,
            ConcurrentConnections = 1,
            Response = new ResponseDefinition { StatusCode = 200 } // Expect 200, will get 401
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.Unauthorized); // 401 status

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        var errorResult = result.ShouldBeOfType<ErrorProcessorResult>();
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Request 'Unauthorized Test' failed with status code Unauthorized. Expected 200");
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentContentTypes_HandlesContentTypeCorrectly()
    {
        // Arrange
        var request = new RequestDefinition
        {
            Name = "Content Type Test",
            Url = "https://example.com/api",
            Method = "POST",
            Body = "key=value&another=test",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/x-www-form-urlencoded" }
            },
            MaxRequests = 1,
            ConcurrentConnections = 1
        };

        var context = new ProcessorContext(request, new Dictionary<string, string>());
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);

        _requestBuilder.BuildRequest(Arg.Any<RequestDefinition>(), Arg.Any<IDictionary<string, string>>())
            .Returns(_httpRequestMessage);

        _httpRequestMessage.SendAsync(Arg.Any<CancellationToken>())
            .Returns(responseMessage);

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<SuccessProcessorResult>();
        _requestBuilder.Received(1).BuildRequest(
            Arg.Is<RequestDefinition>(r =>
                r.Headers["Content-Type"] == "application/x-www-form-urlencoded" &&
                r.Body == "key=value&another=test"),
            Arg.Any<IDictionary<string, string>>());
    }
}