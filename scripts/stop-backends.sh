#!/usr/bin/env bash
# Stops the processes started by start-backends.sh. --all also removes the Aspire dashboard container.
set -euo pipefail
cd "$(dirname "$0")/.."

for backend in "SmartHomeMcp 5101" "VetAgent 5201" "HumanTrackerAgent 5202"; do
  read -r name port <<<"$backend"
  # dotnet run (stored PID) plus the app it launched (listening on the port)
  pid="$(cat ".logs/$name.pid" 2>/dev/null || true)"
  ps -p "${pid:-0}" -o args= 2>/dev/null | grep -q "dotnet run" || pid=""   # stale PID file: never kill a stranger
  pids="$pid $(lsof -ti ":$port" -sTCP:LISTEN || true)"
  if [[ -n "${pids// /}" ]]; then
    kill $pids 2>/dev/null || true
    echo "$name stopped"
  fi
  rm -f ".logs/$name.pid"
done

if [[ "${1:-}" == "--all" ]]; then
  if docker rm -f aspire-dashboard >/dev/null 2>&1; then echo "Aspire dashboard removed"; fi
fi
