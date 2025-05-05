namespace HttpHammer.Processors;

public interface IProcessor
{
    Task<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default);
}