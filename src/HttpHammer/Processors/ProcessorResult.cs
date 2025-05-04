using HttpHammer.Configuration;

namespace HttpHammer.Processors;

public abstract record ProcessorResult
{
    public static ErrorProcessorResult Fail(params string[] errors) => new(errors);

    public static SuccessProcessorResult Success(ExecutionPlan result, string[]? warnings = null) => new(result, warnings ?? []);

    public bool IsSuccess => this is SuccessProcessorResult;

    public bool HasErrors => this is ErrorProcessorResult;
}

public record ProcessorResult<T>(T Result) : ProcessorResult;

public sealed record ErrorProcessorResult(string[] Errors) : ProcessorResult
{
    public override string ToString() => $"Error: {string.Join(", ", Errors)}";
}

public sealed record SuccessProcessorResult(ExecutionPlan ExecutionPlan, string[] Warnings) : ProcessorResult;