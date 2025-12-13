using Ucode.Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5001");

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseApplication();
app.Run();
