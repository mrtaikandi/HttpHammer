using HttpHammer.Plan.Definitions;
using Spectre.Console;

namespace HttpHammer.Processors;

public class PromptProcessor : IProcessor
{
    private readonly IAnsiConsole _console;

    public PromptProcessor(IAnsiConsole console)
    {
        _console = console;
    }

    public bool Interactive => true;

    public async Task<ProcessorResult> ExecuteAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        if (context.Definition is not PromptDefinition definition)
        {
            throw new ProcessorException($"{nameof(PromptProcessor)} can only process {nameof(PromptDefinition)}");
        }

        var appendColon = !string.IsNullOrEmpty(definition.Message) && !":?!.".Contains(definition.Message[^1]);
        var prompt = new TextPrompt<string>(appendColon ? $"{definition.Message}:" : definition.Message)
        {
            AllowEmpty = definition.AllowEmpty,
            ShowDefaultValue = true,
            IsSecret = definition.Secret,
            PromptStyle = new Style(foreground: Color.DeepSkyBlue1),
        };

        if (!string.IsNullOrWhiteSpace(definition.Default))
        {
            prompt.DefaultValue(definition.Default);
        }

        context.Variables[definition.Variable] = await _console.PromptAsync(prompt, cancellationToken);

        return ProcessorResult.Success();
    }
}