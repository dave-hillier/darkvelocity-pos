namespace DarkVelocity.Host.Auth;

public sealed class OAuthSettings
{
    public GoogleSettings Google { get; set; } = new();
    public MicrosoftSettings Microsoft { get; set; } = new();
}

public sealed class GoogleSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class MicrosoftSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
