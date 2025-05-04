using YamlDotNet.Serialization;

namespace HttpHammer.Configuration;

[YamlStaticContext]
[YamlSerializable(typeof(Definition))]
[YamlSerializable(typeof(ExecutionPlan))]
[YamlSerializable(typeof(RequestDefinition))]
[YamlSerializable(typeof(ResponseDefinition))]
public partial class ExecutionPlanYamlStaticContext;