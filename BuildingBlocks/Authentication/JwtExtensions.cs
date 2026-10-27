using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication;

public static class JwtExtensions
{
    /// <summary>HMAC-SHA256 needs at least 256 bits of key material.</summary>
    private const int MinimumKeyBytes = 32;

    /// <summary>
    /// Validates JWTs issued by IdentityService. The signing key is a secret: it is read from user-secrets in
    /// development (scripts/setup-dev-secrets.ps1) or the Jwt__Key environment variable in containers, never from
    /// appsettings.json. The service refuses to start without it.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key is missing or shorter than {MinimumKeyBytes} bytes. " +
                "Run scripts/setup-dev-secrets.ps1 (development) or set the Jwt__Key environment variable.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    // Default is 5 minutes; with 15-minute access tokens a smaller skew keeps expiry meaningful.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
        return services;
    }
}
