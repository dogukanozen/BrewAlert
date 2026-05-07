#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="$HOME/brewalert"

echo "[1/3] Sistem bağımlılıkları kuruluyor..."
sudo apt-get update -q
sudo apt-get install -y \
  libx11-6 libice6 libsm6 \
  libfontconfig1 libfreetype6 \
  libgl1-mesa-glx libgles2

echo "[2/3] Uygulama $INSTALL_DIR altına kopyalanıyor..."
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$INSTALL_DIR"
cp -r "$SCRIPT_DIR"/. "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/BrewAlert.UI"

echo "[3/3] Systemd kullanıcı servisi oluşturuluyor..."
mkdir -p "$HOME/.config/systemd/user"
cat > "$HOME/.config/systemd/user/brewalert.service" << SERVICE
[Unit]
Description=BrewAlert Brew Timer
After=graphical-session.target
Wants=graphical-session.target

[Service]
ExecStart=$INSTALL_DIR/BrewAlert.UI
Restart=on-failure
Environment=DISPLAY=:0

[Install]
WantedBy=default.target
SERVICE

systemctl --user daemon-reload
systemctl --user enable brewalert.service
systemctl --user start  brewalert.service

echo ""
echo "Kurulum tamamlandı!"
echo "Durum: systemctl --user status brewalert"
