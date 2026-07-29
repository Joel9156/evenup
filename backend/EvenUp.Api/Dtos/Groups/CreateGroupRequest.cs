using System.ComponentModel.DataAnnotations;

namespace EvenUp.Api.Dtos.Groups;

public class CreateGroupRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
