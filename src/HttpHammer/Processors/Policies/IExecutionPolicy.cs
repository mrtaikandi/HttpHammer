namespace HttpHammer.Processors.Policies;

public interface IExecutionPolicy
{
    internal Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);
}