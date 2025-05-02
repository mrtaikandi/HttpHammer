namespace HttpHammer.Configuration;

public class WarmupDefinition : BaseRequestDefinition
{
    public WarmupDefinition()
    {
        MaxRequests = 1;
    }
}