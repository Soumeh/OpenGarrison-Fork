#!/bin/bash
set -e

RID="${1:-linux-x64}"
OUT="./dist/$RID"
FLAGS="-c Release -r $RID --self-contained false -o $OUT"

PROJECTS=(
  "OpenGarrison.Client/OpenGarrison.Client.csproj"
  "OpenGarrison.Server/OpenGarrison.Server.csproj"
  "OpenGarrison.ServerLauncher/OpenGarrison.ServerLauncher.csproj"
)

for proj in "${PROJECTS[@]}"; do
  dotnet publish "$proj" $FLAGS
done
