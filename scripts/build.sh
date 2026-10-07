#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
export PATH="$root/.tools/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"${ASSET_PYTHON:-$root/.tools/assets-env/bin/python}" "$root/scripts/validate-assets.py"
tml="${TML_PATH:-$root/.tools/tModLoader}"
dotnet run --project "$root/checks/CoreChecks.csproj"
dotnet build "$root/Riakawa/Riakawa.csproj" -c Release -p:TModLoaderDirectory="$tml" --nologo
cd "$tml"
dotnet tModLoader.dll -server -nosteam -build "$root/Riakawa" \
  -eac "$root/Riakawa/bin/Release/net8.0/Riakawa.dll" -tmlsavedirectory "$root/.tools/tml-save"
mkdir -p "$root/dist"
cp "$root/.tools/tml-save/Mods/Riakawa.tmod" "$root/dist/Riakawa.tmod"
