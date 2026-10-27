#!/usr/bin/env bash
# Builds the plugin and packs it as artifacts/kyuplan-plugin-example.kyuplugin (any folder as the first argument).
# A package is a zip with plugin.json, the assembly and the JSON files in its root.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
output="$(mkdir -p "${1:-$root/artifacts}" && cd "${1:-$root/artifacts}" && pwd)"
package="$output/kyuplan-plugin-example.kyuplugin"

rm -rf "$output/publish" "$package"
dotnet publish "$root/src/Kyuplan.Plugins.Example" -c Release -o "$output/publish"
(cd "$output/publish" && zip -qr "$package" .)
echo "Packed $package"
