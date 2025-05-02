using Spectre.Console;

namespace HttpHammer.Console.Renderers;

public static class Shell
{
    public static async Task<string> PromptForStringAsync(
        this IAnsiConsole console,
        string promptText,
        string? defaultValue = null,
        Func<string, ValidationResult>? validator = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promptText, nameof(promptText));
        var prompt = new TextPrompt<string>(promptText);

        if (defaultValue is not null)
        {
            prompt.DefaultValue(defaultValue);
            prompt.ShowDefaultValue();
        }

        if (validator is not null)
        {
            prompt.Validate(validator);
        }

        return await console.PromptAsync(prompt, cancellationToken);
    }

    public static void DisplayError(this IAnsiConsole console, string errorMessage) =>
        console.DisplayMessage("thumbs_down", $"[red bold]{errorMessage}[/]");

    public static void DisplayErrors(this IAnsiConsole console, params IEnumerable<string> errorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorMessage, nameof(errorMessage));
        foreach (var error in errorMessage)
        {
            console.DisplayError(error);
        }
    }

    public static void DisplayMessage(this IAnsiConsole console, string emoji, string message) =>
        console.MarkupLine($":{emoji}:  {message}");
}