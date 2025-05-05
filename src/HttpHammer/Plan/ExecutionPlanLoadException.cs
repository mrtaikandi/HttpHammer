namespace HttpHammer.Plan;

public class ExecutionPlanLoadException : Exception
{
    public ExecutionPlanLoadException() { }

    public ExecutionPlanLoadException(string message) : base(message) { }

    public ExecutionPlanLoadException(string message, Exception inner) : base(message, inner) { }
}