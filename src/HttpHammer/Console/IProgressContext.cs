namespace HttpHammer.Console;

public interface IProgressContext
{
    IProgress Create(string description, int maxValue);
}