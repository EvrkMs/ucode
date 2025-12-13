using Microsoft.EntityFrameworkCore;
using Ucode.Backend.Entities;

namespace Ucode.Backend.Data;

public class UcodeDbContext(DbContextOptions<UcodeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Code> Codes => Set<Code>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.TelegramId);
            entity.Property(u => u.TelegramId).ValueGeneratedNever();
            entity.Property(u => u.Username).HasMaxLength(64);
            entity.Property(u => u.FirstName).HasMaxLength(128);
            entity.Property(u => u.LastName).HasMaxLength(128);
            entity.Property(u => u.LanguageCode).HasMaxLength(16);
            entity.Property(u => u.PhotoUrl).HasMaxLength(512);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Code>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Value).HasMaxLength(32).IsRequired();
            entity.HasIndex(c => c.Value).IsUnique();
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(c => c.ExpiresAt).HasDefaultValueSql("now()");
            entity.Property(c => c.Used).IsConcurrencyToken();
        });
    }
}
