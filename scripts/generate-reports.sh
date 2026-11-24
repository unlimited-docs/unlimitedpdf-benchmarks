#!/usr/bin/env bash
# Regenerates report.html, summary.md, results.csv and results.json from the raw data of run folders.
#   bash scripts/generate-reports.sh                          # every folder under results/
#   bash scripts/generate-reports.sh results/20261124-220000-full
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
folders=("$@")
if [[ ${#folders[@]} -eq 0 ]]; then
  for d in results/*/; do [[ -f "$d/manifest.json" ]] && folders+=("${d%/}"); done
fi
[[ ${#folders[@]} -eq 0 ]] && { echo "No run folders found under results/." >&2; exit 1; }
dotnet build src/UnlimitedPDF.Benchmarks.Reporting -c Release
for folder in "${folders[@]}"; do
  dotnet run --project src/UnlimitedPDF.Benchmarks.Reporting -c Release --no-build -- "$folder"
done
