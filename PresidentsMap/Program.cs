using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PresidentsMap;
using PresidentsMap.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=presidents.db"));
builder.Services.AddSingleton<CountryCatalog>();
builder.Services.AddSingleton<CardRenderer>();

// PayPal turns on when both keys are configured; without them every payment is a simulation.
if (config["PayPal:ClientId"] is { Length: > 0 } && config["PayPal:Secret"] is { Length: > 0 })
    builder.Services.AddHttpClient<IPaymentGateway, PayPalGateway>(c => c.Timeout = TimeSpan.FromSeconds(20));
else
    builder.Services.AddSingleton<IPaymentGateway, DemoPaymentGateway>();

// Email turns on when a mail server is configured; without one, messages are only logged.
if (config["Smtp:Host"] is { Length: > 0 })
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
builder.Services.AddSingleton<Mailer>();
builder.Services.AddHostedService<MailWorker>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Api.BuyLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

// Behind a reverse proxy (the normal way to host this) the real scheme and client address arrive in headers.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.Migrate();

app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGifts();
app.MapCabinet();
app.MapPages();
app.MapAdmin();

app.Run();

public partial class Program;
