#!/usr/bin/env bash
# Builds the suite, validates every case, measures the passing cases, profiles process resources and writes the reports.
#
#   bash scripts/run-benchmarks.sh --profile quick
#   bash scripts/run-benchmarks.sh --profile standard --category Manipulation --library UnlimitedPdf,PdfPig
#   bash scripts/run-benchmarks.sh --profile all              # full, then stress, each in its own run folder
#   bash scripts/run-benchmarks.sh --profile smoke -- --filter '*TableBenchmarks*'
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

profile=standard category="" library="" output="" skip_build=0 skip_bench=0 skip_profiler=0
bdn_args=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile) profile="$2"; shift 2 ;;
    --category) category="$2"; shift 2 ;;
    --library) library="$2"; shift 2 ;;
    --output) output="$2"; shift 2 ;;
    --skip-build) skip_build=1; shift ;;
    --skip-benchmarks) skip_bench=1; shift ;;
    --skip-profiler) skip_profiler=1; shift ;;
    --) shift; bdn_args=("$@"); break ;;
    *) echo "Unknown option $1" >&2; exit 2 ;;
  esac
done

if [[ $skip_build -eq 0 ]]; then
  dotnet restore UnlimitedPDF.Benchmarks.slnx --locked-mode
  dotnet build UnlimitedPDF.Benchmarks.slnx -c Release --no-restore
fi

profiles=("$profile")
[[ "$profile" == "all" ]] && profiles=(full stress)
for p in "${profiles[@]}"; do
  run_id="$(date -u +%Y%m%d-%H%M%S)-$p"
  if [[ -n "$output" && ${#profiles[@]} -eq 1 ]]; then folder="$output"; elif [[ -n "$output" ]]; then folder="$output/$run_id"; else folder="results/$run_id"; fi
  mkdir -p "$folder"
  echo "== Profile $p -> $folder"
  common=(--profile "$p" --output "$folder")
  [[ -n "$category" ]] && common+=(--category "$category")
  [[ -n "$library" ]] && common+=(--library "$library")
  if [[ $skip_bench -eq 0 ]]; then
    dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- validate "${common[@]}"
    dotnet run --project src/UnlimitedPDF.Benchmarks.Runner -c Release --no-build -- bench "${common[@]}" "${bdn_args[@]}"
  fi
  if [[ $skip_profiler -eq 0 ]]; then
    profiler=(--profile "$p" --output "$folder")
    [[ -n "$library" ]] && profiler+=(--library "$library")
    dotnet run --project src/UnlimitedPDF.Benchmarks.ResourceProfiler -c Release --no-build -- "${profiler[@]}"
  fi
  dotnet run --project src/UnlimitedPDF.Benchmarks.Reporting -c Release --no-build -- "$folder"
  echo "Report: $folder/report.html"
done
