using System.Text.Json;

namespace PresidentsMap;

public record Country(string Id, string Name, int BasePrice);

/// <summary>Countries and base prices, read once from the same local file the map uses.</summary>
public class CountryCatalog
{
    private readonly Dictionary<string, Country> _byId;

    public CountryCatalog(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.WebRootPath, "data", "countries.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        _byId = doc.RootElement.EnumerateArray()
            .Select(e => new Country(e.GetProperty("id").GetString()!, e.GetProperty("n").GetString()!, e.GetProperty("p").GetInt32()))
            .ToDictionary(c => c.Id);
    }

    public Country? Find(string? id) => id is not null && _byId.TryGetValue(id, out var c) ? c : null;
}

public static class Pricing
{
    private static readonly Dictionary<(int Month, int Day), string> Special = new()
    {
        [(2, 14)] = "ვალენტინობა",
        [(3, 8)] = "8 მარტი",
        [(12, 31)] = "ახალი წლის ღამე",
        [(1, 1)] = "ახალი წელი",
    };

    public static string? SpecialDay(DateOnly d) => Special.GetValueOrDefault((d.Month, d.Day));
    public static int Multiplier(DateOnly d) => SpecialDay(d) is null ? 1 : 2;
    public static int Price(Country c, DateOnly d) => c.BasePrice * Multiplier(d);
}

public record CabinetPost(string Key, string Title, int Price);

public static class Cabinet
{
    public static readonly IReadOnlyList<CabinetPost> Posts =
    [
        new("pm", "პრემიერ-მინისტრი", 3),
        new("foreign", "საგარეო საქმეთა მინისტრი", 1),
        new("finance", "ფინანსთა მინისტრი", 1),
        new("defense", "თავდაცვის მინისტრი", 1),
        new("culture", "კულტურის მინისტრი", 1),
        new("love", "სიყვარულის მინისტრი", 1),
        new("sweets", "ტკბილეულის მინისტრი", 1),
        new("rest", "დასვენების მინისტრი", 1),
        new("fun", "გართობის მინისტრი", 1),
    ];

    public static CabinetPost? Find(string? key) => Posts.FirstOrDefault(p => p.Key == key);

    /// <summary>Taking an occupied seat costs 50% more than it is worth, and always at least $1 more.</summary>
    public static int CoupPrice(int worth) => Math.Max(worth + 1, (int)Math.Ceiling(worth * 1.5));

    /// <summary>All still-empty posts at once cost 30% less than one by one.</summary>
    public static int BundlePrice(IEnumerable<CabinetPost> empty) => (int)Math.Ceiling(empty.Sum(p => p.Price) * 0.7);
}

public static class Ka
{
    private static readonly string[] Months =
    [
        "იანვარი", "თებერვალი", "მარტი", "აპრილი", "მაისი", "ივნისი",
        "ივლისი", "აგვისტო", "სექტემბერი", "ოქტომბერი", "ნოემბერი", "დეკემბერი",
    ];

    public static string Date(DateOnly d) => $"{d.Day} {Months[d.Month - 1]}, {d.Year}";
}
