namespace PresidentsMap;

/// <summary>
/// Two languages, gettext style: Georgian text is written in the code and is the key;
/// this table holds its English. Anything missing here simply stays Georgian.
/// </summary>
public static class Loc
{
    public static string Norm(string? lang) => lang == "en" ? "en" : "ka";

    /// <summary>Language of an API call: the page tells us which one it is showing.</summary>
    public static string Of(HttpRequest http) => Norm(http.Headers["X-Lang"].FirstOrDefault());

    public static string T(string lang, string ka) => lang == "en" && En.TryGetValue(ka, out var en) ? en : ka;

    public static string Date(string lang, DateOnly d) => lang == "en"
        ? $"{MonthsEn[d.Month - 1]} {d.Day}, {d.Year}"
        : $"{d.Day} {MonthsKa[d.Month - 1]}, {d.Year}";

    private static readonly string[] MonthsKa =
    [
        "იანვარი", "თებერვალი", "მარტი", "აპრილი", "მაისი", "ივნისი",
        "ივლისი", "აგვისტო", "სექტემბერი", "ოქტომბერი", "ნოემბერი", "დეკემბერი",
    ];

    private static readonly string[] MonthsEn =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    private static readonly Dictionary<string, string> En = new()
    {
        // holidays
        ["ვალენტინობა"] = "Valentine's Day",
        ["8 მარტი"] = "March 8",
        ["ახალი წლის ღამე"] = "New Year's Eve",
        ["ახალი წელი"] = "New Year's Day",
        // cabinet posts
        ["პრემიერ-მინისტრი"] = "Prime Minister",
        ["საგარეო საქმეთა მინისტრი"] = "Minister of Foreign Affairs",
        ["ფინანსთა მინისტრი"] = "Minister of Finance",
        ["თავდაცვის მინისტრი"] = "Minister of Defense",
        ["კულტურის მინისტრი"] = "Minister of Culture",
        ["სიყვარულის მინისტრი"] = "Minister of Love",
        ["ტკბილეულის მინისტრი"] = "Minister of Sweets",
        ["დასვენების მინისტრი"] = "Minister of Rest",
        ["გართობის მინისტრი"] = "Minister of Fun",
        // errors
        ["ასეთი ქვეყანა რუკაზე არ არის."] = "That country is not on the map.",
        ["თარიღი არასწორია."] = "The date is not valid.",
        ["წარსული თარიღის ჩუქება არ შეიძლება."] = "A past date cannot be gifted.",
        ["თარიღი ძალიან შორსაა. აირჩიე უახლოესი 5 წლიდან."] = "That date is too far away. Pick one within the next 5 years.",
        ["ჩაწერე, ვის ჩუქნი (2-დან 40 სიმბოლომდე)."] = "Enter who the gift is for (2 to 40 characters).",
        ["შენი სახელი ძალიან გრძელია."] = "Your name is too long.",
        ["მილოცვა ძალიან გრძელია (მაქსიმუმ 140 სიმბოლო)."] = "The message is too long (140 characters at most).",
        ["სანამ იხდიდი, ეს დღე სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი."] = "Someone took this day while you were paying and the price changed. See the new price.",
        ["გადატრიალება"] = "Coup",
        // titles
        ["პრეზიდენტი"] = "President",
        ["მეფე"] = "King",
        ["დედოფალი"] = "Queen",
        ["იმპერატორი"] = "Emperor",
        ["იმპერატრიცა"] = "Empress",
        ["ტიტული"] = "Title",
        ["საპატიო ტიტულის სერტიფიკატი"] = "Honorary Title Certificate",
        ["ასეთი ტიტული არ არსებობს."] = "There is no such title.",
        ["ელფოსტის მისამართი არასწორია."] = "That email address is not valid.",
        ["ჩაწერე სამინისტროს სახელი (2-დან 30 სიმბოლომდე)."] = "Enter the ministry's name (2 to 30 characters).",
        ["საკუთარი სამინისტროების ლიმიტი ამოიწურა."] = "This cabinet already has the maximum number of custom ministries.",
        ["სერტიფიკატის ბეჭდვა"] = "Print the certificate",
        ["წესები"] = "Terms",
        ["კონფიდენციალურობა"] = "Privacy",
        ["თანხის დაბრუნება"] = "Refunds",
        ["ეს პრეზიდენტი გადატრიალებისგან დაცულია."] = "This president is protected from coups.",
        ["ეს პრეზიდენტი უკვე დაცულია."] = "This president is already protected.",
        ["სანამ იხდიდი, პრეზიდენტი შეიცვალა. გადატვირთე გვერდი."] = "The president changed while you were paying. Reload the page.",
        ["დაცულია გადატრიალებისგან"] = "Protected from coups",
        ["იმუნიტეტი"] = "Immunity",
        ["დაიცავი პრეზიდენტი: ამ დღეს მას ვეღარავინ ჩამოაგდებს."] = "Protect the president: nobody can overthrow them on this day.",
        ["წაართვი პრეზიდენტობა"] = "Overthrow the president",
        ["გადახდა ვერ შესრულდა."] = "The payment did not go through.",
        ["ასეთი თანამდებობა არ არსებობს."] = "There is no such post.",
        ["ჩაწერე სახელი (2-დან 40 სიმბოლომდე)."] = "Enter a name (2 to 40 characters).",
        ["სანამ იხდიდი, ეს სკამი სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი."] = "Someone took this seat while you were paying and the price changed. See the new price.",
        ["მთავრობა უკვე გადახდილია."] = "The cabinet is already paid for.",
        ["ყველა თანამდებობა უკვე დაკავებულია."] = "Every post is already taken.",
        // certificate page and share card
        ["საპატიო პრეზიდენტი"] = "Honorary President",
        ["საპატიო პრეზიდენტის სერტიფიკატი"] = "Honorary President Certificate",
        ["ქვეყანა"] = "Country",
        ["მმართველობის დღე"] = "Day in office",
        ["გამჩუქებელი"] = "A gift from",
        ["სიმბოლური ტიტული. არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას."] = "A symbolic title. It grants no legal or political rights.",
        ["სიმბოლური ტიტული · პრეზიდენტების რუკა"] = "A symbolic title · Presidents Map",
        ["საჩუქარი: {0} ერთი დღით პრეზიდენტია. ქვეყანა: {1}, დღე: {2}."] = "A gift: {0} is president for a day. Country: {1}, date: {2}.",
        ["აჩუქე შენც ქვეყანა"] = "Gift a country too",
        ["პრეზიდენტების რუკა"] = "Presidents Map",
    };
}
