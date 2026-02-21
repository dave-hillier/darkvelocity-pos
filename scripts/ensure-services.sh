#!/usr/bin/env bash
#
# DarkVelocity POS - Ensure Background Service Dependencies
#
# Checks whether Docker infrastructure services are already running and healthy.
# Only starts services that aren't already up. Designed to be fast when everything
# is already running (exits in < 1 second).
#
# Usage:
#   ./scripts/ensure-services.sh           # Check and start if needed
#   ./scripts/ensure-services.sh --quiet   # Suppress output when all healthy
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DOCKER_DIR="$PROJECT_ROOT/docker"

QUIET=false
[[ "${1:-}" == "--quiet" ]] && QUIET=true

# Timeouts for waiting (shorter than setup.sh since containers may already be warm)
AZURITE_WAIT_TIMEOUT=30
POSTGRES_WAIT_TIMEOUT=60

log() {
    [[ "$QUIET" == true ]] && return
    echo -e "$1"
}

log_ok()   { log "${GREEN}[OK]${NC} $1"; }
log_info() { log "${BLUE}[INFO]${NC} $1"; }
log_warn() { log "${YELLOW}[WARN]${NC} $1"; }
log_err()  { echo -e "${RED}[ERROR]${NC} $1" >&2; }

# -----------------------------------------------------------------------------
# Health checks for individual services
# -----------------------------------------------------------------------------

CONTAINERS=("darkvelocity-azurite" "darkvelocity-postgres" "darkvelocity-spicedb")

container_is_healthy() {
    local name="$1"
    local status
    status=$(docker inspect --format='{{.State.Health.Status}}' "$name" 2>/dev/null) || return 1
    [[ "$status" == "healthy" ]]
}

container_is_running() {
    local name="$1"
    local state
    state=$(docker inspect --format='{{.State.Status}}' "$name" 2>/dev/null) || return 1
    [[ "$state" == "running" ]]
}

all_services_healthy() {
    for c in "${CONTAINERS[@]}"; do
        container_is_healthy "$c" || return 1
    done
    return 0
}

all_services_running() {
    for c in "${CONTAINERS[@]}"; do
        container_is_running "$c" || return 1
    done
    return 0
}

# -----------------------------------------------------------------------------
# Wait helpers
# -----------------------------------------------------------------------------

wait_for_azurite() {
    local elapsed=0
    while [[ $elapsed -lt $AZURITE_WAIT_TIMEOUT ]]; do
        if nc -z localhost 10002 &>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    return 1
}

wait_for_postgres() {
    local elapsed=0
    while [[ $elapsed -lt $POSTGRES_WAIT_TIMEOUT ]]; do
        if docker exec darkvelocity-postgres pg_isready -U darkvelocity &>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    return 1
}

wait_for_all_healthy() {
    log_info "Waiting for services to become healthy..."
    local max_wait=90
    local elapsed=0
    while [[ $elapsed -lt $max_wait ]]; do
        if all_services_healthy; then
            return 0
        fi
        sleep 3
        elapsed=$((elapsed + 3))
    done
    return 1
}

# -----------------------------------------------------------------------------
# Docker compose helper (supports new and legacy)
# -----------------------------------------------------------------------------

compose_up() {
    cd "$DOCKER_DIR"
    if docker compose version &>/dev/null; then
        docker compose up -d
    else
        docker-compose up -d
    fi
    cd "$PROJECT_ROOT"
}

# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------

main() {
    # Check Docker daemon is running
    if ! docker info &>/dev/null; then
        log_err "Docker daemon is not running."
        exit 1
    fi

    # Fast path: everything already healthy
    if all_services_healthy; then
        log_ok "All background services already running and healthy"
        return 0
    fi

    # Check what's missing
    local needs_start=false
    for c in "${CONTAINERS[@]}"; do
        if container_is_healthy "$c"; then
            log_ok "$c is healthy"
        elif container_is_running "$c"; then
            log_warn "$c is running but not yet healthy"
        else
            log_info "$c is not running"
            needs_start=true
        fi
    done

    if [[ "$needs_start" == true ]]; then
        log_info "Starting Docker infrastructure..."
        compose_up
    else
        log_info "All containers are running, waiting for health checks..."
    fi

    # Wait for readiness
    if ! wait_for_azurite; then
        log_err "Azurite did not become ready within ${AZURITE_WAIT_TIMEOUT}s"
        exit 1
    fi
    log_ok "Azurite is ready"

    if ! wait_for_postgres; then
        log_err "PostgreSQL did not become ready within ${POSTGRES_WAIT_TIMEOUT}s"
        exit 1
    fi
    log_ok "PostgreSQL is ready"

    # Wait for all containers (including SpiceDB) to report healthy
    if ! wait_for_all_healthy; then
        log_warn "Some services did not reach healthy state, but core services (Azurite, PostgreSQL) are ready"
    else
        log_ok "All background services are running and healthy"
    fi
}

main
