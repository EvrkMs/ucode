using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Ucode.Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

static X509Certificate2? LoadCertificate(string contentRoot)
{
    var certPath = Environment.GetEnvironmentVariable("HTTPS_CERT_PATH") ?? Path.Combine(contentRoot, "certs", "fullchain.pem");
    var keyPath = Environment.GetEnvironmentVariable("HTTPS_KEY_PATH") ?? Path.Combine(contentRoot, "certs", "privkey.pem");

    if (!File.Exists(certPath) || !File.Exists(keyPath))
    {
        Console.WriteLine($"[startup] HTTPS certificate or key not found at {certPath} / {keyPath}, falling back to HTTP.");
        return null;
    }

    try
    {
        return X509Certificate2.CreateFromPemFile(certPath, keyPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[startup] Failed to load HTTPS certificate: {ex.Message}");
        return null;
    }
}

var certificate = LoadCertificate(builder.Environment.ContentRootPath);

builder.WebHost.ConfigureKestrel(options =>
{
    // Allow slow uploads through proxies (prevents 408 from MinRequestBodyDataRate).
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MinResponseDataRate = null;

    if (certificate is not null)
    {
        options.ListenAnyIP(5001, listenOptions =>
        {
            listenOptions.UseHttps(certificate);
        });
    }
    else
    {
        options.ListenAnyIP(5001);
    }
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Доверяем всем прокси в цепочке (nginx-cf + docker gateway), чтобы не обрезались реальные IP.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Cloudflare сети
    var cfRanges = new (string ip, int prefix)[]
    {
        ("188.114.96.0", 20),
        ("190.93.240.0", 20),
        ("172.64.0.0", 13),
        ("104.16.0.0", 13),
        ("162.158.0.0", 15),
        ("108.162.192.0", 18),
        ("131.0.72.0", 22),
        ("2a06:98c0::", 29),
        ("2c0f:f248::", 32)
    };

    // Локальные сети docker/localhost
    var localRanges = new (string ip, int prefix)[]
    {
        ("127.0.0.0", 8),
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16)
    };

    void AddRange((string ip, int prefix) range)
    {
        if (IPAddress.TryParse(range.ip, out var addr))
        {
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(addr, range.prefix));
        }
    }

    foreach (var r in cfRanges) AddRange(r);
    foreach (var r in localRanges) AddRange(r);
});

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.Configure<WebSocketOptions>(options =>
{
    // Поддерживаем соединение за прокси, отправляя ping каждые 30 секунд.
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/redeem") &&
        DateTime.Now.Date >= new DateTime(2026, 1, 10))
    {
        context.Response.StatusCode = 410;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Акция закончилась");
        return;
    }
    await next(context);
});

app.UseForwardedHeaders();
app.UseApplication();
app.Run();
