namespace HttpHammer.Processors;

public abstract record ProcessorResult
{
    public static ErrorProcessorResult Fail(params string[] errors) => new(errors);

    public static SuccessProcessorResult Success(string[]? warnings = null) => new(warnings ?? []);

    public bool IsSuccess => this is SuccessProcessorResult;

    public bool HasErrors => this is ErrorProcessorResult;
}

public record ProcessorResult<T>(T Result) : ProcessorResult;

public sealed record ErrorProcessorResult(string[] Errors) : ProcessorResult
{
    public override string ToString() => $"Error: {string.Join(", ", Errors)}";
}

public sealed record SuccessProcessorResult(string[] Warnings) : ProcessorResult;