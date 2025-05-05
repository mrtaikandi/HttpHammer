using HttpHammer.Configuration;
using HttpHammer.Console;
using HttpHammer.Plan.Definitions;
using HttpHammer.Processors;
using HttpHammer.Processors.Policies;
using Microsoft.Extensions.Logging;
using ExecutionContext = HttpHammer.Processors.Policies.ExecutionContext;

namespace HttpHammer.Tests.Processors;

public class RequestProcessorTests
{
    private readonly ILogger<RequestProcessor> _logger;
    private readonly IExecutionPolicyFactory _executionPolicyFactory;
    private readonly IExecutionPolicy _executionPolicy;

    public RequestProcessorTests()
    {
        _logger = Substitute.For<ILogger<RequestProcessor>>();
        _executionPolicyFactory = Substitute.For<IExecutionPolicyFactory>();
        _executionPolicy = Substitute.For<IExecutionPolicy>();

        _executionPolicyFactory.Create(Arg.Any<RequestDefinition>()).Returns(_executionPolicy);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRequests_ReturnsFail()
    {
        // Arrange
        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = [],
            Variables = new Dictionary<string, string>()
        };

        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("No requests to execute.");
    }

    [Fact]
    public async Task ExecuteAsync_WithRequests_CallsExecutionPolicyForEach()
    {
        // Arrange
        RequestDefinition[] requests =
        [
            new()
            {
                Name = "Request 1",
                MaxRequests = 10,
                Url = "https://example.com/api1"
            },
            new()
            {
                Name = "Request 2",
                MaxRequests = 20,
                Url = "https://example.com/api2"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _executionPolicyFactory.Received(2).Create(Arg.Any<RequestDefinition>());
        await _executionPolicy.Received(2).ExecuteAsync(Arg.Any<ExecutionContext>(), Arg.Any<CancellationToken>());

        // Verify progress creation
        progressContext.Received(1).Create("Request 1", 10);
        progressContext.Received(1).Create("Request 2", 20);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ReturnsWithoutError()
    {
        // Arrange
        RequestDefinition[] requests =
        [
            new()
            {
                Name = "Request 1",
                MaxRequests = 10,
                Url = "https://example.com/api1"
            },
            new()
            {
                Name = "Request 2",
                MaxRequests = 10,
                Url = "https://example.com/api2"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        _executionPolicy
            .ExecuteAsync(Arg.Any<ExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new TaskCanceledException());

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.Warnings.Length.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToExecutionPolicy()
    {
        // Arrange
        RequestDefinition[] requests =
        [
            new()
            {
                Name = "Request 1",
                MaxRequests = 10,
                Url = "https://example.com/api"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        using var cts = new CancellationTokenSource();

        // Act
        await processor.ExecuteAsync(context, cts.Token);

        // Assert
        await _executionPolicy.Received(1).ExecuteAsync(
            Arg.Any<ExecutionContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesVariablesToExecutionContext()
    {
        // Arrange
        RequestDefinition[] requests =
        [
            new()
            {
                Name = "Request 1",
                MaxRequests = 10,
                Url = "https://example.com/api"
            }
        ];

        var variables = new Dictionary<string, string>
        {
            { "var1", "value1" },
            { "var2", "value2" }
        };

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = variables
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        ExecutionContext? capturedContext = null;
        _executionPolicy
            .When(x => x.ExecuteAsync(Arg.Any<ExecutionContext>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => { capturedContext = callInfo.Arg<ExecutionContext>(); });

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        await processor.ExecuteAsync(context);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext.Variables.ShouldBeSameAs(variables);
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeNumberOfRequests_HandlesAllCorrectly()
    {
        // Arrange
        var requests = Enumerable.Range(0, 100)
            .Select(i => new RequestDefinition
            {
                Name = $"Request {i}",
                MaxRequests = 1,
                Url = $"https://example.com/api{i}"
            })
            .ToArray();

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _executionPolicyFactory.Received(100).Create(Arg.Any<RequestDefinition>());
        await _executionPolicy.Received(100).ExecuteAsync(Arg.Any<ExecutionContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroMaxRequests_DoesNotCallExecutionPolicy()
    {
        // Arrange
        RequestDefinition[] requests =
        [
            new()
            {
                Name = "Zero Request",
                MaxRequests = 0, // Zero requests to make
                Url = "https://example.com/api"
            }
        ];

        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progress = Substitute.For<IProgress>();
        var progressContext = Substitute.For<IProgressContext>();
        progressContext.Create(Arg.Any<string>(), Arg.Any<int>()).Returns(progress);

        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _executionPolicyFactory.DidNotReceive().Create(Arg.Any<RequestDefinition>());
        await _executionPolicy.DidNotReceive().ExecuteAsync(Arg.Any<ExecutionContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyExecutionPlan_StillReturnsSuccess()
    {
        // Arrange
        RequestDefinition[] requests = [];
        var plan = new ExecutionPlan
        {
            FilePath = string.Empty,
            RequestDefinitions = requests,
            Variables = new Dictionary<string, string>()
        };

        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(plan, progressContext);
        var processor = new RequestProcessor(_logger);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
    }
}