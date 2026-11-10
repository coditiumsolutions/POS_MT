#!/usr/bin/env bash
# Deploy published ASP.NET Core output to TezHost / Plesk Windows via FTPS (explicit TLS).
set -euo pipefail

: "${FTP_SERVER:?FTP_SERVER is required}"
: "${FTP_USERNAME:?FTP_USERNAME is required}"
: "${FTP_PASSWORD:?FTP_PASSWORD is required}"
: "${FTP_REMOTE_DIR:?FTP_REMOTE_DIR is required}"
: "${LOCAL_DIR:?LOCAL_DIR is required}"

PROTOCOL="${FTP_PROTOCOL:-ftps}"
PORT="${FTP_PORT:-21}"
VERIFY_CERT="${FTP_VERIFY_CERT:-no}"
OFFLINE_FILE="${OFFLINE_FILE:-deploy/app_offline.htm}"

if [[ ! -d "$LOCAL_DIR" ]]; then
  echo "Local publish directory not found: $LOCAL_DIR" >&2
  exit 1
fi

if [[ ! -f "$OFFLINE_FILE" ]]; then
  echo "Offline file not found: $OFFLINE_FILE" >&2
  exit 1
fi

if ! command -v lftp >/dev/null 2>&1; then
  sudo apt-get update
  sudo apt-get install -y lftp
fi

# Normalize remote dir to always end with /
REMOTE_DIR="${FTP_REMOTE_DIR%/}/"

lftp_cmd() {
  lftp -c "
    set cmd:fail-exit true;
    set ftp:ssl-force true;
    set ftp:ssl-protect-data true;
    set ssl:verify-certificate ${VERIFY_CERT};
    set net:max-retries 3;
    set net:timeout 30;
    open -u \"${FTP_USERNAME}\",\"${FTP_PASSWORD}\" -p ${PORT} ${PROTOCOL}://${FTP_SERVER};
    $*
  "
}

echo "==> Taking site offline (${REMOTE_DIR}app_offline.htm)"
lftp_cmd "cd ${REMOTE_DIR}; put -O ${REMOTE_DIR} ${OFFLINE_FILE} -o app_offline.htm"
sleep 5

echo "==> Uploading publish output from ${LOCAL_DIR} to ${REMOTE_DIR}"
# Mirror local publish into remote web root. Keep App_Data on server.
lftp_cmd "
  cd ${REMOTE_DIR};
  mirror -R --verbose --parallel=4 \
    --exclude-glob app_offline.htm \
    --exclude-glob App_Data/ \
    --exclude-glob App_Data/** \
    --exclude-glob *.log \
    ${LOCAL_DIR}/ ${REMOTE_DIR};
"

echo "==> Bringing site online (remove app_offline.htm)"
lftp_cmd "cd ${REMOTE_DIR}; rm -f app_offline.htm" || true

echo "==> Deploy complete"
