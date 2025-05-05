using HttpHammer.Plan.Definitions;

namespace HttpHammer.Plan;

public interface IExecutionPlanLoader
{
    ExecutionPlan Load(string filePath);
}