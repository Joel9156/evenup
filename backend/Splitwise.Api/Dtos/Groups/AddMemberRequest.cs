using System.ComponentModel.DataAnnotations;

namespace Splitwise.Api.Dtos.Groups;

public class AddMemberRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
