using HttpHammer.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpHammer.Processors.Policies;

internal class ExecutionPolicyFactory : IExecutionPolicyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ExecutionPolicyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IExecutionPolicy Create(RequestDefinition request) => request.ConcurrentConnections <= 1
        ? _serviceProvider.GetRequiredService<SynchronousExecutionPolicy>()
        : _serviceProvider.GetRequiredService<ConcurrentExecutionPolicy>();
}