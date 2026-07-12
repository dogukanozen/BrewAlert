using System.Text;
using BrewAlert.TeamsBot;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Teams.Api;
using Microsoft.Teams.Apps.Activities;
using Microsoft.Teams.Plugins.AspNetCore.Extensions;

const int MaxCardBytes = 28 * 1024;

var builder = WebApplication.CreateBuilder(args);
builder.AddTeams();
builder.Services.Configure<BrewAlertBotOptions>(
    builder.Configuration.GetSection(BrewAlertBotOptions.SectionName));
builder.Services.AddSingleton<ConversationStore>();

var app = builder.Build();
var teams = app.UseTeams();
var conversationStore = app.Services.GetRequiredService<ConversationStore>();
var installLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("BrewAlert.TeamsBot.Install");

teams.OnInstall(async (context, cancellationToken) =>
{
    var conversationId = context.Activity.Conversation.Id;

    await conversationStore.AddAsync(
        new ConversationTarget(
            conversationId,
            context.Activity.ServiceUrl ?? string.Empty,
            context.Activity.Conversation.Type?.Value ?? string.Empty),
        cancellationToken);
    installLogger.LogInformation("BrewAlert bot installed in conversation {ConversationId}.", conversationId);
    await context.Send("BrewAlert bildirim botu hazır. ☕", cancellationToken);
});

teams.OnMessage(async (context, cancellationToken) =>
{
    await conversationStore.AddAsync(
        new ConversationTarget(
            context.Activity.Conversation.Id,
            context.Activity.ServiceUrl ?? string.Empty,
            context.Activity.Conversation.Type?.Value ?? string.Empty),
        cancellationToken);
    await context.Send("BrewAlert bu sohbet için hazır. ☕", cancellationToken);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/notifications", async (
    HttpRequest request,
    IOptions<BrewAlertBotOptions> options,
    ConversationStore store,
    CancellationToken cancellationToken) =>
{
    var providedKey = request.Headers["X-BrewAlert-Key"].FirstOrDefault();
    if (!ApiKeyValidator.IsValid(options.Value.ApiKey, providedKey))
        return Results.Unauthorized();

    if (request.ContentLength is > MaxCardBytes)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    var bodySizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (bodySizeFeature is { IsReadOnly: false })
        bodySizeFeature.MaxRequestBodySize = MaxCardBytes;

    string cardJson;
    try
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        cardJson = await reader.ReadToEndAsync(cancellationToken);
    }
    catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    if (Encoding.UTF8.GetByteCount(cardJson) > MaxCardBytes)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    if (!AdaptiveCardPayload.TryDeserialize(cardJson, out var card))
        return Results.BadRequest(new { error = "Request body must be a valid Adaptive Card." });

    var capturedTargets = await store.GetAllAsync(cancellationToken);
    var conversationId = options.Value.ConversationId.Trim();
    if (conversationId.Any(char.IsWhiteSpace))
    {
        return Results.Problem(
            "BrewAlertBot:ConversationId must contain exactly one Teams conversation ID.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    if (string.IsNullOrWhiteSpace(conversationId))
    {
        if (capturedTargets.Count == 0)
        {
            return Results.Problem(
                "Install the BrewAlert Teams app before sending notifications.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (capturedTargets.Count > 1)
        {
            return Results.Conflict(new
            {
                error = "Multiple conversations were captured. Configure BrewAlertBot:ConversationId.",
            });
        }

        conversationId = capturedTargets.Single().Id;
    }

    var capturedTarget = capturedTargets.FirstOrDefault(target =>
        string.Equals(target.Id, conversationId, StringComparison.Ordinal));
    var conversationTypeValue = FirstNotEmpty(
        capturedTarget?.ConversationType,
        options.Value.ConversationType,
        InferConversationType(conversationId));
    var conversationType = new ConversationType(conversationTypeValue);
    var serviceUrlValue = FirstNotEmpty(capturedTarget?.ServiceUrl, options.Value.ServiceUrl);
    var serviceUrl = string.IsNullOrWhiteSpace(serviceUrlValue)
        ? null
        : serviceUrlValue;
    await teams.Send(
        conversationId,
        card!,
        conversationType,
        serviceUrl,
        cancellationToken);
    return Results.Accepted();
});

app.Run();

static string FirstNotEmpty(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

static string InferConversationType(string conversationId) =>
    conversationId.EndsWith("@thread.tacv2", StringComparison.OrdinalIgnoreCase)
        ? "channel"
        : conversationId.EndsWith("@thread.v2", StringComparison.OrdinalIgnoreCase)
            ? "groupChat"
            : "personal";

public partial class Program;
