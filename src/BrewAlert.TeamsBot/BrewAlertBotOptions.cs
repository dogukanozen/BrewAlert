namespace BrewAlert.TeamsBot;

public sealed class BrewAlertBotOptions
{
    public const string SectionName = "BrewAlertBot";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional fixed target. When empty, the single conversation captured during
    /// Teams app installation is used.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Conversation type passed to Teams for proactive sends.</summary>
    public string ConversationType { get; set; } = "groupChat";

    /// <summary>
    /// Regional Bot Connector service URL captured from Teams. When empty, the
    /// Teams SDK default is used.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string ConversationStorePath { get; set; } = "App_Data/conversations.json";
}
