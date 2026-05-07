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
sudo tee /etc/systemd/system/$SERVICE_NAME.service > /dev/null << SERVICE
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

[Install]
WantedBy=multi-user.target
SERVICE

sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME.service
sudo systemctl restart $SERVICE_NAME.service

echo ""
echo "Installation complete!"
echo "Status: sudo systemctl status $SERVICE_NAME"
