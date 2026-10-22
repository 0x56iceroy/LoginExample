
#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE="loginexample"
APP_DIR="/opt/loginexample"
RELEASE_DIR="/opt/loginexample-release-sprint06"
PREVIOUS_DIR="/opt/loginexample-previous-sprint06"
CONFIG="/etc/loginexample/appsettings.Production.json"

# Do not deploy without an explicit argument.
if [[ "${1:-}" != "--apply" ]]; then
    echo "Usage: sudo bash switch-release.sh --apply"
    exit 2
fi

if [[ "$EUID" -ne 0 ]]; then
    echo "ERROR: Run as root using sudo."
    exit 1
fi

# Preflight
test -f "$RELEASE_DIR/LoginExample.dll"
test -d "$APP_DIR"
test ! -e "$PREVIOUS_DIR"
test -L "$RELEASE_DIR/appsettings.Production.json"
test "$(readlink "$RELEASE_DIR/appsettings.Production.json")" = "$CONFIG"

runuser -u loginexample -- test -r "$RELEASE_DIR/LoginExample.dll"
runuser -u loginexample -- test -r "$RELEASE_DIR/appsettings.Production.json"

systemctl is-active --quiet "$SERVICE"

stopped=0
old_moved=0
new_moved=0
completed=0


rollback() {
    local exit_code=$?
    trap - EXIT INT TERM

    if (( completed == 1 || stopped == 0 )); then
        exit "$exit_code"
    fi

    echo "ERROR: Deployment failed. Starting rollback."

    systemctl stop "$SERVICE" || true

    # Move failed release out of the production path.
    if (( new_moved == 1 )); then
        if [[ -e "$RELEASE_DIR" ]] ||
           ! mv "$APP_DIR" "$RELEASE_DIR"; then
            echo "CRITICAL: Cannot move failed release."
            exit 1
        fi
    fi

    # Restore the previous release.
    if (( old_moved == 1 )); then
        if [[ -e "$APP_DIR" ]] ||
           ! mv "$PREVIOUS_DIR" "$APP_DIR"; then
            echo "CRITICAL: Cannot restore previous release."
            exit 1
        fi
    fi

    if ! systemctl start "$SERVICE"; then
        echo "CRITICAL: Previous service failed to start."
        exit 1
    fi

    echo "Checking restored service health..."

    for attempt in {1..30}; do
        if curl --silent --fail --max-time 2 \
            http://127.0.0.1:5000/Articles > /dev/null &&
           curl --silent --fail --max-time 2 \
            http://127.0.0.1/Articles > /dev/null; then

            echo "[OK] Previous release restored and healthy."
            exit "$exit_code"
        fi

        sleep 1
    done

    echo "CRITICAL: Rollback health check failed."
    exit 1
}


trap rollback EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

echo "Stopping $SERVICE..."
stopped=1
systemctl stop "$SERVICE"

echo "Preserving current release..."
mv "$APP_DIR" "$PREVIOUS_DIR"
old_moved=1

echo "Activating new release..."
mv "$RELEASE_DIR" "$APP_DIR"
new_moved=1

systemctl start "$SERVICE"

echo "Checking HTTP health..."

healthy=0
for attempt in {1..30}; do
    if curl --silent --show-error --fail \
        --max-time 2 \
        http://127.0.0.1:5000/Articles > /dev/null 2>&1 &&
       curl --silent --show-error --fail \
        --max-time 2 \
        http://127.0.0.1/Articles > /dev/null 2>&1; then
        healthy=1
        break
    fi
    sleep 1
done

if (( healthy != 1 )); then
    echo "ERROR: Health check failed."
    exit 1
fi

completed=1
trap - EXIT

echo "Deployment succeeded."
echo "Previous release: $PREVIOUS_DIR"
