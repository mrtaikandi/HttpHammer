namespace HttpHammer.Processors;

public interface IProcessor
{
    int Order { get; }

    ValueTask<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default);
}