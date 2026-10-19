# BrewAlert ☕🍵

A brew timer & notification app that alerts your Microsoft Teams channel when your tea or coffee is ready.

Built with [Avalonia UI](https://avaloniaui.net/) for cross-platform support — runs on **Raspberry Pi** with a touchscreen, as well as Windows and Linux desktops.

## Features

- Pre-configured brew profiles (Turkish Tea, Filter Coffee) — editable durations, add/delete custom profiles
- Visual countdown timer with pause / resume / cancel — run several brews at once, each tracked on its own row
- Today's completed brews on the home screen, kept in a local SQLite history
- Ambient idle screen — clock, date, and brew freshness scenes across 12 animation modes, three mode-aware background families, and smooth cinematic transitions
- Microsoft Teams Adaptive Card notifications (Power Automate webhook or Graph API), retried when the network drops
- Settings shows the device's network name and IPv4 address, so a kiosk Pi can be reached over SSH
- TR / EN language toggle in Settings (no restart required)
- Obsidian and Porcelain appearances, selectable instantly in Settings or by a seven-day schedule
- Touch-friendly layouts for Raspberry Pi 3 kiosk screens; bundled Inter text and font-independent vector icons
- Neutral ambient palettes follow the selected appearance across all 12 animation modes
- No secrets in the repository

## Architecture

```
src/
├── BrewAlert.Core/            # Business logic, models, interfaces (zero dependencies)
├── BrewAlert.Infrastructure/  # Teams notifications, JSON profiles, SQLite brew history
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

Environment variables use the prefix `BREWALERT__`. `AddEnvironmentVariables("BREWALERT__")` strips the prefix and converts `__` to `:`, so the `BrewAlert:` config root must remain in the name:

```bash
BREWALERT__BrewAlert__Notifications__Provider=Webhook
```

### Option A: Power Automate Webhook (Recommended)

Sends an Adaptive Card to any Teams channel or chat via a Power Automate flow. No Azure AD app registration needed.

1. In [flow.microsoft.com](https://flow.microsoft.com), create an **Instant cloud flow** with trigger **"When a HTTP request is received"**.
2. Add a **"Post a card in a chat or channel"** action; set the **Adaptive Card** field to `triggerBody()`.
3. Copy the generated HTTP POST URL from the trigger step.
4. Set the URL via the Settings screen, env var, or `appsettings.Development.json`:

   ```bash
   export BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl="https://your-flow-url..."
   export BREWALERT__BrewAlert__Notifications__Provider="Webhook"
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
   export BREWALERT__BrewAlert__Notifications__Provider="Graph"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__TenantId="your-tenant-id"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientId="your-client-id"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientSecret="your-client-secret"
   export BREWALERT__BrewAlert__Notifications__TeamsGraph__ChatId="19:xxx@thread.v2"
   ```

## Deploy to Raspberry Pi

Download the latest release and run the bundled installer on the Pi:

```bash
wget https://github.com/dogukanozen/BrewAlert/releases/latest/download/brewalert-<version>-linux-arm64.tar.gz
mkdir -p ~/brewalert
tar -xzf brewalert-*.tar.gz -C ~/brewalert/
bash ~/brewalert/install.sh
```

`install.sh` installs the required system libraries (`libdrm2`, `libgbm1`, `libfontconfig1`, `libfreetype6`, `libinput10`, `libfuse2`), copies `BrewAlert.AppImage` to `~/brewalert/`, and sets up a systemd service that starts automatically on boot.

The app runs in DRM/KMS mode (`--drm`) for direct framebuffer access — no desktop environment required. Updates are handled by Velopack: the running app checks GitHub Releases at startup and every hour, downloads a new release in the background, and shows an install prompt. The update is applied only when you tap install and no brew is running; the app exits and systemd restarts it with the new version.

### Teams notifications on Raspberry Pi

`install.sh` creates `/etc/brewalert/env` on first install and never overwrites it on subsequent updates — your credentials survive upgrades. Edit the file to enable Teams:

```bash
sudo nano /etc/brewalert/env
```

```
BREWALERT__BrewAlert__Notifications__Provider=Webhook
BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl=https://your-flow-url...
```

```bash
sudo systemctl restart brewalert
```

> **Note:** The URL may contain `%` characters (Power Automate query strings). Using `EnvironmentFile=` avoids systemd treating them as specifiers — do not put the URL directly in the `[Service]` section with `Environment=`.

To check the service after installation:

```bash
sudo systemctl status brewalert
sudo journalctl -u brewalert -n 50
```

## Contributing

Contributions — human or AI — go through the shared brief in [`AGENT.md`](AGENT.md). Commit messages must follow [`docs/commit-style.md`](docs/commit-style.md). PRs use the auto-loaded template.

## License

[MIT](LICENSE)
