#!/usr/bin/env bash

set -euo pipefail

# -----------------------------------------------------------------------------
# Deployment configuration
# -----------------------------------------------------------------------------
# Override any of these values from the environment when invoking the script:
#   DOCKER_REPO=yourname/yourrepo DEV_SSH_HOST=dev.example.com ./deploy.sh dev

PROJECT_NAME="${PROJECT_NAME:-wrapsfer-api}"
DOCKER_REPO="${DOCKER_REPO:-chownrmrf/wrapsfer-api}"

DEV_SSH_HOST="${DEV_SSH_HOST:-wrapsfer}"
PROD_SSH_HOST="${PROD_SSH_HOST:-wrapsfer}"
SSH_USER="${SSH_USER:-}"

# Leave blank to use the default SSH agent/config. Set to a private key path if
# each environment needs a specific key.
DEV_SSH_KEY="${DEV_SSH_KEY:-}"
PROD_SSH_KEY="${PROD_SSH_KEY:-}"

DEV_HOST_PORT="${DEV_HOST_PORT:-8081}"
PROD_HOST_PORT="${PROD_HOST_PORT:-8080}"
CONTAINER_PORT="${CONTAINER_PORT:-8080}"

DOCKERFILE="${DOCKERFILE:-Dockerfile}"
BUILD_CONTEXT="${BUILD_CONTEXT:-.}"

usage() {
    cat <<EOF
Usage: $0 <dev|prod>

Required environment variables:
  DOCKER_USERNAME      Docker Hub username
  DOCKER_PASSWORD      Docker Hub password or access token

Optional configuration overrides:
  PROJECT_NAME         Container/project name (default: ${PROJECT_NAME})
  DOCKER_REPO          Docker Hub repo, e.g. username/repo (default: ${DOCKER_REPO})
  DEV_SSH_HOST         Dev SSH host or SSH config alias (default: ${DEV_SSH_HOST})
  PROD_SSH_HOST        Prod SSH host or SSH config alias (default: ${PROD_SSH_HOST})
  SSH_USER             SSH username; leave blank when SSH host alias includes it
  DEV_SSH_KEY          Dev private key path; leave blank to use SSH config/agent
  PROD_SSH_KEY         Prod private key path; leave blank to use SSH config/agent
  DEV_HOST_PORT        Dev host port (default: ${DEV_HOST_PORT})
  PROD_HOST_PORT       Prod host port (default: ${PROD_HOST_PORT})
  CONTAINER_PORT       Container port (default: ${CONTAINER_PORT})
EOF
}

log() {
    printf '\n==> %s\n' "$1"
}

die() {
    printf 'ERROR: %s\n' "$1" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || die "Required command not found: $1"
}

require_not_placeholder() {
    local name="$1"
    local value="$2"

    if [[ -z "$value" || "$value" == *"[Insert "* ]]; then
        die "$name is not configured. Set it at the top of this script or export it before running."
    fi
}

build_ssh_target() {
    local host="$1"

    if [[ -n "$SSH_USER" ]]; then
        printf '%s@%s' "$SSH_USER" "$host"
    else
        printf '%s' "$host"
    fi
}

validate_ssh_key() {
    local key_path="$1"

    if [[ -n "$key_path" && ! -r "$key_path" ]]; then
        die "SSH key is not readable: $key_path"
    fi
}

ENVIRONMENT="${1:-}"

log "Validating arguments"
case "$ENVIRONMENT" in
    dev|prod)
        ;;
    *)
        usage
        exit 1
        ;;
esac

log "Checking local dependencies"
require_command docker
require_command git
require_command ssh

log "Checking required secrets"
[[ -n "${DOCKER_USERNAME:-}" ]] || die "DOCKER_USERNAME is not set"
[[ -n "${DOCKER_PASSWORD:-}" ]] || die "DOCKER_PASSWORD is not set"

require_not_placeholder "PROJECT_NAME" "$PROJECT_NAME"
require_not_placeholder "DOCKER_REPO" "$DOCKER_REPO"
[[ -f "$DOCKERFILE" ]] || die "Dockerfile not found: $DOCKERFILE"
[[ -d "$BUILD_CONTEXT" ]] || die "Build context directory not found: $BUILD_CONTEXT"

