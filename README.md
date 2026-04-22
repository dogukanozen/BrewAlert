# BrewAlert ☕🍵

A brew timer & notification app that alerts your Microsoft Teams channel when your tea or coffee is ready.

Built with [Avalonia UI](https://avaloniaui.net/) for cross-platform support — runs on **Raspberry Pi** with a touchscreen display, as well as Windows and Linux desktops.

## Features

- 🍵 Pre-configured brew profiles (Turkish Tea, French Press, Pour Over, etc.)
- ⏱️ Visual countdown timer with pause/resume
- 📢 Microsoft Teams notifications via Incoming Webhook
- 🎨 Dark theme optimized for small screens
- 🔒 Secure — no secrets in the repository

## Architecture

```
src/
├── BrewAlert.Core/            # Business logic, models, interfaces (zero dependencies)
├── BrewAlert.Infrastructure/  # Teams webhook, JSON persistence, hardware
└── BrewAlert.UI/              # Avalonia views + view models

tests/
├── BrewAlert.Core.Tests/
└── BrewAlert.Infrastructure.Tests/
```

See [docs/architecture.md](docs/architecture.md) for detailed design documentation.

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run locally
```bash
dotnet run --project src/BrewAlert.UI
```

### Run tests
```bash
dotnet test
```

### Configure Teams Notifications

1. Create an [Incoming Webhook](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook) in your Teams channel
2. Set the webhook URL via environment variable:
   ```bash
   # Linux / macOS
   export BREWALERT__NOTIFICATIONS__TEAMS__WEBHOOKURL="https://outlook.office.com/webhook/..."
   export BREWALERT__NOTIFICATIONS__TEAMS__ENABLED="true"

   # Windows PowerShell
   $env:BREWALERT__NOTIFICATIONS__TEAMS__WEBHOOKURL="https://outlook.office.com/webhook/..."
   $env:BREWALERT__NOTIFICATIONS__TEAMS__ENABLED="true"
   ```

### Deploy to Raspberry Pi
```bash
# Build for Pi 4/5 (64-bit)
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained

# Copy to Pi
scp -r src/BrewAlert.UI/bin/Release/net10.0/linux-arm64/publish/ pi@raspberrypi:/opt/brewalert/
```

## Contributing

See [CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines on how to contribute.

## License

[MIT](LICENSE)
