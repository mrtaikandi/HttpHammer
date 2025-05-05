using HttpHammer.Plan;
using HttpHammer.Plan.Definitions;

namespace HttpHammer.Processors;

public class DelayProcessor : IProcessor
{
    public async Task<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        if (context.Definition is not DelayDefinition definition)
        {
            throw new ProcessorException($"{nameof(DelayProcessor)} can only process {nameof(DelayDefinition)}");
        }

        if (definition.Duration <= 0)
        {
            return ProcessorResult.Success();
        }

        var progress = context.Progress.Create(definition.Name, definition.Duration);
        progress.IsIndeterminate = true;

        try
        {
            await Task.Delay(definition.Duration, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            progress.IsIndeterminate = false;
            progress.Complete(definition.Duration);
        }

        return ProcessorResult.Success();
    }
}