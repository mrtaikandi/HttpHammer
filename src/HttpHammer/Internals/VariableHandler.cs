using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace HttpHammer.Internals;

internal sealed partial class VariableHandler : IVariableHandler
{
    [GeneratedRegex(@"^=>\{([^}]+)\}$", RegexOptions.Compiled)]
    private static partial Regex VariableAssignmentPattern { get; }

    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern { get; }

    /// <inheritdoc />
    public string Substitute(string input, IDictionary<string, string> variables)
    {
        return VariablePattern.Replace(input, match =>
        {
            var variableName = match.Groups[1].Value;
            return variables.TryGetValue(variableName, out var value) ? value : match.Value;
        });
    }

    /// <inheritdoc />
    public bool TryGetAssignmentVariableName(string input, [NotNullWhen(true)] out string? variableName)
    {
        variableName = null;

        var match = VariableAssignmentPattern.Match(input);
        if (!match.Success)
        {
            return false;
        }

        variableName = match.Groups[1].Value;
        return true;
    }
}