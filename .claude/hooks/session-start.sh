#!/bin/bash
set -euo pipefail

# Founder's Lands — SessionStart hook for Claude Code on the web.
# Warms up the engine-free .NET simulation harness (NuGet restore + Release build) so the
# 117-test suite and the headless scenario CLI run immediately, with no first-run wait.
#
# Remote (web) sessions only: locally you already have your own toolchain set up.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cd "${CLAUDE_PROJECT_DIR:-.}"

SOLUTION="SimHarness/FoundersLands.sln"

echo "[session-start] Restoring and building the Founder's Lands simulation core…"
dotnet restore "$SOLUTION" --verbosity quiet
dotnet build "$SOLUTION" -c Release --no-restore --verbosity quiet
echo "[session-start] Ready — dotnet $(dotnet --version); run: dotnet test $SOLUTION -c Release"
