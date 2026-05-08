#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="$HOME/brewalert"
SERVICE_NAME="brewalert"
APPIMAGE_NAME="BrewAlert.AppImage"

echo "[1/3] Installing system dependencies..."
sudo apt-get update -q
sudo apt-get install -y \
  libdrm2 libgbm1 \
  libfontconfig1 libfreetype6 \
  libinput10 \
  libfuse2

echo "[2/3] Installing app to $INSTALL_DIR..."
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$INSTALL_DIR"
if [ "$SCRIPT_DIR" != "$INSTALL_DIR" ]; then
  cp "$SCRIPT_DIR/$APPIMAGE_NAME" "$INSTALL_DIR/$APPIMAGE_NAME"
fi
chmod +x "$INSTALL_DIR/$APPIMAGE_NAME"

echo "[3/3] Setting up systemd service..."

SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"
ENV_FILE="/etc/brewalert/env"

# Create env file only on first install — never overwrite on update so user config survives
if [ ! -f "$ENV_FILE" ]; then
  sudo mkdir -p /etc/brewalert
  sudo tee "$ENV_FILE" > /dev/null << 'ENVFILE'
# BrewAlert environment configuration — survives app updates
# Uncomment and fill in to enable Teams notifications:
#BREWALERT__BrewAlert__Notifications__Provider=Webhook
#BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl=https://your-flow-url...
ENVFILE
  sudo chmod 600 "$ENV_FILE"
fi

sudo tee "$SERVICE_FILE" > /dev/null << SERVICE
[Unit]
Description=BrewAlert Brew Timer App
After=network.target

[Service]
User=$(whoami)
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/$APPIMAGE_NAME --drm
Restart=always
RestartSec=5
KillMode=process
StandardOutput=inherit
StandardError=inherit
EnvironmentFile="$ENV_FILE"
SERVICE

sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME.service
sudo systemctl restart $SERVICE_NAME.service

echo ""
echo "Installation complete!"
echo "Status: sudo systemctl status $SERVICE_NAME"
