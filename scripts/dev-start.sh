#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/apps/signaling-server"

if [ ! -f .env ]; then
  cp .env.example .env
fi

pnpm install
SIGNALING_TOKEN="${SIGNALING_TOKEN:-dev-token}" pnpm dev

