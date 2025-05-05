using HttpHammer.Plan.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace HttpHammer.Processors;

public class ProcessorFactory : IProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IProcessor Create(Definition definition) => definition switch
    {
        RequestDefinition request => _serviceProvider.GetRequiredService<RequestProcessor>(),

        // DelayDefinition delay => _serviceProvider.GetRequiredService<IDelayProcessor>(),
        // PromptDefinition prompt => _serviceProvider.GetRequiredService<IPromptProcessor>(),
        _ => throw new NotSupportedException($"Unsupported definition type: {definition.GetType().Name}")
    };

    public IProcessor Create<TDefinition>() where TDefinition : Definition
    {
        var definitionType = typeof(TDefinition);
        if (definitionType == typeof(RequestDefinition))
        {
            return _serviceProvider.GetRequiredService<RequestProcessor>();
        }

        throw new NotSupportedException($"Unsupported definition type: {definitionType.Name}");
    }
}