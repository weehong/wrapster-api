#!/usr/bin/env bash
# Idempotently ensure the Wrapsfer Keycloak admin service-account client
# exists in the master realm with the correct secret and role assignment.
# Designed to run as a one-shot compose service against a freshly started
# Keycloak. Re-running on an already-bootstrapped realm is a no-op.
set -euo pipefail

: "${KC_BASE_URL:?KC_BASE_URL is required (e.g. http://keycloak:8080)}"
: "${KEYCLOAK_ADMIN_USER:?KEYCLOAK_ADMIN_USER is required}"
: "${KEYCLOAK_ADMIN_PASSWORD:?KEYCLOAK_ADMIN_PASSWORD is required}"
: "${KEYCLOAK_ADMIN_CLIENT_ID:?KEYCLOAK_ADMIN_CLIENT_ID is required}"
: "${KEYCLOAK_ADMIN_CLIENT_SECRET:?KEYCLOAK_ADMIN_CLIENT_SECRET is required}"

KCADM=/opt/keycloak/bin/kcadm.sh
READINESS_TIMEOUT_SECONDS="${READINESS_TIMEOUT_SECONDS:-180}"

log() { printf '[bootstrap-admin-client] %s\n' "$*"; }

log "Waiting for Keycloak at ${KC_BASE_URL} (timeout ${READINESS_TIMEOUT_SECONDS}s)"
deadline=$((SECONDS + READINESS_TIMEOUT_SECONDS))
while :; do
    if "${KCADM}" config credentials \
            --server "${KC_BASE_URL}" \
            --realm master \
            --user "${KEYCLOAK_ADMIN_USER}" \
            --password "${KEYCLOAK_ADMIN_PASSWORD}" >/dev/null 2>&1; then
        log "Admin login succeeded"
        break
    fi
    if (( SECONDS >= deadline )); then
        log "ERROR: timed out waiting for Keycloak admin login"
        exit 1
    fi
    sleep 3
done

# Look up existing client uuid (empty string if not present).
client_uuid="$("${KCADM}" get clients -r master \
    -q "clientId=${KEYCLOAK_ADMIN_CLIENT_ID}" \
    --fields id --format csv --noquotes 2>/dev/null \
    | tr -d '\r' | head -n 1 || true)"

if [[ -z "${client_uuid}" ]]; then
    log "Creating client '${KEYCLOAK_ADMIN_CLIENT_ID}' in master realm"
    client_uuid="$("${KCADM}" create clients -r master \
        -s "clientId=${KEYCLOAK_ADMIN_CLIENT_ID}" \
        -s protocol=openid-connect \
        -s publicClient=false \
        -s serviceAccountsEnabled=true \
        -s standardFlowEnabled=false \
        -s directAccessGrantsEnabled=false \
        -s implicitFlowEnabled=false \
        -s "secret=${KEYCLOAK_ADMIN_CLIENT_SECRET}" \
        -i)"
    log "Created client uuid=${client_uuid}"
else
    log "Client '${KEYCLOAK_ADMIN_CLIENT_ID}' already exists (uuid=${client_uuid}); reconciling"
fi

# Reconcile flags + secret on every run so config and secret drift heal automatically.
"${KCADM}" update "clients/${client_uuid}" -r master \
    -s protocol=openid-connect \
    -s publicClient=false \
    -s serviceAccountsEnabled=true \
    -s standardFlowEnabled=false \
    -s directAccessGrantsEnabled=false \
    -s implicitFlowEnabled=false \
    -s "secret=${KEYCLOAK_ADMIN_CLIENT_SECRET}" >/dev/null

# Keycloak names the service-account user `service-account-<clientId-lowercased>`.
service_account_user="service-account-$(printf '%s' "${KEYCLOAK_ADMIN_CLIENT_ID}" | tr '[:upper:]' '[:lower:]')"

# Assign the master realm's `admin` realm-level role to the service account.
# This is Keycloak's built-in super-admin role; it grants the privileges
# required to create new realms and manage clients/users inside them.
# The role-mappings endpoint is idempotent — re-assigning an already-mapped
# role returns 204 with no error.
log "Ensuring master 'admin' realm role is assigned to ${service_account_user}"
"${KCADM}" add-roles -r master \
    --uusername "${service_account_user}" \
    --rolename admin >/dev/null

log "Done"
