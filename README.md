# BrewAlert ☕🍵

A brew timer & notification app that alerts your Microsoft Teams channel when your tea or coffee is ready.

Built with [Avalonia UI](https://avaloniaui.net/) for cross-platform support — runs on **Raspberry Pi** with a touchscreen, as well as Windows and Linux desktops.

## Features

- Pre-configured brew profiles (Turkish Tea, French Press, Pour Over, …)
- Visual countdown timer with pause / resume / cancel
- Microsoft Teams notifications (Graph API or Incoming Webhook)
- Dark theme optimised for small screens
- No secrets in the repository

## Architecture

```
src/
├── BrewAlert.Core/            # Business logic, models, interfaces (zero dependencies)
├── BrewAlert.Infrastructure/  # Teams notifications, JSON persistence
└── BrewAlert.UI/              # Avalonia views + view models

tests/
├── BrewAlert.Core.Tests/
└── BrewAlert.Infrastructure.Tests/
```

See [`docs/architecture.md`](docs/architecture.md) for detailed design.

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/BrewAlert.UI   # run
dotnet test                             # test
```

## Configure Teams notifications

BrewAlert supports two notification methods. Configuration is handled via `appsettings.json` or environment variables (prefixed with `BREWALERT__`).

### Option A: Microsoft Graph API (Recommended)
Sends messages to a specific chat using an Azure AD App Registration.

1. Create an App Registration with `Chat.ReadWrite` permission.
2. Set environment variables:
   ```bash
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__Enabled="true"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__TenantId="your-tenant-id"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientId="your-client-id"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientSecret="your-client-secret"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ChatId="your-chat-id"
   ```

### Option B: Incoming Webhook
Sends adaptive cards to a Teams channel.

1. Create an [Incoming Webhook](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook) in your Teams channel.
2. Set environment variables:

   ```bash
   export BREWALERT__BrewAlert__Notifications__Teams__Enabled="true"
   export BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl="https://outlook.office.com/webhook/..."
   ```

If neither is configured, BrewAlert falls back to a console notifier (useful for local dev).

## Deploy to Raspberry Pi

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
scp -r src/BrewAlert.UI/bin/Release/net10.0/linux-arm64/publish/ pi@raspberrypi:/opt/brewalert/
```

## Contributing

Contributions — human or AI — go through the shared brief in [`AGENT.md`](AGENT.md). Commit messages must follow [`docs/commit-style.md`](docs/commit-style.md). PRs use the auto-loaded template.

## License

[MIT](LICENSE)
