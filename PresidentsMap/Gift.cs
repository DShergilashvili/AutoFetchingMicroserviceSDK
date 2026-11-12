using Microsoft.EntityFrameworkCore;

namespace PresidentsMap;

/// <summary>One country gifted to one person for one day.</summary>
public class Gift
{
    public string Id { get; set; } = "";
    public string Country { get; set; } = "";
    public DateOnly Date { get; set; }
    public string To { get; set; } = "";
    public string From { get; set; } = "";
    public string Message { get; set; } = "";
    public int Price { get; set; }
    public string PaymentRef { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public string Number => $"{Country}-{Date:yyyyMMdd}";
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Gift> Gifts => Set<Gift>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var g = b.Entity<Gift>();
        g.HasKey(x => x.Id);
        // The database itself guarantees one owner per country per day.
        g.HasIndex(x => new { x.Country, x.Date }).IsUnique();
        g.HasIndex(x => x.CreatedAt);
        g.Property(x => x.Country).HasMaxLength(2);
        g.Property(x => x.To).HasMaxLength(Limits.Name);
        g.Property(x => x.From).HasMaxLength(Limits.Name);
        g.Property(x => x.Message).HasMaxLength(Limits.Message);
    }
}

public static class Limits
{
    public const int Name = 40;
    public const int Message = 140;
}
