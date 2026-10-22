
#!/usr/bin/env bash
set -Eeuo pipefail

# LoginExample Deployment Script
# Phase 1: Preflight checks only.

APP_DIR="/opt/loginexample"
CONFIG_LINK="$APP_DIR/appsettings.Production.json"
CONFIG_FILE="/etc/loginexample/appsettings.Production.json"
SERVICE="loginexample"

STAGING_DIR="${1:-$HOME/apps/LoginExample-next}"

echo "=== LoginExample Deployment Preflight ==="

# 1. Check deployment source
if [[ ! -f "$STAGING_DIR/LoginExample.dll" ]]; then
    echo "ERROR: LoginExample.dll not found in staging directory."
    exit 1
fi

echo "[OK] Staging DLL exists"

# 2. Check required commands
command -v rsync >/dev/null
command -v systemctl >/dev/null
command -v tar >/dev/null

echo "[OK] Required commands available"

# 3. Check production directory
if ! sudo test -d "$APP_DIR"; then
    echo "ERROR: Production directory does not exist."
    exit 1
fi

echo "[OK] Production directory exists"

# 4. Protect production configuration
if [[ "$(sudo readlink "$CONFIG_LINK")" != "$CONFIG_FILE" ]]; then
    echo "ERROR: Production configuration symlink is invalid."
    exit 1
fi

if ! sudo -u loginexample test -r "$CONFIG_LINK"; then
    echo "ERROR: Service account cannot read Production configuration."
    exit 1
fi

echo "[OK] Production configuration is protected"

# 5. Check service state
if ! systemctl is-active --quiet "$SERVICE"; then
    echo "ERROR: LoginExample service is not active."
    exit 1
fi

echo "[OK] LoginExample service is active"

echo
echo "Preflight passed."

# 6. Create deployment backup
BACKUP_DIR="/var/backups/loginexample"

echo "=== Creating Deployment Backup ==="

sudo install -d -o root -g root -m 0700 "$BACKUP_DIR"

BACKUP_FILE="$(sudo mktemp \
    "$BACKUP_DIR/loginexample-$(date +%Y%m%d-%H%M%S)-XXXXXX.tar.gz")"

sudo tar -C /opt -czf "$BACKUP_FILE" loginexample

# Verify archive integrity
sudo tar -tzf "$BACKUP_FILE" > /dev/null

echo "[OK] Backup created: $BACKUP_FILE"
echo "[OK] Backup archive verified"

echo
echo "Backup completed. Application was not modified."
