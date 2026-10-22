#!/usr/bin/env bash
set -Eeuo pipefail

# LoginExample Deployment Pipeline

APP_DIR="/opt/loginexample"
CONFIG_FILE="/etc/loginexample/appsettings.Production.json"
CONFIG_LINK="$APP_DIR/appsettings.Production.json"
BACKUP_DIR="/var/backups/loginexample"
SERVICE="loginexample"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SWITCH_SCRIPT="$SCRIPT_DIR/switch-release.sh"

ACTION="${1:-}"
STAGING_DIR="${2:-}"
RELEASE_ID="${3:-}"

# 1. Validate arguments
case "$ACTION" in
    --check|--backup|--apply)
        ;;
    *)
        echo "Usage: bash deploy.sh <--check|--backup|--apply> <staging-dir> [release-id]"
        exit 2
        ;;
esac

if [[ -z "$STAGING_DIR" || "$STAGING_DIR" != /* ]]; then
    echo "ERROR: Staging directory must be an absolute path."
    exit 2
fi

if [[ "$ACTION" == "--apply" ]]; then
    if [[ ! "$RELEASE_ID" =~ ^[a-zA-Z0-9][a-zA-Z0-9._-]*$ ]]; then
        echo "ERROR: Invalid release ID."
        exit 2
    fi

    RELEASE_DIR="/opt/loginexample-release-${RELEASE_ID}"
    PREVIOUS_DIR="/opt/loginexample-previous-${RELEASE_ID}"
fi

# Require root privileges for the deployment pipeline.
if [[ "$EUID" -ne 0 ]]; then
    echo "ERROR: Run using sudo bash deploy.sh."
    exit 1
fi

# Shared deployment lock.
# File descriptor 9 remains open for the entire pipeline.
LOCK_FILE="/run/lock/loginexample-deploy.lock"

umask 077
exec 9>"$LOCK_FILE"

if ! flock -n 9; then
    echo "ERROR: Another deployment is already running."
    exit 1
fi

echo "[OK] Deployment lock acquired."
echo "=== LoginExample Deployment Pipeline ==="
echo "Action: $ACTION"
echo "Staging: $STAGING_DIR"

# 2. Preflight
echo
echo "=== Preflight ==="

test -f "$STAGING_DIR/LoginExample.dll"
echo "[OK] Staging DLL exists"

for cmd in sudo rsync tar systemctl curl flock; do
    command -v "$cmd" >/dev/null
done
echo "[OK] Required commands available"

sudo test -d "$APP_DIR"
echo "[OK] Production directory exists"

test "$(sudo readlink "$CONFIG_LINK")" = "$CONFIG_FILE"
sudo -u loginexample test -r "$CONFIG_LINK"
echo "[OK] Production configuration is protected"

systemctl is-active --quiet "$SERVICE"
echo "[OK] Service is active"

if [[ "$ACTION" == "--apply" ]]; then
    test -f "$SWITCH_SCRIPT" || {
        echo "ERROR: switch-release.sh not found."
        exit 1
    }

    if sudo test -e "$RELEASE_DIR" ||
       sudo test -L "$RELEASE_DIR" ||
       sudo test -e "$PREVIOUS_DIR" ||
       sudo test -L "$PREVIOUS_DIR"; then
        echo "ERROR: Release ID has already been used or its path exists."
        exit 1
    fi

    echo "[OK] Release paths available"
fi

echo "Preflight passed."

if [[ "$ACTION" == "--check" ]]; then
    echo "No deployment changes were made."
    exit 0
fi

# 3. Backup
echo
echo "=== Backup ==="

sudo install -d -o root -g root -m 0700 "$BACKUP_DIR"

BACKUP_FILE="$(sudo mktemp \
    "$BACKUP_DIR/loginexample-$(date +%Y%m%d-%H%M%S)-XXXXXX.tar.gz")"

sudo tar -C /opt -czf "$BACKUP_FILE" loginexample
sudo tar -tzf "$BACKUP_FILE" >/dev/null

echo "[OK] Backup created and verified: $BACKUP_FILE"

if [[ "$ACTION" == "--backup" ]]; then
    echo "Backup completed. Application was not modified."
    exit 0
fi

# 4. Prepare release
echo
echo "=== Prepare Release: $RELEASE_ID ==="

sudo install -d \
    -o root -g loginexample -m 750 \
    "$RELEASE_DIR"

sudo rsync -rlt \
    --chown=root:loginexample \
    --chmod=D750,F640 \
    --exclude='/appsettings.Production.json' \
    "$STAGING_DIR/" \
    "$RELEASE_DIR/"

sudo ln -s "$CONFIG_FILE" \
    "$RELEASE_DIR/appsettings.Production.json"

# 5. Verify release before switching
bash "$SWITCH_SCRIPT" --check "$RELEASE_ID"

echo "[OK] Release candidate verified"

# 6. Activate release and handle rollback
echo
echo "=== Activate Release ==="

bash "$SWITCH_SCRIPT" --apply "$RELEASE_ID"

echo
echo "Deployment pipeline completed successfully."
