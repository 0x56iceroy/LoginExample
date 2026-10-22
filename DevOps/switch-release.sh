
#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE="loginexample"
APP_DIR="/opt/loginexample"
CONFIG="/etc/loginexample/appsettings.Production.json"

# Require an explicit deployment version.
ACTION="${1:-}"

if [[ "$ACTION" != "--apply" && "$ACTION" != "--check" ]]; then
    echo "Usage: sudo bash switch-release.sh <--check|--apply> <release-id>"
    exit 2
fi

RELEASE_ID="${2:-}"

# Allow only safe release identifiers.
if [[ ! "$RELEASE_ID" =~ ^[a-zA-Z0-9][a-zA-Z0-9._-]*$ ]]; then
    echo "ERROR: Invalid release ID."
    exit 2
fi

# Initialize deployment paths after validating the release ID.
RELEASE_DIR="/opt/loginexample-release-${RELEASE_ID}"
PREVIOUS_DIR="/opt/loginexample-previous-${RELEASE_ID}"

if [[ "$EUID" -ne 0 ]]; then
    echo "ERROR: Run as root using sudo."
    exit 1
fi

# Shared deployment lock.
LOCK_FILE="/run/lock/loginexample-deploy.lock"

umask 077

# Reuse file descriptor 9 when inherited from deploy.sh.
if [[ -e "/proc/$$/fd/9" ]] &&
   [[ "$(readlink "/proc/$$/fd/9")" == "$LOCK_FILE" ]]; then
    echo "[INFO] Reusing inherited deployment lock."
else
    exec 9>"$LOCK_FILE"
fi

if ! flock -n 9; then
    echo "ERROR: Another deployment is already running."
    exit 1
fi

echo "[OK] Deployment lock acquired."

# Preflight
if [[ ! -f "$RELEASE_DIR/LoginExample.dll" ]]; then
    echo "ERROR: Release DLL does not exist: $RELEASE_DIR"
    exit 1
fi

if [[ ! -d "$APP_DIR" ]]; then
    echo "ERROR: Current application directory is missing."
    exit 1
fi

if [[ -e "$PREVIOUS_DIR" || -L "$PREVIOUS_DIR" ]]; then
    echo "ERROR: This release ID has already been used."
    echo "Previous version directory: $PREVIOUS_DIR"
    exit 1
fi
test -L "$RELEASE_DIR/appsettings.Production.json"
test "$(readlink "$RELEASE_DIR/appsettings.Production.json")" = "$CONFIG"

runuser -u loginexample -- test -r "$RELEASE_DIR/LoginExample.dll"
runuser -u loginexample -- test -r "$RELEASE_DIR/appsettings.Production.json"

systemctl is-active --quiet "$SERVICE"

if [[ "$ACTION" == "--check" ]]; then
    echo "[OK] Deployment preflight passed."
    echo "No deployment changes were made."
    exit 0
fi

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

    # Reconcile deployment state with the actual filesystem.
    # A signal may interrupt execution between mv and flag assignment.
    if [[ -d "$PREVIOUS_DIR" ]]; then
        old_moved=1

        if [[ -d "$APP_DIR" ]]; then
            new_moved=1
        fi
    fi

    systemctl stop "$SERVICE" || true


    # Restore according to actual filesystem state.
    if [[ -d "$PREVIOUS_DIR" ]]; then

        # Preserve the failed release outside the production path.
        if [[ -e "$APP_DIR" || -L "$APP_DIR" ]]; then
            FAILED_DIR="/opt/loginexample-failed-${RELEASE_ID}-$$"

            if [[ -e "$FAILED_DIR" || -L "$FAILED_DIR" ]] ||
               ! mv -T -- "$APP_DIR" "$FAILED_DIR"; then
                echo "CRITICAL: Cannot move failed release."
                exit 1
            fi

            echo "[ROLLBACK] Failed release preserved: $FAILED_DIR"
        fi

        # Move the known previous release back into production.
        if [[ -e "$APP_DIR" || -L "$APP_DIR" ]] ||
           ! mv -T -- "$PREVIOUS_DIR" "$APP_DIR"; then
            echo "CRITICAL: Cannot restore previous release."
            exit 1
        fi

        echo "[ROLLBACK] Previous release directory restored."
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
