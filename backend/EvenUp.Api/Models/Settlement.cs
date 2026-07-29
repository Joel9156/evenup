namespace EvenUp.Api.Models;

public class Settlement
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public DateTime GeneratedAt { get; set; }

    // JSON snapshot of the transfer plan (who pays whom how much) at settlement time.
    public string SnapshotJson { get; set; } = string.Empty;
}
