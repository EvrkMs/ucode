using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ucode.Backend.Data;
using Ucode.Backend.Middleware;
using Ucode.Backend.Options;
using Ucode.Backend.Services;

namespace Ucode.Backend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        services.AddDbContext<UcodeDbContext>(options =>
        {
            var conn = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
            }

            options.UseNpgsql(conn);
        });

        services.AddSingleton<ITelegramAuthValidator, TelegramAuthValidator>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ILeaderboardNotifier, LeaderboardNotifier>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICodeService, CodeService>();

        services.AddControllers();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

                if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
                {
                    throw new InvalidOperationException("Jwt:SigningKey is not configured.");
                }

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    RoleClaimType = "role"
                };

                options.Events = JwtEventsFactory.Create();
            });

        services.AddOpenApi();
        return services;
    }
}
