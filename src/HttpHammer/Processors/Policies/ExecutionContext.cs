using HttpHammer.Configuration;
using HttpHammer.Console;

namespace HttpHammer.Processors.Policies;

internal sealed record ExecutionContext(RequestDefinition Request, IDictionary<string, string> Variables, IProgress? Progress);