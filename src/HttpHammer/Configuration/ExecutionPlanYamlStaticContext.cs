using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

[YamlStaticContext]
[YamlSerializable(typeof(ExecutionPlan))]
[YamlSerializable(typeof(WarmupDefinition))]
[YamlSerializable(typeof(RequestDefinition))]
[YamlSerializable(typeof(ResponseDefinition))]
[YamlSerializable(typeof(BaseRequestDefinition))]
public partial class ExecutionPlanYamlStaticContext;