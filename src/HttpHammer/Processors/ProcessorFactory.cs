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
        RequestDefinition => _serviceProvider.GetRequiredService<RequestProcessor>(),
        DelayDefinition => _serviceProvider.GetRequiredService<DelayProcessor>(),
        PromptDefinition => _serviceProvider.GetRequiredService<PromptProcessor>(),

        _ => throw new NotSupportedException($"Unsupported definition type: {definition.GetType().Name}")
    };
}