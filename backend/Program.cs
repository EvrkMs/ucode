using Microsoft.AspNetCore.HttpOverrides;
using Ucode.Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5001");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Доверяем всем прокси в цепочке (nginx-cf + docker gateway), чтобы не обрезались реальные IP.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApplication();
app.Run();
