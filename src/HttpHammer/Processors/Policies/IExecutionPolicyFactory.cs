using HttpHammer.Configuration;

namespace HttpHammer.Processors.Policies;

public interface IExecutionPolicyFactory
{
    IExecutionPolicy Create(RequestDefinition request);
}