GIT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
GIT_SHA="$(git rev-parse HEAD)"
IMAGE_LATEST="${DOCKER_REPO}:latest"
IMAGE_SHA="${DOCKER_REPO}:${GIT_SHA}"

if [[ "$ENVIRONMENT" == "prod" ]]; then
    log "Enforcing production branch binding"
    if [[ "$GIT_BRANCH" != "main" && "$GIT_BRANCH" != "master" ]]; then
        die "Production deployments must run from main or master. Current branch: $GIT_BRANCH"
    fi

    printf '\033[1;31m\nWARNING: You are about to deploy to PRODUCTION.\nType "prod" to continue: \033[0m'
    if ! read -r confirmation; then
        die "Production deployment confirmation could not be read"
    fi

    if [[ "$confirmation" != "prod" ]]; then
        die "Production deployment aborted"
    fi
fi

case "$ENVIRONMENT" in
    dev)
        SSH_HOST="$DEV_SSH_HOST"
        SSH_KEY="$DEV_SSH_KEY"
        HOST_PORT="$DEV_HOST_PORT"
        ;;
    prod)
        SSH_HOST="$PROD_SSH_HOST"
        SSH_KEY="$PROD_SSH_KEY"
        HOST_PORT="$PROD_HOST_PORT"
        ;;
esac

require_not_placeholder "SSH_HOST" "$SSH_HOST"
validate_ssh_key "$SSH_KEY"

SSH_TARGET="$(build_ssh_target "$SSH_HOST")"
CONTAINER_NAME="${PROJECT_NAME}-${ENVIRONMENT}"

SSH_OPTS=(
    -o BatchMode=yes
)

if [[ -n "$SSH_KEY" ]]; then
    SSH_OPTS+=(-o IdentitiesOnly=yes -i "$SSH_KEY")
fi

log "Authenticating with Docker Hub"
printf '%s\n' "$DOCKER_PASSWORD" | docker login \
    --username "$DOCKER_USERNAME" \
    --password-stdin

log "Building Docker image"
echo "Repository: $DOCKER_REPO"
echo "Git branch: $GIT_BRANCH"
echo "Git SHA:    $GIT_SHA"
docker build \
    --pull \
    --file "$DOCKERFILE" \
    --tag "$IMAGE_SHA" \
    --tag "$IMAGE_LATEST" \
    "$BUILD_CONTEXT"

log "Pushing Docker image tags"
docker push "$IMAGE_SHA"
docker push "$IMAGE_LATEST"

log "Deploying to ${ENVIRONMENT} via SSH"
echo "SSH target:      $SSH_TARGET"
echo "Container name:  $CONTAINER_NAME"
echo "Image to run:    $IMAGE_SHA"
echo "Port mapping:    ${HOST_PORT}:${CONTAINER_PORT}"

printf -v REMOTE_CMD \
    'IMAGE_SHA=%q IMAGE_LATEST=%q CONTAINER_NAME=%q HOST_PORT=%q CONTAINER_PORT=%q bash -se' \
    "$IMAGE_SHA" "$IMAGE_LATEST" "$CONTAINER_NAME" "$HOST_PORT" "$CONTAINER_PORT"

ssh "${SSH_OPTS[@]}" "$SSH_TARGET" "$REMOTE_CMD" <<'REMOTE_SCRIPT'
set -euo pipefail

echo "Pulling latest tag from Docker Hub..."
docker pull "$IMAGE_LATEST"

echo "Pulling immutable commit tag from Docker Hub..."
docker pull "$IMAGE_SHA"

echo "Stopping existing container if present..."
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

echo "Starting new container..."
docker run -d \
    --name "$CONTAINER_NAME" \
    --restart always \
    -p "${HOST_PORT}:${CONTAINER_PORT}" \
    "$IMAGE_SHA"

echo "Deployment status:"
docker ps \
    --filter "name=^/${CONTAINER_NAME}$" \
    --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}'
REMOTE_SCRIPT

log "Deployment complete"
