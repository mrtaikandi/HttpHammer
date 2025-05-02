using HttpHammer.Configuration;
using HttpHammer.Console;
using HttpHammer.Processors;
using Microsoft.Extensions.Logging;

namespace HttpHammer.Tests.Processors;

public class ExecutionPlanFileProcessorTests
{
    private readonly ILogger<ExecutionPlanFileProcessor> _logger = Substitute.For<ILogger<ExecutionPlanFileProcessor>>();
    private readonly string _testDataPath = Path.GetFullPath("TestData");

    [Fact]
    public async Task ExecuteAsync_WhenFileExists_LoadsExecutionPlan()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Requests.Count.ShouldBe(1);
        successResult.ExecutionPlan.Requests.ShouldContainKey("test-request");
        successResult.ExecutionPlan.Requests["test-request"].Name.ShouldBe("Test Request");
        successResult.ExecutionPlan.Requests["test-request"].Url.ShouldBe("https://example.com/api");
        successResult.ExecutionPlan.Requests["test-request"].Method.ShouldBe("GET");
        successResult.ExecutionPlan.Variables.ShouldContainKey("timestamp");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileDoesNotExist_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "non-existent-file.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Failed to load execution plan from file.");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidYaml_ReturnsError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Failed to load execution plan from file.");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRequestUrl_ReturnsValidationError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty-url-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Request 'Test Request' URL is missing or empty.");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRequestUrl_ReturnsValidationError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "missing-url-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Request 'Test Request' URL is missing or empty.");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRequestName_SetsNameFromKey()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "empty-name-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Requests["test-request"].Name.ShouldBe("test-request");
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeMaxRequests_SetsToZero()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "negative-max-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Requests["test-request"].MaxRequests.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithWarmupAndRegularRequests_ValidatesBoth()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "both-requests-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.WarmupRequests.Count.ShouldBe(1);
        successResult.ExecutionPlan.Requests.Count.ShouldBe(1);
        successResult.ExecutionPlan.WarmupRequests.ShouldContainKey("warmup-request");
        successResult.ExecutionPlan.Requests.ShouldContainKey("test-request");
    }

    [Fact]
    public async Task ExecuteAsync_WithWarmupRequestMissingUrl_ReturnsValidationError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-warmup-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();
        result.ShouldBeOfType<ErrorProcessorResult>();
        var errorResult = (ErrorProcessorResult)result;
        errorResult.Errors.Length.ShouldBe(1);
        errorResult.Errors[0].ShouldBe("Request 'Warmup Request' URL is missing or empty.");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPlan_AddsDefaultVariables()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "variables-plan.yaml");
        var processor = new ExecutionPlanFileProcessor(_logger);
        var progressContext = Substitute.For<IProgressContext>();
        var context = new ProcessorContext(new ExecutionPlan { FilePath = filePath }, progressContext);

        // Act
        var result = await processor.ExecuteAsync(context);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ShouldBeOfType<SuccessProcessorResult>();
        var successResult = (SuccessProcessorResult)result;
        successResult.ExecutionPlan.Variables.Count.ShouldBe(2); // custom_var + timestamp
        successResult.ExecutionPlan.Variables.ShouldContainKey("custom_var");
        successResult.ExecutionPlan.Variables.ShouldContainKey("timestamp");
        successResult.ExecutionPlan.Variables["custom_var"].ShouldBe("custom_value");
        long.TryParse(successResult.ExecutionPlan.Variables["timestamp"], out _).ShouldBeTrue();
    }

    [Fact]
    public void Order_ReturnsExpectedValue()
    {
        // Arrange
        var processor = new ExecutionPlanFileProcessor(_logger);

        // Act & Assert
        processor.Order.ShouldBe(0);
    }
}