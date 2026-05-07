#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="$HOME/brewalert"
SERVICE_NAME="brewalert"

echo "[1/3] Sistem bağımlılıkları kuruluyor..."
sudo apt-get update -q
sudo apt-get install -y \
  libdrm2 libgbm1 \
  libfontconfig1 libfreetype6

echo "[2/3] Uygulama $INSTALL_DIR altına kopyalanıyor..."
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$INSTALL_DIR"
cp -r "$SCRIPT_DIR"/. "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/BrewAlert.UI"

echo "[3/3] Systemd servisi oluşturuluyor..."
sudo tee /etc/systemd/system/$SERVICE_NAME.service > /dev/null << SERVICE
[Unit]
Description=BrewAlert Brew Timer App
After=network.target

[Service]
User=$(whoami)
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/BrewAlert.UI --drm
Restart=always
RestartSec=5
StandardOutput=inherit
StandardError=inherit

[Install]
WantedBy=multi-user.target
SERVICE

sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME.service
sudo systemctl restart $SERVICE_NAME.service

echo ""
echo "Kurulum tamamlandı!"
echo "Durum: sudo systemctl status $SERVICE_NAME"
