#!/usr/bin/env bash
set -Eeuo pipefail

MODE="${1:---check}"

if [[ "$MODE" != "--check" && "$MODE" != "--download" ]]; then
    echo "Usage: check-release.sh [--check|--download]" >&2
    exit 2
fi

REPO="0x56iceroy/LoginExample"
WORKFLOW="ci.yml"
BRANCH="master"

# Prevent concurrent release checks and downloads.
umask 077

LOCK_DIR="$HOME/.local/state/loginexample"
LOCK_FILE="$LOCK_DIR/check-release.lock"

mkdir -p -- "$LOCK_DIR"

exec 8>"$LOCK_FILE"

if ! flock -n 8; then
    echo "[SKIP] Another release check is already running."
    exit 0
fi

echo "[OK] Release check lock acquired."

# Load GitHub credentials provided by systemd when GH_TOKEN is unset.
if [[ -z "${GH_TOKEN:-}" && -n "${CREDENTIALS_DIRECTORY:-}" ]]; then
    CREDENTIAL_FILE="${CREDENTIALS_DIRECTORY}/github-token"

    if [[ ! -f "$CREDENTIAL_FILE" ||
          ! -s "$CREDENTIAL_FILE" ||
          ! -r "$CREDENTIAL_FILE" ||
          -L "$CREDENTIAL_FILE" ]]; then
        echo "ERROR: GitHub credential file is unavailable." >&2
        exit 2
    fi

    GH_TOKEN="$(< "$CREDENTIAL_FILE")"
    export GH_TOKEN
fi

# Require authentication without storing credentials in the script.
if [[ -z "${GH_TOKEN:-}" ]]; then
    echo "ERROR: GH_TOKEN is not configured." >&2
    exit 2
fi

echo "Checking latest GitHub Actions run..."

RUN_INFO=$(
    GH_PAGER=cat gh run list \
        --repo "$REPO" \
        --workflow "$WORKFLOW" \
        --branch "$BRANCH" \
        --event push \
        --limit 1 \
        --json databaseId,headSha,status,conclusion \
        --jq '.[] | [.databaseId, .headSha, .status, (.conclusion // "")] | join("|")'
)

if [[ -z "$RUN_INFO" ]]; then
    echo "ERROR: No workflow runs found." >&2
    exit 1
fi

IFS='|' read -r RUN_ID COMMIT_SHA STATUS CONCLUSION <<< "$RUN_INFO"

echo "Run ID: $RUN_ID"
echo "Commit SHA: $COMMIT_SHA"
echo "Status: $STATUS"
echo "Conclusion: $CONCLUSION"

# Never fall back to an older successful run.
if [[ "$STATUS" != "completed" || "$CONCLUSION" != "success" ]]; then
    echo "[SKIP] Latest CI run is not successful."
    exit 0
fi

echo "[OK] Latest CI run completed successfully."
echo "Candidate release: gh-${COMMIT_SHA:0:7}"
if [[ "$MODE" == "--check" ]]; then
    echo "No files downloaded. Production unchanged."
    exit 0
fi

# Validate the Git commit identifier.
if [[ ! "$COMMIT_SHA" =~ ^[a-fA-F0-9]{40}$ ]]; then
    echo "ERROR: Invalid Commit SHA." >&2
    exit 1
fi

ARTIFACT_NAME="loginexample-linux-${COMMIT_SHA}"
BASE_DIR="$HOME/apps/LoginExample-ci-artifacts"
DEST_DIR="$BASE_DIR/$COMMIT_SHA"

echo "Checking artifact: $ARTIFACT_NAME"

ARTIFACT_INFO=$(
    gh api \
        "repos/$REPO/actions/runs/$RUN_ID/artifacts" \
        --jq ".artifacts[] |
            select(.name == \"$ARTIFACT_NAME\") |
            [.expired, .size_in_bytes] | @tsv"
)

if [[ -z "$ARTIFACT_INFO" ]]; then
    echo "ERROR: Matching artifact not found." >&2
    exit 1
fi

IFS=$'\t' read -r EXPIRED SIZE <<< "$ARTIFACT_INFO"

if [[ "$EXPIRED" != "false" ||
      ! "$SIZE" =~ ^[0-9]+$ ||
      "$SIZE" -le 0 ]]; then
    echo "ERROR: Artifact expired or invalid." >&2
    exit 1
fi

# Never overwrite an existing staging directory.
# Verify existing staging before skipping download.
if [[ -e "$DEST_DIR" || -L "$DEST_DIR" ]]; then

    if [[ ! -d "$DEST_DIR" || -L "$DEST_DIR" ]]; then
        echo "ERROR: Invalid staging directory: $DEST_DIR" >&2
        exit 1
    fi

    for file in \
        LoginExample.dll \
        LoginExample.deps.json \
        LoginExample.runtimeconfig.json
    do
        if [[ ! -f "$DEST_DIR/$file" ||
              ! -s "$DEST_DIR/$file" ||
              -L "$DEST_DIR/$file" ]]; then
            echo "ERROR: Missing or invalid staging file: $file" >&2
            exit 1
        fi
    done

    for environment in Development Testing Production; do
        config="$DEST_DIR/appsettings.$environment.json"

        if [[ -e "$config" || -L "$config" ]]; then
            echo "ERROR: Unexpected configuration file: $config" >&2
            exit 1
        fi
    done

    echo "[OK] Existing staging verified."
    echo "[SKIP] Artifact already downloaded: $DEST_DIR"
    exit 0
fi

mkdir -p "$BASE_DIR"

# Download into a temporary directory first.
TMP_DIR=$(mktemp -d "$BASE_DIR/.download.XXXXXXXX")

trap 'rm -rf -- "$TMP_DIR"' EXIT

echo "Downloading verified CI artifact..."

gh run download "$RUN_ID" \
    --repo "$REPO" \
    --name "$ARTIFACT_NAME" \
    --dir "$TMP_DIR"

# Verify required application files.
for file in \
    LoginExample.dll \
    LoginExample.deps.json \
    LoginExample.runtimeconfig.json
do
    if [[ ! -s "$TMP_DIR/$file" ]]; then
        echo "ERROR: Missing or empty file: $file" >&2
        exit 1
    fi
done

# Never include environment-specific configuration.
for environment in Development Testing Production; do
    if [[ -e "$TMP_DIR/appsettings.$environment.json" ]]; then
        echo "ERROR: Unexpected configuration file." >&2
        exit 1
    fi
done

# Publish the completed download into staging.
mv -T -- "$TMP_DIR" "$DEST_DIR"
trap - EXIT

echo "[OK] Artifact downloaded and verified."
echo "Staging: $DEST_DIR"
echo "Production unchanged."