using HttpHammer.Plan.Definitions;

namespace HttpHammer.Processors;

public interface IProcessorFactory
{
    IProcessor Create(Definition definition);

    IProcessor Create<TDefinition>() where TDefinition : Definition;
}