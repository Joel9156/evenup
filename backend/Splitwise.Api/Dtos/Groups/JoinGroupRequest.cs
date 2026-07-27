using System.ComponentModel.DataAnnotations;

namespace Splitwise.Api.Dtos.Groups;

public class JoinGroupRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    // Set when a signed-in visitor recognizes themselves in the invite preview's
    // ClaimableMembers list — links that existing guest placeholder to their account
    // instead of creating a brand-new (duplicate) member. Ignored for guest joins: an
    // unauthenticated visitor "claims" a placeholder purely client-side, since there's no
    // account to link either way.
    public Guid? ExistingMemberId { get; set; }
}
