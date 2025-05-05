using HttpHammer.Console;
using HttpHammer.Plan.Definitions;

namespace HttpHammer.Processors;

public sealed class ProcessorContext
{
    public ProcessorContext(Definition definition, Dictionary<string, string> variables, IProgressContext progress)
    {
        Definition = definition;
        Progress = progress;
        Variables = variables;
    }

    public Dictionary<string, string> Variables { get; }

    public Definition Definition { get; }

    public IProgressContext Progress { get; }
}