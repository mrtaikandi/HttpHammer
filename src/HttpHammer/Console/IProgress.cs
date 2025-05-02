namespace HttpHammer.Console;

public interface IProgress
{
    void MaxValue(int maxValue);

    void Increment();

    void Complete();
}