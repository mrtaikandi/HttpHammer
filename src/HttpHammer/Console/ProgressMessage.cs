namespace HttpHammer.Console;

public readonly record struct ProgressMessage(MessageType Type, string RequestName, string Text, DateTime Timestamp)
{
    public ProgressMessage(MessageType type, string requestName, string text)
        : this(type, requestName, text, DateTime.Now) { }
}

public enum MessageType
{
    Information,
    Warning,
    Error
}