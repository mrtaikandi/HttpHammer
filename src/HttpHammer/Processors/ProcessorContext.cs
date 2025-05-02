using HttpHammer.Configuration;
using HttpHammer.Console;

namespace HttpHammer.Processors;

public sealed class ProcessorContext
{
    public ProcessorContext(ExecutionPlan executionPlan, IProgressContext progress)
    {
        ExecutionPlan = executionPlan;
        Progress = progress;
    }

    public ExecutionPlan ExecutionPlan { get; }

    public IProgressContext Progress { get; }
}