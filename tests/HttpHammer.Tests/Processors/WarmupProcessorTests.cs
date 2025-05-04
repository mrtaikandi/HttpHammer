using System.Net;
using System.Text;
using HttpHammer.Configuration;
using HttpHammer.Console;
using HttpHammer.Http;
using HttpHammer.Internals;
using HttpHammer.Processors;
using Microsoft.Extensions.Logging;
using VariableHandler = HttpHammer.Internals.VariableHandler;

namespace HttpHammer.Tests.Processors;

public class WarmupProcessorTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FakeHttpMessageHandler _httpMessageHandler;
    private readonly IHttpRequestBuilder _requestBuilder;
    private readonly IVariableHandler _variableHandler;

    public WarmupProcessorTests()
    {
        _httpMessageHandler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(_httpMessageHandler);

        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient().Returns(httpClient);

        _variableHandler = new VariableHandler();
        _requestBuilder = new HttpRequestBuilder(_variableHandler, _httpClientFactory);
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorOccurs_ReportsCompletedProgress()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Error Request",
                Url = "https://example.com/api/error",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200 // We'll return 400 to cause an error
                }
            },
            new RequestDefinition
            {
                Name = "Never Executed",
                Url = "https://example.com/api/never",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var progress = new SynchronousProgress();
        var progressContext = new FakeProgressContext(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new WarmupProcessor(
            Substitute.For<ILogger<WarmupProcessor>>(),
            _requestBuilder,
            _variableHandler);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        progress.MaximumValue.ShouldBe(2);
        progress.CurrentValue.ShouldBe(2);
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsonExtractionFails_AddsErrorToPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "user.nonexistent", "=>{userId}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var responseJson = "{ \"user\": { \"id\": \"12345\", \"name\": \"John Doe\" } }";
        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = new FakeProgressContext();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("Failed to extract 'user.nonexistent'");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJsonParsingFails_AddsErrorToPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "user.id", "=>{userId}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var responseJson = "invalid json";
        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusCodeMismatch_AddsErrorToPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("failed with status code BadRequest");
        errorResult.Errors[0].ShouldContain("Expected 200");
    }

    [Fact]
    public async Task ExecuteAsync_WhenWarmupRequestFails_ReturnsOriginalPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.ShouldBeOfType<ErrorProcessorResult>();
    }

    [Fact]
    public async Task ExecuteAsync_WithBodyContent_SendsBodyInRequest()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "POST",
                Body = "test body content"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        var capturedRequest = _httpMessageHandler.LastRequest;
        capturedRequest.ShouldNotBeNull();
        _httpMessageHandler.RequestContents.Last().ShouldBe("test body content");
    }

    [Fact]
    public async Task ExecuteAsync_WithBothHeaderAndJsonExtraction_PopulatesAllVariables()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Request-Id", "=>{requestId}" }
                    },
                    Content = new Dictionary<string, string>
                    {
                        { "user.id", "=>{userId}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Request-Id", "abc123");
        response.Content = new StringContent("""{ "user": { "id": "12345" } }""", Encoding.UTF8, "application/json");
        _httpMessageHandler.SetResponse(response);

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(2);
        successResult.ExecutionPlan.Variables.ShouldContainKey("requestId");
        successResult.ExecutionPlan.Variables["requestId"].ShouldBe("abc123");
        successResult.ExecutionPlan.Variables.ShouldContainKey("userId");
        successResult.ExecutionPlan.Variables["userId"].ShouldBe("12345");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ReportsProgressUpToCancellation()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Cancelled Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        var progress = new SynchronousProgress();
        var progressContext = new FakeProgressContext(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new WarmupProcessor(
            Substitute.For<ILogger<WarmupProcessor>>(),
            _requestBuilder,
            _variableHandler);

        // Act
        var result = await processor.ExecuteAsync(context, cts.Token);

        // Assert
        progress.MaximumValue.ShouldBe(1);
        progress.CurrentValue.ShouldBe(1);
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors[0].ShouldContain("cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_RespectsToken()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var actualPlan = await processor.ExecuteAsync(context, cts.Token);

        // Assert
        actualPlan.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)actualPlan;
        errorResult.Errors.ShouldNotBeEmpty();
        errorResult.Errors[0].ShouldContain("Warmup request execution was cancelled.");
    }

    [Fact]
    public async Task ExecuteAsync_WithComplexJsonExtraction_CorrectlyExtractsValues()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "data.user.profile.id", "=>{userId}" },
                        { "data.user.profile.details.name", "=>{userName}" },
                        { "data.meta.count", "=>{count}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        const string ResponseJson = """
                                    {
                                        "data": {
                                            "user": {
                                                "profile": {
                                                    "id": "abc123",
                                                    "details": {
                                                        "name": "Jane Smith"
                                                    }
                                                }
                                            },
                                            "meta": {
                                                "count": 42
                                            }
                                        }
                                    }
                                    """;

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(3);
        successResult.ExecutionPlan.Variables.ShouldContainKey("userId");
        successResult.ExecutionPlan.Variables["userId"].ShouldBe("abc123");
        successResult.ExecutionPlan.Variables.ShouldContainKey("userName");
        successResult.ExecutionPlan.Variables["userName"].ShouldBe("Jane Smith");
        successResult.ExecutionPlan.Variables.ShouldContainKey("count");
        successResult.ExecutionPlan.Variables["count"].ShouldBe("42");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyResponseContent_HandlesGracefully()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 204, // No Content
                    Content = new Dictionary<string, string>
                    {
                        { "data", "=>{value}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("Failed to read response content");
    }

    [Fact]
    public async Task ExecuteAsync_WithExtractedHeaderVariables_UsesThemInSubsequentRequests()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "First Request",
                Url = "https://example.com/api/auth",
                Method = "POST",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Auth-Token", "=>{authToken}" }
                    }
                }
            },
            new RequestDefinition
            {
                Name = "Second Request",
                Url = "https://example.com/api/data",
                Method = "GET",
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer ${authToken}" }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        firstResponse.Headers.Add("X-Auth-Token", "secret-token-123");

        var secondResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        _httpMessageHandler.SetResponseQueue([firstResponse, secondResponse]);

        // Use a separate client for each call
        _httpClientFactory.CreateClient().Returns(
            new HttpClient(_httpMessageHandler),
            new HttpClient(_httpMessageHandler));

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.ShouldContainKey("authToken");
        successResult.ExecutionPlan.Variables["authToken"].ShouldBe("secret-token-123");

        var secondRequest = _httpMessageHandler.Requests.Skip(1).First();
        secondRequest.Headers.GetValues("Authorization").First().ShouldBe("Bearer secret-token-123");
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaderExtraction_PopulatesVariables()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Request-Id", "=>{requestId}" },
                        { "X-Rate-Limit", "=>{rateLimit}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        response.Headers.Add("X-Request-Id", "abc123");
        response.Headers.Add("X-Rate-Limit", "100");
        _httpMessageHandler.SetResponse(response);

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(2);
        successResult.ExecutionPlan.Variables.ShouldContainKey("requestId");
        successResult.ExecutionPlan.Variables["requestId"].ShouldBe("abc123");
        successResult.ExecutionPlan.Variables.ShouldContainKey("rateLimit");
        successResult.ExecutionPlan.Variables["rateLimit"].ShouldBe("100");
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaders_SendsHeadersInRequest()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Headers = new Dictionary<string, string>
                {
                    { "X-Test-Header", "TestValue" },
                    { "Authorization", "Bearer token" }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        var capturedRequest = _httpMessageHandler.LastRequest;
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.GetValues("X-Test-Header").First().ShouldBe("TestValue");
        capturedRequest.Headers.GetValues("Authorization").First().ShouldBe("Bearer token");
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonExtraction_PopulatesVariables()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "user.id", "=>{userId}" },
                        { "user.name", "=>{name}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        const string ResponseJson = """{ "user": { "id": "12345", "name": "John Doe" } }""";
        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(2);
        successResult.ExecutionPlan.Variables.ShouldContainKey("userId");
        successResult.ExecutionPlan.Variables["userId"].ShouldBe("12345");
        successResult.ExecutionPlan.Variables.ShouldContainKey("name");
        successResult.ExecutionPlan.Variables["name"].ShouldBe("John Doe");
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonParseError_ReturnsFalse()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "user.id", "={userId}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var responseJson = "invalid json";
        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingHeaderExtraction_AddsErrorToPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Nonexistent-Header", "value" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };

        response.Headers.Add("X-Different-Header", "some-value");
        _httpMessageHandler.SetResponse(response);

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("Failed to extract 'X-Nonexistent-Header'");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingJsonPath_ReturnsFalse()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "user.nonexistent", "=>{userId}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        const string ResponseJson = """{ "user": { "id": "12345", "name": "John Doe" } }""";
        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRequests_ExecutesInOrder()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "First Request",
                Url = "https://example.com/api/first",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "token", "=>{authToken}" }
                    }
                }
            },
            new RequestDefinition
            {
                Name = "Second Request",
                Url = "https://example.com/api/second",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer ${authToken}" }
                }
            }
        ];

        var variables = new Dictionary<string, string>();

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = variables
        };

        const string FirstResponseJson = """{ "token": "secret-token" }""";

        _httpMessageHandler.SetResponseQueue([
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FirstResponseJson, Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }
        ]);

        // Use a separate client for each call to avoid disposing issues
        _httpClientFactory.CreateClient().Returns(
            new HttpClient(_httpMessageHandler),
            new HttpClient(_httpMessageHandler));

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.ShouldContainKey("authToken");
        successResult.ExecutionPlan.Variables["authToken"].ShouldBe("secret-token");

        var secondRequest = _httpMessageHandler.Requests.Skip(1).First();
        secondRequest.Headers.GetValues("Authorization").First().ShouldBe("Bearer secret-token");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRequests_StopsExecutionAfterFirstError()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "First Request",
                Url = "https://example.com/api/first",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200 // We'll make this fail by returning 400
                }
            },
            new RequestDefinition
            {
                Name = "Second Request",
                Url = "https://example.com/api/second",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("First Request");

        // Verify only the first request was executed
        _httpMessageHandler.Requests.Count.ShouldBe(1);
        _httpMessageHandler.Requests[0].RequestUri!.ToString().ShouldContain("/first");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleWarmupRequests_ReportsProgressForEach()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "First Request",
                Url = "https://example.com/api/first",
                Method = "GET"
            },
            new RequestDefinition
            {
                Name = "Second Request",
                Url = "https://example.com/api/second",
                Method = "GET"
            },
            new RequestDefinition
            {
                Name = "Third Request",
                Url = "https://example.com/api/third",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponseQueue([
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }
        ]);

        // Use a separate client for each call
        _httpClientFactory.CreateClient().Returns(
            new HttpClient(_httpMessageHandler),
            new HttpClient(_httpMessageHandler),
            new HttpClient(_httpMessageHandler));

        var progressValues = new List<double>();
        var progress = new SynchronousProgress(v => progressValues.Add(v));
        var progressContext = new FakeProgressContext(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new WarmupProcessor(
            Substitute.For<ILogger<WarmupProcessor>>(),
            _requestBuilder,
            _variableHandler);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        progress.MaximumValue.ShouldBe(3);
        progressValues.Count.ShouldBe(3);
        progressValues[0].ShouldBe(1, double.Epsilon);
        progressValues[1].ShouldBe(2, double.Epsilon);
        progressValues[2].ShouldBe(3, double.Epsilon);
    }

    [Fact]
    public async Task ExecuteAsync_WithNestedJsonArray_HandlesCorrectly()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "items[0].id", "=>{firstItemId}" },
                        { "items.1.name", "=>{secondItemName}" } // incorrect JSON path.
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        const string ResponseJson = """
                                    {
                                        "items": [
                                            { "id": "item1", "name": "First Item" },
                                            { "id": "item2", "name": "Second Item" }
                                        ]
                                    }
                                    """;

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("Failed to extract 'items.1.name'");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoWarmupRequests_ReportsFullProgressImmediately()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = [],
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var progress = new SynchronousProgress();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new WarmupProcessor(
            Substitute.For<ILogger<WarmupProcessor>>(),
            _requestBuilder,
            _variableHandler);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        progress.MaximumValue.ShouldBe(0);
        progress.CurrentValue.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoWarmupRequests_ReturnsOriginalPlan()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = [],
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        ((SuccessProcessorResult)result).ExecutionPlan.ShouldBeSameAs(plan);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullProgress_DoesNotThrowException()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act & Assert (no exception should be thrown)
        await Should.NotThrowAsync(async () =>
            await processor.ExecuteAsync(context));
    }

    [Fact]
    public async Task ExecuteAsync_WithRecursiveVariableReplacement_ResolvesCorrectly()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "First Request",
                Url = "https://example.com/api/first",
                Method = "GET",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "baseUrl", "=>{baseUrl}" }
                    }
                }
            },
            new RequestDefinition
            {
                Name = "Second Request",
                Url = "${baseUrl}/path/${endpoint}",
                Method = "GET"
            }
        ];

        var variables = new Dictionary<string, string>
        {
            { "endpoint", "users" }
        };

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = variables
        };

        _httpMessageHandler.SetResponseQueue([
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "baseUrl": "https://api.example.org" }""", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            }
        ]);

        // Use a separate client for each call to avoid disposing issues
        _httpClientFactory.CreateClient().Returns(
            new HttpClient(_httpMessageHandler),
            new HttpClient(_httpMessageHandler));

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        var secondRequest = _httpMessageHandler.Requests.Skip(1).First();
        secondRequest.RequestUri!.ToString().ShouldBe("https://api.example.org/path/users");
    }

    [Fact]
    public async Task ExecuteAsync_WithRequestException_ThrowsException()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        // Simulate a network error
        _httpMessageHandler.ShouldThrowException = true;
        _httpMessageHandler.ExceptionToThrow = new HttpRequestException("Connection failed");

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var actualPlan = await processor.ExecuteAsync(context);

        // Assert
        actualPlan.HasErrors.ShouldBeTrue();
        actualPlan.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)actualPlan;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldContain("Connection failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleWarmupRequest_ReportsProgressOnce()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var progress = new SynchronousProgress();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>())
            .Returns(callInfo =>
            {
                progress.MaxValue(callInfo.ArgAt<int>(1));
                return progress;
            });

        var context = new ProcessorContext(plan, progressContext);
        var processor = new WarmupProcessor(
            Substitute.For<ILogger<WarmupProcessor>>(),
            _requestBuilder,
            _variableHandler);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        progress.MaximumValue.ShouldBe(1);
        progress.CurrentValue.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulWarmupRequests_ReturnsOriginalPlan()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api",
                Method = "GET"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        ((SuccessProcessorResult)result).ExecutionPlan.ShouldBeSameAs(plan);
    }

    [Fact]
    public async Task ExecuteAsync_WithVariables_ReplacesVariablesInUrlHeadersAndBody()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Test Request",
                Url = "https://example.com/api/${path}",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    { "X-User-Id", "${userId}" }
                },
                Body = """{ "name": "${name}" }"""
            }
        ];

        var variables = new Dictionary<string, string>
        {
            { "path", "users" },
            { "userId", "12345" },
            { "name", "John Doe" }
        };

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = variables
        };

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        var capturedRequest = _httpMessageHandler.LastRequest;
        capturedRequest.ShouldNotBeNull();
        capturedRequest.RequestUri!.ToString().ShouldBe("https://example.com/api/users");
        capturedRequest.Headers.GetValues("X-User-Id").First().ShouldBe("12345");
        _httpMessageHandler.RequestContents.Last().ShouldBe("{ \"name\": \"John Doe\" }");
    }

    [Fact]
    public async Task ExecuteAsync_WithFormUrlEncodedContent_SendsCorrectlyToAuthEndpoint()
    {
        // Arrange
        Definition[] warmupRequests =
        [
            new RequestDefinition
            {
                Name = "Auth Request",
                Url = "https://example.com/api/auth/token",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/x-www-form-urlencoded" }
                },
                Body = "grant_type=client_credentials&client_id=test_client&client_secret=test_secret",
                Response = new ResponseDefinition
                {
                    StatusCode = 200,
                    Content = new Dictionary<string, string>
                    {
                        { "access_token", "=>{token}" },
                        { "expires_in", "=>{expiresIn}" },
                        { "token_type", "=>{tokenType}" }
                    }
                }
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            Warmups = warmupRequests,
            Requests = [],
            Variables = new Dictionary<string, string>()
        };

        const string ResponseJson = """
                                    {
                                        "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                                        "expires_in": 3600,
                                        "token_type": "Bearer"
                                    }
                                    """;

        _httpMessageHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
        });

        var processor = CreateProcessor();
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        // Check that the request was sent correctly
        var capturedRequest = _httpMessageHandler.LastRequest;
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().ShouldBe("https://example.com/api/auth/token");

        // Verify Content-Type header
        capturedRequest.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-www-form-urlencoded");

        // Verify form data
        _httpMessageHandler.RequestContents.Last().ShouldBe("grant_type=client_credentials&client_id=test_client&client_secret=test_secret");

        // Check that the response variables were extracted correctly
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(3);
        successResult.ExecutionPlan.Variables.ShouldContainKey("token");
        successResult.ExecutionPlan.Variables["token"].ShouldBe("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...");
        successResult.ExecutionPlan.Variables.ShouldContainKey("expiresIn");
        successResult.ExecutionPlan.Variables["expiresIn"].ShouldBe("3600");
        successResult.ExecutionPlan.Variables.ShouldContainKey("tokenType");
        successResult.ExecutionPlan.Variables["tokenType"].ShouldBe("Bearer");
    }

    private WarmupProcessor CreateProcessor(
        ILogger<WarmupProcessor>? logger = null,
        IHttpRequestBuilder? requestBuilder = null,
        IVariableHandler? variableHandler = null) =>
        new(
            logger ?? Substitute.For<ILogger<WarmupProcessor>>(),
            requestBuilder ?? _requestBuilder,
            variableHandler ?? _variableHandler);
}