using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace PresidentsMap;

public record Email(string To, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(Email email, CancellationToken ct);
}

/// <summary>Used when no mail server is configured: the message only goes to the log.</summary>
public class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    public Task SendAsync(Email email, CancellationToken ct)
    {
        log.LogInformation("Email not sent (no mail server configured). To: {To}. Subject: {Subject}", email.To, email.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Sends through any SMTP server; settings come from the Smtp section.</summary>
public class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(Email email, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Smtp:From"] ?? config["Smtp:User"]));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };

        using var client = new SmtpClient();
        var port = config.GetValue("Smtp:Port", 587);
        var security = config.GetValue("Smtp:Tls", true) ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await client.ConnectAsync(config["Smtp:Host"], port, security, ct);
        if (config["Smtp:User"] is { Length: > 0 } user) await client.AuthenticateAsync(user, config["Smtp:Password"] ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

/// <summary>What the overthrown buyer needs to hear, captured before their data is replaced.</summary>
public record Deposed(string Email, string Name, string Lang, string Origin);

/// <summary>
/// Writes the site's emails and queues them. Sending happens in the background,
/// so a slow or broken mail server never delays or fails a purchase.
/// </summary>
public class Mailer
{
    private readonly Channel<Email> _queue = Channel.CreateBounded<Email>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<Email> Queue => _queue.Reader;

    private void Send(string to, string subject, string body) => _queue.Writer.TryWrite(new Email(to, subject, body));

    public void Receipt(Gift g, Country c)
    {
        if (g.BuyerEmail.Length == 0) return;
        var en = g.Lang == "en";
        var link = $"{g.Origin}/cert/{g.Id}";
        var what = $"{g.To} · {c.Name(g.Lang)} · {Loc.Date(g.Lang, g.Date)}";
        Send(g.BuyerEmail,
            en ? $"Your gift is ready: {what}" : $"შენი საჩუქარი მზადაა: {what}",
            en
                ? $"Thank you!\n\n{what}\nTitle: {Titles.Name(g.Title, "en")}\nPaid: ${g.Price}\n\nThe certificate and the cabinet:\n{link}\n\nSend this link to {g.To}. Friends who have it can take seats in the cabinet.\n"
                  + (g.Immune ? "\nThis day is protected from coups.\n" : "\nAnyone can overthrow an unprotected ruler. You can add immunity on the certificate page.\n")
                  + "\nPresidents Map. A symbolic title; it grants no legal or political rights."
                : $"გმადლობთ!\n\n{what}\nტიტული: {Titles.Name(g.Title, "ka")}\nგადახდილია: ${g.Price}\n\nსერტიფიკატი და მთავრობა:\n{link}\n\nგაუგზავნე ეს ბმული: {g.To}. ვისაც ბმული აქვს, მთავრობაში ადგილის დაკავება შეუძლია.\n"
                  + (g.Immune ? "\nეს დღე გადატრიალებისგან დაცულია.\n" : "\nდაუცველ მმართველს ნებისმიერი ჩამოაგდებს. იმუნიტეტის დამატება სერტიფიკატის გვერდიდან შეიძლება.\n")
                  + "\nპრეზიდენტების რუკა. სიმბოლური ტიტული; არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას.");
    }

    public void Overthrown(Deposed old, Gift now, Country c)
    {
        var en = old.Lang == "en";
        var back = Pricing.CoupPrice(now.Price);
        var link = $"{old.Origin}/?c={now.Country}&d={now.Date:yyyy-MM-dd}";
        var country = c.Name(old.Lang);
        var date = Loc.Date(old.Lang, now.Date);
        Send(old.Email,
            en ? $"{old.Name} was overthrown: {country}" : $"{old.Name} ჩამოაგდეს: {country}",
            en
                ? $"{now.To} took {country} on {date} from {old.Name}.\n\n"
                  + (now.Immune ? "The new ruler bought immunity, so this day cannot be taken back.\n" : $"Take it back for ${back}:\n{link}\n")
                  + "\nPresidents Map"
                : $"{now.To}: ახალი მმართველი. ქვეყანა: {country}, დღე: {date}. ჩამოგდებულია: {old.Name}.\n\n"
                  + (now.Immune ? "ახალმა მმართველმა იმუნიტეტი იყიდა, ამიტომ ამ დღის დაბრუნება აღარ შეიძლება.\n" : $"დაიბრუნე ${back}-ად:\n{link}\n")
                  + "\nპრეზიდენტების რუკა");
    }

    public void Delivery(Gift g, Country c)
    {
        var en = g.Lang == "en";
        var link = $"{g.Origin}/cert/{g.Id}";
        var country = c.Name(g.Lang);
        var title = Titles.Name(g.Title, g.Lang);
        Send(g.RecipientEmail,
            en ? $"A gift for you: {country} is yours today" : $"საჩუქარი შენთვის: {country} დღეს შენია",
            en
                ? $"{g.To},\n\n" + (g.From.Length > 0 ? $"{g.From} made you {title} for a day.\n" : $"Someone made you {title} for a day.\n")
                  + $"Country: {country}\nDay in office: {Loc.Date("en", g.Date)}\n" + (g.Message.Length > 0 ? $"\n“{g.Message}”\n" : "")
                  + $"\nYour certificate and your cabinet:\n{link}\n\nPresidents Map. A symbolic title; it grants no legal or political rights."
                : $"{g.To},\n\n" + (g.From.Length > 0 ? $"გამჩუქებელი: {g.From}\n" : "") + $"შენი ტიტული ერთი დღით: {title}\n"
                  + $"ქვეყანა: {country}\nმმართველობის დღე: {Loc.Date("ka", g.Date)}\n" + (g.Message.Length > 0 ? $"\n„{g.Message}“\n" : "")
                  + $"\nშენი სერტიფიკატი და მთავრობა:\n{link}\n\nპრეზიდენტების რუკა. სიმბოლური ტიტული; არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას.");
    }
}

/// <summary>Background worker: sends queued emails, and delivers each certificate to its recipient when its day comes.</summary>
public class MailWorker(Mailer mailer, IEmailSender sender, IServiceScopeFactory scopes, CountryCatalog catalog, ILogger<MailWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var deliveries = DeliverDueGiftsAsync(stop);
        await foreach (var email in mailer.Queue.ReadAllAsync(stop))
        {
            try { await sender.SendAsync(email, stop); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Email to {To} failed: {Subject}", email.To, email.Subject); }
        }
        await deliveries;
    }

    private async Task DeliverDueGiftsAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { await DeliverOnceAsync(stop); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Gift delivery pass failed"); }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }

    /// <summary>The day starts at 00:00 UTC. Each gift is marked first, so a crash can skip one email but never send it twice.</summary>
    public async Task DeliverOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = await db.Gifts.Where(g => !g.Delivered && g.RecipientEmail != "" && g.Date <= today).OrderBy(g => g.Date).Take(100).ToListAsync(ct);
        foreach (var g in due)
        {
            var coups = g.Coups;
            var marked = await db.Gifts.Where(x => x.Id == g.Id && !x.Delivered && x.Coups == coups)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Delivered, true), ct);
            if (marked == 1 && catalog.Find(g.Country) is { } country) mailer.Delivery(g, country);
        }
    }
}
