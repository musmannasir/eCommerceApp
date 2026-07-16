namespace ECommerceApp.Application.Common.Options;

/// <summary>Binds the "Jwt" configuration section. <see cref="Key"/> is only ever set via User Secrets/environment.</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
