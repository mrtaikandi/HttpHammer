namespace HttpHammer.Processors;

public interface IProcessor
{
    bool Interactive => false;

    Task<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default);
}