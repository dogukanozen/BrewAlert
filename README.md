# BrewAlert ☕🍵

A brew timer & notification app that alerts your Microsoft Teams channel when your tea or coffee is ready.

Built with [Avalonia UI](https://avaloniaui.net/) for cross-platform support — runs on **Raspberry Pi** with a touchscreen, as well as Windows and Linux desktops.

## Features

- Pre-configured brew profiles (Turkish Tea, Turkish Coffee) — editable durations, add/delete custom profiles
- Visual countdown timer with pause / resume / cancel
- Microsoft Teams Adaptive Card notifications (Power Automate webhook or Graph API)
- TR / EN language toggle in Settings (no restart required)
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
├── BrewAlert.Infrastructure.Tests/
└── BrewAlert.UI.Tests/
```

See [`docs/architecture.md`](docs/architecture.md) for detailed design.

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/BrewAlert.UI   # run
dotnet test                             # test
```

## Configure Teams notifications

BrewAlert has three notification backends selected via a single `Provider` setting. Switch between them live from the Settings screen (no restart required) — or set it in config:

```json
// %AppData%\BrewAlert\preferences.json
{ "BrewAlert": { "Notifications": { "Provider": "Webhook" } } }
```

| `Provider` | Description |
|---|---|
| `"Webhook"` | Power Automate webhook — no Azure registration needed |
| `"Graph"` | Microsoft Graph API — posts to a specific Teams chat |
| `"Console"` | Stdout only — default for local dev |

Environment variables use the prefix `BREWALERT__` (replaces the `BrewAlert:` config root):

```bash
BREWALERT__Notifications__Provider=Webhook
```

### Option A: Power Automate Webhook (Recommended)

Sends an Adaptive Card to any Teams channel or chat via a Power Automate flow. No Azure AD app registration needed.

1. In [flow.microsoft.com](https://flow.microsoft.com), create an **Instant cloud flow** with trigger **"When a HTTP request is received"**.
2. Add a **"Post a card in a chat or channel"** action; set the **Adaptive Card** field to `triggerBody()`.
3. Copy the generated HTTP POST URL from the trigger step.
4. Set the URL via env var or `appsettings.Development.json`:

   ```bash
   export BREWALERT__Notifications__Teams__WebhookUrl="https://your-flow-url..."
   export BREWALERT__Notifications__Provider="Webhook"
   ```

   Or in `appsettings.Development.json` (gitignored):

   ```json
   {
     "BrewAlert": {
       "Notifications": {
         "Provider": "Webhook",
         "Teams": { "WebhookUrl": "https://your-flow-url..." }
       }
     }
   }
   ```

### Option B: Microsoft Graph API

Posts to a specific Teams chat via an Azure AD App Registration (client credentials flow).

> **Limitation:** `POST /chats/{id}/messages` with application-only tokens requires Resource-Specific Consent (RSC) on the target chat in addition to the `Chat.ReadWrite.All` permission. Without RSC the API returns 403. Use Option A unless you specifically need Graph.

1. Create an App Registration, add `Chat.ReadWrite.All` **Application** permission, grant admin consent, and configure RSC for the target chat.
2. Set the credentials via env vars or `appsettings.Development.json`:

   ```bash
   export BREWALERT__Notifications__Provider="Graph"
   export BREWALERT__Notifications__TeamsGraph__TenantId="your-tenant-id"
   export BREWALERT__Notifications__TeamsGraph__ClientId="your-client-id"
   export BREWALERT__Notifications__TeamsGraph__ClientSecret="your-client-secret"
   export BREWALERT__Notifications__TeamsGraph__ChatId="19:xxx@thread.v2"
   ```

## Deploy to Raspberry Pi

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
scp -r src/BrewAlert.UI/bin/Release/net10.0/linux-arm64/publish/ pi@raspberrypi:/opt/brewalert/
```

## Contributing

Contributions — human or AI — go through the shared brief in [`AGENT.md`](AGENT.md). Commit messages must follow [`docs/commit-style.md`](docs/commit-style.md). PRs use the auto-loaded template.

## License

[MIT](LICENSE)
