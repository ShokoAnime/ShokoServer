#!/usr/bin/env bash
# Runs the text benchmarks (Shoko.Benchmarks/Text) in Release and leaves the reports in a folder outside the repo.
#
# Usage: scripts/text_benchmarks.sh <output-dir> [extra BenchmarkDotNet arguments...]
#   scripts/text_benchmarks.sh /tmp/unitext-bench
#   scripts/text_benchmarks.sh /tmp/unitext-smoke --job Dry --inProcess
#
# The fixture's row counts are written to <output-dir>/fixture.txt first, so rows per second can be worked out
# from the load timings.
set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "usage: $0 <output-dir> [benchmarkdotnet args...]" >&2
    exit 2
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="$(mkdir -p "$1" && cd "$1" && pwd)"
shift

dotnet build "$root/Shoko.Benchmarks/Shoko.Benchmarks.csproj" -c Release -v quiet -nologo
bin="$root/Shoko.Benchmarks/bin/Release/net10.0/Shoko.Benchmarks"

"$bin" --fixture > "$out/fixture.txt"

# BenchmarkDotNet looks for the project it builds its runners from under the working directory.
cd "$root"
"$bin" --filter 'Benchmarks.Text.*' --artifacts "$out" "$@"
