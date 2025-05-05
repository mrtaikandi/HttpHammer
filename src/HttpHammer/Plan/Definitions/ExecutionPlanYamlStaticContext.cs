using YamlDotNet.Serialization;

namespace HttpHammer.Plan.Definitions;

[YamlStaticContext]
[YamlSerializable(typeof(Definition))]
[YamlSerializable(typeof(ExecutionPlan))]
[YamlSerializable(typeof(RequestDefinition))]
[YamlSerializable(typeof(ResponseDefinition))]
[YamlSerializable(typeof(DelayDefinition))]
[YamlSerializable(typeof(PromptDefinition))]
[YamlSerializable(typeof(WarmupDefinition))]
public partial class ExecutionPlanYamlStaticContext;