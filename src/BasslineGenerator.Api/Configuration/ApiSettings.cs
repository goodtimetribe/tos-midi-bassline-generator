namespace BasslineGenerator.Api.Configuration;

public sealed class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitSettings
{
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
}
