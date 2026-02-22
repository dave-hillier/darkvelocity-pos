#!/usr/bin/env bash
#
# DarkVelocity POS - Ensure Background Service Dependencies
#
# Checks whether Docker infrastructure services are already running and healthy.
# Only starts services that aren't already up. Designed to be fast when everything
# is already running (exits in < 1 second).
#
# If a required port is already in use (e.g., another Azurite instance), the
# existing service is reused rather than starting a new container.
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
# Service definitions: map container names to compose services and check ports
# -----------------------------------------------------------------------------

CONTAINERS=("darkvelocity-azurite" "darkvelocity-postgres" "darkvelocity-spicedb")

service_for_container() {
    case "$1" in
        darkvelocity-azurite)  echo "azurite" ;;
        darkvelocity-postgres) echo "postgres" ;;
        darkvelocity-spicedb)  echo "spicedb" ;;
    esac
}

port_for_container() {
    case "$1" in
        darkvelocity-azurite)  echo 10002 ;;
        darkvelocity-postgres) echo 5432 ;;
        darkvelocity-spicedb)  echo 50051 ;;
    esac
}

# -----------------------------------------------------------------------------
# Health checks for individual services
# -----------------------------------------------------------------------------

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

port_is_reachable() {
    local port="$1"
    nc -z localhost "$port" &>/dev/null
}

all_services_healthy() {
    for c in "${CONTAINERS[@]}"; do
        container_is_healthy "$c" || return 1
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
        if nc -z localhost 5432 &>/dev/null; then
            return 0
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    return 1
}

wait_for_managed_healthy() {
    local skip_containers=("$@")
    log_info "Waiting for managed services to become healthy..."
    local max_wait=90
    local elapsed=0
    while [[ $elapsed -lt $max_wait ]]; do
        local all_healthy=true
        for c in "${CONTAINERS[@]}"; do
            # Skip containers that are externally managed
            local is_skipped=false
            for s in "${skip_containers[@]}"; do
                [[ "$c" == "$s" ]] && is_skipped=true && break
            done
            [[ "$is_skipped" == true ]] && continue

            container_is_healthy "$c" || { all_healthy=false; break; }
        done
        [[ "$all_healthy" == true ]] && return 0
        sleep 3
        elapsed=$((elapsed + 3))
    done
    return 1
}

# -----------------------------------------------------------------------------
# Docker compose helpers
# -----------------------------------------------------------------------------

# Determine the compose command (v2 plugin vs standalone)
compose_cmd() {
    if docker compose version &>/dev/null; then
        echo "docker compose"
    else
        echo "docker-compose"
    fi
}

# On self-hosted CI runners, containers from previous runs may persist but
# lose their compose project labels (e.g., after the compose network is
# removed between jobs). When we selectively start a service like spicedb,
# compose resolves depends_on and tries to CREATE postgres — which fails
# because a container with that name already exists.
#
# Fix: create the compose network, connect existing containers to it (with
# service-name aliases so compose DNS resolution works), then start only
# the requested services with --no-deps.
ensure_compose_network() {
    # Compose project name defaults to directory basename ("docker")
    local network="docker_default"
    docker network create "$network" 2>/dev/null || true

    # Connect already-running containers so new services can reach them
    # by compose service name (e.g. "postgres" not "darkvelocity-postgres")
    for c in "${CONTAINERS[@]}"; do
        if container_is_running "$c"; then
            local svc
            svc=$(service_for_container "$c")
            docker network connect --alias "$svc" "$network" "$c" 2>/dev/null || true
        fi
    done
}

# Start services via docker compose.
# When specific service names are given, uses --no-deps to avoid compose
# trying to recreate already-running dependency containers.
compose_up() {
    local services=("$@")
    local cmd
    cmd=$(compose_cmd)
    cd "$DOCKER_DIR"
    if [[ ${#services[@]} -gt 0 ]]; then
        ensure_compose_network
        $cmd up -d --no-deps "${services[@]}"
    else
        $cmd up -d
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

    # Check what's missing, collect only the compose services that need starting
    local services_to_start=()
    local external_services=()
    for c in "${CONTAINERS[@]}"; do
        local port
        port=$(port_for_container "$c")
        local service
        service=$(service_for_container "$c")
        if container_is_healthy "$c"; then
            log_ok "$c is healthy"
        elif container_is_running "$c"; then
            log_warn "$c is running but not yet healthy"
        elif port_is_reachable "$port"; then
            log_ok "$service is already available on port $port (reusing existing service)"
            external_services+=("$c")
        else
            log_info "$c is not running"
            services_to_start+=("$service")
        fi
    done

    if [[ ${#services_to_start[@]} -gt 0 ]]; then
        # Include spicedb-migrate when spicedb needs starting
        for s in "${services_to_start[@]}"; do
            if [[ "$s" == "spicedb" ]]; then
                services_to_start+=("spicedb-migrate")
                break
            fi
        done
        log_info "Starting services: ${services_to_start[*]}..."
        compose_up "${services_to_start[@]}"
    fi

    # Wait for readiness (port-based checks work for both managed and external services)
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

    # Wait for managed containers to report healthy (skip external services)
    if ! wait_for_managed_healthy "${external_services[@]}"; then
        log_warn "Some services did not reach healthy state, but core services (Azurite, PostgreSQL) are ready"
    else
        log_ok "All background services are running and healthy"
    fi
}

main
