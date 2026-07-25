namespace Splitwise.Api.Models;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Short random code used in invite links, e.g. https://.../join/{InviteCode}
    public string InviteCode { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
