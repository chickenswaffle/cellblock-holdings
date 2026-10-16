#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
action="${1:-status}"
lock_root="$project_root/.collaboration/locks"
mkdir -p "$lock_root"
if [[ "$action" == status ]]; then
  for lock in "$lock_root"/*; do
    [[ -d "$lock" ]] || continue
    printf '%s: ' "${lock##*/}"
    cat "$lock/owner" 2>/dev/null || true
  done
  exit 0
fi
agent="${2:-}"; scope="${3:-}"
if [[ ! "$agent" =~ ^[a-zA-Z0-9_-]+$ || ! "$scope" =~ ^[a-zA-Z0-9_-]+$ ]]; then
  echo 'Usage: agent-coordination.sh status | claim|check|release <agent> <scope>' >&2; exit 2
fi
lock="$lock_root/$scope"
case "$action" in
  claim)
    if mkdir "$lock" 2>/dev/null; then
      printf '%s\n' "$agent" > "$lock/owner"
      date -u '+%Y-%m-%dT%H:%M:%SZ' > "$lock/claimed-at"
      printf 'Claimed %s for %s\n' "$scope" "$agent"
    elif [[ "$(cat "$lock/owner" 2>/dev/null || true)" == "$agent" ]]; then
      printf '%s already owns %s\n' "$agent" "$scope"
    else
      printf 'BLOCKED: %s belongs to %s. Read COORDINATION.md; do not override the lock.\n' "$scope" "$(cat "$lock/owner" 2>/dev/null || echo pending-owner)" >&2; exit 1
    fi ;;
  check)
    [[ "$(cat "$lock/owner" 2>/dev/null || true)" == "$agent" ]] || { echo "BLOCKED: $agent does not own $scope" >&2; exit 1; } ;;
  release)
    [[ "$(cat "$lock/owner" 2>/dev/null || true)" == "$agent" ]] || { echo 'BLOCKED: cannot release another agent’s lock' >&2; exit 1; }
    rm "$lock/owner" "$lock/claimed-at"; rmdir "$lock"
    printf 'Released %s\n' "$scope" ;;
  *) echo 'Unknown action' >&2; exit 2 ;;
esac
