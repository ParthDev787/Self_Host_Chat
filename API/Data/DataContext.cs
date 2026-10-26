using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<ConnectionRequest> ConnectionRequests { get; set; }
    public DbSet<UserConnection> UserConnections { get; set; }
    public DbSet<ChatMessageEntity> ChatMessages { get; set; }
    public DbSet<UserPublicKey> UserPublicKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.UserName)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Username);

        modelBuilder.Entity<ChatMessageEntity>()
            .HasIndex(m => new { m.Sender, m.Recipient });

        modelBuilder.Entity<ChatMessageEntity>()
            .HasIndex(m => new { m.Recipient, m.IsRead });

        modelBuilder.Entity<ChatMessageEntity>()
            .HasIndex(m => m.Timestamp);

        modelBuilder.Entity<ConnectionRequest>()
            .HasIndex(c => new { c.ToUsername, c.Status });

        modelBuilder.Entity<ConnectionRequest>()
            .HasIndex(c => c.FromUsername);

        modelBuilder.Entity<UserConnection>()
            .HasIndex(u => new { u.User1, u.User2 });

        modelBuilder.Entity<UserPublicKey>()
            .HasIndex(p => p.Username)
            .IsUnique();
    }
}
