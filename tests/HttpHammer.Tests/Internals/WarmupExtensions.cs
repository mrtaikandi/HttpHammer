using HttpHammer.Configuration;
using HttpHammer.Plan.Definitions;

namespace HttpHammer.Tests.Internals;

internal static class WarmupContainerExtensions
{
    internal static WarmupDefinition[] WarmupDefinitions(this IEnumerable<Definition> definitions) => definitions
        .Select(definition => definition switch
        {
            RequestDefinition r => new WarmupDefinition { Request = r },
            DelayDefinition d => new WarmupDefinition { Delay = d },
            PromptDefinition p => new WarmupDefinition { Prompt = p },
            _ => throw new InvalidOperationException($"Unknown definition type: {definition.GetType().Name}")
        })
        .ToArray();
}