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
    /// <summary>Language the gift was bought in ("ka" or "en"); its certificate and share card use it.</summary>
    public string Lang { get; set; } = "ka";
    /// <summary>What the current president's day cost; a coup costs 1.5x this.</summary>
    public int Price { get; set; }
    /// <summary>How many times the presidency was taken from someone. Also guards against two coups at once.</summary>
    public int Coups { get; set; }
    /// <summary>Everything ever paid for this country-day, by every president.</summary>
    public int TotalPaid { get; set; }
    public string PaymentRef { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the whole cabinet was bought at once; remaining posts are then filled for free.</summary>
    public bool CabinetPrepaid { get; set; }
    public int CabinetPaid { get; set; }

    public string Number => $"{Country}-{Date:yyyyMMdd}";
}

/// <summary>A prime minister or minister in the cabinet of one gifted country-day.</summary>
public class Appointment
{
    public int Id { get; set; }
    public string GiftId { get; set; } = "";
    public string Post { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>What the current holder paid (0 when the seat came from a prepaid cabinet).</summary>
    public int Price { get; set; }
    /// <summary>What the seat is valued at; the next coup costs 1.5x this.</summary>
    public int Worth { get; set; }
    /// <summary>How many times the seat was taken from someone. Also guards against two coups at once.</summary>
    public int Coups { get; set; }
    /// <summary>Everything ever paid for this seat, by every holder.</summary>
    public int TotalPaid { get; set; }
    public string PaymentRef { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Gift> Gifts => Set<Gift>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

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
        g.Property(x => x.Coups).IsConcurrencyToken();

        var a = b.Entity<Appointment>();
        a.HasIndex(x => new { x.GiftId, x.Post }).IsUnique();   // one person per post
        a.HasOne<Gift>().WithMany().HasForeignKey(x => x.GiftId);
        a.Property(x => x.Name).HasMaxLength(Limits.Name);
        a.Property(x => x.Coups).IsConcurrencyToken();
    }
}

public static class Limits
{
    public const int Name = 40;
    public const int Message = 140;
}
