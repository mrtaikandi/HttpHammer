using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace HttpHammer.Plan.Definitions;

public class Definition
{
    [Required]
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = null!;

    [YamlIgnore]
    internal Guid Id { get; } = Guid.CreateVersion7();
}