namespace HttpHammer.Processors;

public class ProcessorException : Exception
{
    public ProcessorException() { }

    public ProcessorException(string message) : base(message) { }

    public ProcessorException(string message, Exception inner) : base(message, inner) { }
}