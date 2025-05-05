using HttpHammer.Plan;
using HttpHammer.Plan.Definitions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HttpHammer.Tests.Plan;

public class ExecutionPlanLoaderTests
{
    private readonly ILogger<ExecutionPlanLoader> _logger;
    private readonly ExecutionPlanLoader _sut;
    private readonly string _testDataPath;

    public ExecutionPlanLoaderTests()
    {
        _logger = NullLogger<ExecutionPlanLoader>.Instance;
        _sut = new ExecutionPlanLoader(_logger);
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    [Fact]
    public void Load_ValidFile_ReturnsExecutionPlan()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.ShouldNotBeNull();
        result.RequestDefinitions.ShouldNotBeNull();
        result.RequestDefinitions.Length.ShouldBe(1);
        result.RequestDefinitions[0].Name.ShouldBe("Test Request");
        result.RequestDefinitions[0].Url.ShouldBe("https://example.com/api");
        result.RequestDefinitions[0].Method.ShouldBe("GET");
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsEmptyPlan()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "non-existent-file.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.ShouldNotBeNull();
        result.FilePath.ShouldBe(Path.GetFullPath(filePath));
        result.RequestDefinitions.ShouldBeEmpty();
    }

    [Fact]
    public void Load_InvalidYaml_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldBe("Failed to load execution plan from file.");
    }

    [Fact]
    public void Load_MissingName_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing-name-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldContain("name is missing or empty");
    }

    [Fact]
    public void Load_EmptyName_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty-name-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldContain("name is missing or empty");
    }

    [Fact]
    public void Load_MissingUrl_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing-url-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldContain("URL is missing or empty");
    }

    [Fact]
    public void Load_EmptyUrl_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty-url-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldContain("URL is missing or empty");
    }

    [Fact]
    public void Load_MissingWarmupRequestUrl_ThrowsException()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing-warmup-requestUrl-plan.yaml");

        // Act & Assert
        var exception = Should.Throw<ExecutionPlanLoadException>(() => _sut.Load(filePath));
        exception.Message.ShouldContain("URL is missing or empty");
    }

    [Fact]
    public void Load_NormalizesMethod_ToUpperCase()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
                method: get
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].Method.ShouldBe("GET");
    }

    [Fact]
    public void Load_NegativeMaxRequests_SetsToZero()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "negative-max-plan.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].MaxRequests.ShouldBe(0);
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData(200, 200)]
    public void Load_SetsDefaultMaxRequests_WhenNotSpecified(int? maxRequests, int expected)
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");
        var content = """
                      requests:
                        - name: Test Request
                          url: https://example.com/api
                          method: GET
                      """;

        if (maxRequests.HasValue)
        {
            content += $"\n    max_requests: {maxRequests}";
        }

        File.WriteAllText(filePath, content);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].MaxRequests.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(-5, 1)]
    [InlineData(20, 20)]
    public void Load_SetsDefaultConcurrentConnections_WhenNotSpecifiedOrNegative(int? concurrentConnections, int expected)
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");
        var content = """
                      requests:
                        - name: Test Request
                          url: https://example.com/api
                          method: GET
                      """;

        if (concurrentConnections.HasValue)
        {
            content += $"\n    concurrent_connections: {concurrentConnections}";
        }

        File.WriteAllText(filePath, content);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].ConcurrentConnections.ShouldBe(expected);
    }

    [Fact]
    public void Load_WithVariables_AddsTimestampVariable()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "variables-plan.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.Variables.ShouldContainKey("timestamp");
        long.TryParse(result.Variables["timestamp"], out var timestamp).ShouldBeTrue();

        // Timestamp should be recent
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Math.Abs(now - timestamp).ShouldBeLessThan(10); // Within 10 seconds
    }

    [Fact]
    public void Load_WithBothRequests_ProcessesWarmupAndRegularRequests()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "both-requests-plan.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions.Length.ShouldBeGreaterThan(0);
        result.WarmupDefinitions.Length.ShouldBeGreaterThan(0);

        // Verify warmup requests are normalized with different defaults
        var warmupRequest = result.WarmupDefinitions.OfType<RequestDefinition>().First();
        warmupRequest.MaxRequests.ShouldBe(1); // Default for warmup
        warmupRequest.ConcurrentConnections.ShouldBe(1); // Default for warmup

        // Verify regular requests are normalized
        var regularRequest = result.RequestDefinitions.First();
        regularRequest.MaxRequests.ShouldBe(100); // Default for regular requests
        regularRequest.ConcurrentConnections.ShouldBe(10); // Default for regular requests
    }

    [Fact]
    public void Load_FilePath_SetsFullPath()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.FilePath.ShouldBe(Path.GetFullPath(filePath));
    }

    [Fact]
    public void Load_LogsYamlContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");

        // Act
        var plan = _sut.Load(filePath);

        // Assert
        plan.RequestDefinitions.Length.ShouldBe(1);
        plan.RequestDefinitions[0].Name.ShouldBe("Test Request");
        plan.RequestDefinitions[0].Url.ShouldBe("https://example.com/api");
        plan.RequestDefinitions[0].Method.ShouldBe("GET");
    }

    [Fact]
    public void Load_WithHeaders_ParsesHeadersCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "headers-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
                method: GET
                headers:
                  Content-Type: application/json
                  Authorization: Bearer token123
                  Custom-Header: CustomValue
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        var request = result.RequestDefinitions[0];
        request.Headers.ShouldNotBeNull();
        request.Headers.Count.ShouldBe(3);
        request.Headers["Content-Type"].ShouldBe("application/json");
        request.Headers["Authorization"].ShouldBe("Bearer token123");
        request.Headers["Custom-Header"].ShouldBe("CustomValue");
    }

    [Fact]
    public void Load_DefaultMethod_IsGet()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "default-method-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].Method.ShouldBe("GET");
    }

    [Fact]
    public void Load_WithBodyContent_ParsesBodyCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "body-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
                method: POST
                body: '{"key":"value","number":123}'
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        var request = result.RequestDefinitions[0];
        request.Body.ShouldNotBeNull();
        request.Body.ShouldBe("{\"key\":\"value\",\"number\":123}");
    }

    [Fact]
    public void Load_MultipleRequestDefinitions_LoadsAllRequests()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "multiple-requests-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: First Request
                url: https://example.com/api/first
                method: GET
              - name: Second Request
                url: https://example.com/api/second
                method: POST
                body: '{"key":"value"}'
              - name: Third Request
                url: https://example.com/api/third
                method: PUT
                headers:
                  Content-Type: application/json
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions.Length.ShouldBe(3);

        result.RequestDefinitions[0].Name.ShouldBe("First Request");
        result.RequestDefinitions[0].Url.ShouldBe("https://example.com/api/first");
        result.RequestDefinitions[0].Method.ShouldBe("GET");

        result.RequestDefinitions[1].Name.ShouldBe("Second Request");
        result.RequestDefinitions[1].Url.ShouldBe("https://example.com/api/second");
        result.RequestDefinitions[1].Method.ShouldBe("POST");
        result.RequestDefinitions[1].Body.ShouldBe("{\"key\":\"value\"}");

        result.RequestDefinitions[2].Name.ShouldBe("Third Request");
        result.RequestDefinitions[2].Url.ShouldBe("https://example.com/api/third");
        result.RequestDefinitions[2].Method.ShouldBe("PUT");
        result.RequestDefinitions[2].Headers.ShouldContainKey("Content-Type");
        result.RequestDefinitions[2].Headers["Content-Type"].ShouldBe("application/json");
    }

    [Fact]
    public void Load_ResponseDefinition_ParsesResponseCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "response-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
                method: GET
                response:
                  status_code: 201
                  headers:
                    Content-Type: application/json
                  content:
                    success: true
                    message: Created successfully
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        var request = result.RequestDefinitions[0];
        request.Response.ShouldNotBeNull();
        request.Response.StatusCode.ShouldBe(201);
        request.Response.Headers.ShouldNotBeNull();
        request.Response.Headers["Content-Type"].ShouldBe("application/json");
        request.Response.Content.ShouldNotBeNull();
        request.Response.Content["success"].ShouldBe("true");
        request.Response.Content["message"].ShouldBe("Created successfully");
    }

    [Fact]
    public void Load_VariablesInUrl_SetsTimestampVariable()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "url-variables-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request with Variable
                url: https://example.com/api?timestamp=${timestamp}
                method: GET
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.Variables.ShouldContainKey("timestamp");
        result.RequestDefinitions[0].Url.ShouldBe("https://example.com/api?timestamp=${timestamp}");

        // The variable should be a valid timestamp
        long.TryParse(result.Variables["timestamp"], out var timestamp).ShouldBeTrue();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Math.Abs(now - timestamp).ShouldBeLessThan(10); // Within 10 seconds
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Load_HttpMethod_SupportsDifferentMethods(string method)
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "http-method-plan.yaml");
        File.WriteAllText(
            filePath,
            $"""
             requests:
               - name: Test Request
                 url: https://example.com/api
                 method: {method.ToLowerInvariant()}
             """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.RequestDefinitions[0].Method.ShouldBe(method.ToUpperInvariant());
    }

    [Fact]
    public void Load_CustomVariables_CanBeUsedInPlan()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "custom-variables-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            variables:
              customVar: custom-value
              api_version: v2
            requests:
              - name: Test Request with Custom Variables
                url: https://example.com/api/${api_version}/resource
                method: GET
                headers:
                  X-Custom-Header: ${customVar}
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        result.Variables.ShouldContainKey("customVar");
        result.Variables["customVar"].ShouldBe("custom-value");
        result.Variables.ShouldContainKey("api_version");
        result.Variables["api_version"].ShouldBe("v2");

        // Built-in timestamp should still be there
        result.Variables.ShouldContainKey("timestamp");
    }

    [Fact]
    public void Load_WithDescription_ParsesDescriptionCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "description-plan.yaml");
        File.WriteAllText(
            filePath,
            """
            requests:
              - name: Test Request
                url: https://example.com/api
                method: GET
                description: This is a test request with a detailed description that explains its purpose
            """);

        // Act
        var result = _sut.Load(filePath);

        // Assert
        var request = result.RequestDefinitions[0];
        request.Description.ShouldNotBeNull();
        request.Description.ShouldBe("This is a test request with a detailed description that explains its purpose");
    }
}