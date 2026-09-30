#!/usr/bin/env bash
# Starts the Aspire dashboard (Docker), the smart-home MCP server and both A2A agents in the background.
# Logs and PIDs go to .logs/. The 08 host itself is started by hand: dotnet run --project src/08-AgUi
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .logs

# name  project  port  url
backends=(
  "SmartHomeMcp      side/SmartHomeMcp      5101 http://localhost:5101/mcp"
  "VetAgent          side/VetAgent          5201 http://localhost:5201/.well-known/agent-card.json"
  "HumanTrackerAgent side/HumanTrackerAgent 5202 http://localhost:5202/.well-known/agent-card.json"
)

if [[ -z "$(docker ps -q -f name=^aspire-dashboard$)" ]]; then
  echo "Starting Aspire dashboard ..."
  docker start aspire-dashboard >/dev/null 2>&1 ||
    docker run -d --name aspire-dashboard -p 18888:18888 -p 4317:18889 -p 4318:18890 \
      -e ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:13.5.2 >/dev/null
fi

# Build once up front, so the three processes do not race for the same obj/ folders
for backend in "${backends[@]}"; do
  read -r _ project _ _ <<<"$backend"
  dotnet build "$project" --nologo -v q -clp:NoSummary --disable-build-servers >/dev/null || { echo "Build failed: $project" >&2; exit 1; }
done

for backend in "${backends[@]}"; do
  read -r name project port _ <<<"$backend"
  if lsof -ti ":$port" >/dev/null; then
    echo "$name: port $port already in use, not starting it again"
    continue
  fi
  nohup dotnet run --no-build --project "$project" >".logs/$name.log" 2>&1 &
  echo $! >".logs/$name.pid"
done

for backend in "${backends[@]}"; do
  read -r name _ port url <<<"$backend"
  for _ in {1..60}; do
    curl -s -o /dev/null --max-time 1 "$url" && break
    sleep 1
  done
  curl -s -o /dev/null --max-time 1 "$url" || { echo "$name did not come up on port $port, see .logs/$name.log" >&2; exit 1; }
done

printf '\n%-20s %s\n' "Aspire dashboard" "http://localhost:18888"
for backend in "${backends[@]}"; do
  read -r name _ _ url <<<"$backend"
  printf '%-20s %s\n' "$name" "$url"
done
printf '\nNext: dotnet run --project src/08-AgUi   (then: dotnet run --project side/AgUiConsole)\n'
