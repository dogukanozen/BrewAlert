# BrewAlert Teams bot

The bot receives the same root Adaptive Card JSON currently sent to Power Automate and posts it to the Teams conversation where the app is installed. Teams displays the app identity (`BrewAlert`) instead of `Workflows`.

## Local registration

Prerequisites: .NET 10, Node.js 20+, a Microsoft 365 account with custom app upload enabled, and a public HTTPS tunnel to local port 3978.

```powershell
npx --yes @microsoft/teams.cli@latest login
npx --yes @microsoft/teams.cli@latest app create `
  --name BrewAlert `
  --endpoint https://<tunnel-host>/api/messages `
  --color-icon appPackage/color.png `
  --outline-icon appPackage/outline.png `
  --env appsettings.Development.json
```

The command creates the Teams app and bot identity, writes authentication values to the gitignored development settings file, and prints an **Install in Teams** link. Use that link to install BrewAlert in the desired personal chat, group chat, or team. Installation captures the conversation ID automatically.

For manual packaging, copy `appPackage/manifest.template.json` to `manifest.json`, replace `${TEAMS_APP_ID}` and `${BOT_ID}` with the IDs printed by the CLI, then zip these files at the root of the archive:

- `manifest.json`
- `color.png`
- `outline.png`

Upload the zip through **Teams > Apps > Manage your apps > Upload a custom app**.

## Configure the notification endpoint

Set a long random key for the bot service:

```powershell
$env:BrewAlertBot__ApiKey = '<random-secret>'
dotnet run --project src/BrewAlert.TeamsBot
```

Point the desktop app at the bot and configure the same key:

```powershell
$env:BREWALERT__BrewAlert__Notifications__Provider = 'Webhook'
$env:BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl = 'https://<bot-host>/api/notifications'
$env:BREWALERT__BrewAlert__Notifications__Teams__ApiKey = '<random-secret>'
```

When the bot has been installed in more than one conversation, set `BrewAlertBot__ConversationId` on the bot host to select the notification target.

Never commit bot credentials, the endpoint key, or environment-specific appsettings files.
