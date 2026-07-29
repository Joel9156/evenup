using Microsoft.EntityFrameworkCore;
using EvenUp.Api.Models;

namespace EvenUp.Api.Data;

public class EvenUpDbContext(DbContextOptions<EvenUpDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseShare> ExpenseShares => Set<ExpenseShare>();
    public DbSet<Settlement> Settlements => Set<Settlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasIndex(g => g.InviteCode).IsUnique();

            entity.HasOne(g => g.CreatedByUser)
                  .WithMany(u => u.CreatedGroups)
                  .HasForeignKey(g => g.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasOne(m => m.Group)
                  .WithMany(g => g.Members)
                  .HasForeignKey(m => m.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                  .WithMany(u => u.Memberships)
                  .HasForeignKey(m => m.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Expenses)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PaidByMember)
                  .WithMany()
                  .HasForeignKey(e => e.PaidByMemberId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByMember)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByMemberId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseShare>(entity =>
        {
            entity.Property(s => s.ShareAmount).HasPrecision(10, 2);

            entity.HasOne(s => s.Expense)
                  .WithMany(e => e.Shares)
                  .HasForeignKey(s => s.ExpenseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Member)
                  .WithMany()
                  .HasForeignKey(s => s.MemberId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.HasOne(s => s.Group)
                  .WithMany(g => g.Settlements)
                  .HasForeignKey(s => s.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
