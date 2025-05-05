using System.Diagnostics.CodeAnalysis;

namespace HttpHammer.Processors;

public interface IVariableHandler
{
    string Substitute(string input, IDictionary<string, string> variables);

    bool TryGetAssignmentVariableName(string input, [NotNullWhen(true)] out string? variableName);
}