using System.Text;
using BrewAlert.TeamsBot;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
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

    await conversationStore.AddAsync(conversationId, cancellationToken);
    installLogger.LogInformation("BrewAlert bot installed in conversation {ConversationId}.", conversationId);
    await context.Send("BrewAlert bildirim botu hazır. ☕", cancellationToken);
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

    var conversationId = options.Value.ConversationId;
    if (string.IsNullOrWhiteSpace(conversationId))
    {
        var capturedIds = await store.GetAllAsync(cancellationToken);
        if (capturedIds.Count == 0)
        {
            return Results.Problem(
                "Install the BrewAlert Teams app before sending notifications.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (capturedIds.Count > 1)
        {
            return Results.Conflict(new
            {
                error = "Multiple conversations were captured. Configure BrewAlertBot:ConversationId.",
            });
        }

        conversationId = capturedIds.Single();
    }

    await teams.Send(conversationId, card!, cancellationToken: cancellationToken);
    return Results.Accepted();
});

app.Run();

public partial class Program;
