namespace BrewAlert.Infrastructure.Configuration;

public sealed class TeamsGraphOptions
{
    public const string SectionPath = "BrewAlert:Notifications:TeamsGraph";

    public bool Enabled { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Target Teams chat ID. Find it via Graph Explorer: GET /me/chats</summary>
    public string ChatId { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
