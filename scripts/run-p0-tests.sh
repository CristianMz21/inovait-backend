#!/usr/bin/env bash
# run-p0-tests.sh
#
# Verifies every P0 evidence ID declared in the canonical manifest table in
# docs/testing-strategy.md ("Manifest P0 canonico: ID -> productor unico")
# is discovered by at least one test, then runs the full P0 suite.
#
# The manifest is parsed at runtime -- IDs are never hardcoded here. Each row
# of that markdown table looks like:
#   | `SOME-ID` | V2-Txxx |
# The first back-ticked token on each row of that table is the evidence ID.
#
# IMPORTANT: `dotnet test` invoked against the solution file runs the two
# test projects in parallel, and their stdout interleaves at the line level
# (empirically confirmed: banner lines from both projects get concatenated
# onto a single line with no separating newline). That corrupts any
# line-oriented parsing of --list-tests output or run-summary lines. To
# avoid this, every dotnet test invocation below targets a single project
# at a time; results across projects are summed.
#
# Exit codes: 0 on full success; non-zero (with a clear message and the
# offending evidence IDs) on any manifest, discovery, or execution failure.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MANIFEST_FILE="docs/testing-strategy.md"
MANIFEST_HEADING="### Manifest P0 canónico: ID → productor único"
SOLUTION="Inovait.slnx"
CONFIGURATION="Debug"
EXPECTED_ID_COUNT=37
SANITY_FLOOR=20
PROBE_ID="__NO_SUCH_ID__"

TEST_PROJECTS=(
  "tests/Inovait.UnitTests/Inovait.UnitTests.csproj"
  "tests/Inovait.IntegrationTests/Inovait.IntegrationTests.csproj"
)

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

# Runs a command, capturing combined stdout+stderr into RUN_OUTPUT and its
# exit status into RUN_STATUS, without tripping `set -e`.
run_capture() {
  set +e
  RUN_OUTPUT="$("$@" 2>&1)"
  RUN_STATUS=$?
  set -e
}

# Counts test names printed by a single-project `dotnet test --list-tests`
# run. VSTest prints one "The following Tests are available:" marker
# followed by indented test names (or, if none match, an unindented
# "No test matches..." message). We only count indented, non-blank lines
# that immediately follow the marker, resetting on the first line that
# breaks that pattern.
count_listed_tests() {
  awk '
    /The following Tests are available:/ { found=1; next }
    found && /^[[:space:]]*$/ { next }
    found && /^[[:space:]]+[^[:space:]]/ { count++; next }
    { found=0 }
    END { print count + 0 }
  '
}

# Sums every "Total: <n>" occurrence in `dotnet test` run-summary output
# for a single project invocation.
sum_total_executed() {
  awk '
    match($0, /Total:[[:space:]]*[0-9]+/) {
      s = substr($0, RSTART, RLENGTH)
      gsub(/[^0-9]/, "", s)
      sum += s
    }
    END { print sum + 0 }
  '
}

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

# ---------------------------------------------------------------------------
# Step 1: parse the canonical P0 evidence manifest.
# ---------------------------------------------------------------------------

[[ -f "$MANIFEST_FILE" ]] || fail "manifest file not found: $MANIFEST_FILE"

heading_line="$(grep -n -F "$MANIFEST_HEADING" "$MANIFEST_FILE" | head -n1 | cut -d: -f1 || true)"
[[ -n "$heading_line" ]] || fail "could not find manifest heading '$MANIFEST_HEADING' in $MANIFEST_FILE"

total_lines="$(wc -l < "$MANIFEST_FILE")"
next_heading_offset="$(tail -n "+$((heading_line + 1))" "$MANIFEST_FILE" | grep -n -E '^#' | head -n1 | cut -d: -f1 || true)"
if [[ -n "$next_heading_offset" ]]; then
  section_end=$((heading_line + next_heading_offset - 1))
else
  section_end="$total_lines"
fi

mapfile -t manifest_ids < <(
  sed -n "$((heading_line + 1)),${section_end}p" "$MANIFEST_FILE" \
    | grep -E '^\|' \
    | grep -oE '^\|[[:space:]]*`[^`]+`' \
    | sed -E 's/^\|[[:space:]]*`([^`]+)`.*/\1/'
)

manifest_count="${#manifest_ids[@]}"
echo "Parsed ${manifest_count} evidence IDs from the P0 manifest (${MANIFEST_FILE})."

duplicate_ids="$(printf '%s\n' "${manifest_ids[@]}" | sort | uniq -d || true)"
if [[ -n "$duplicate_ids" ]]; then
  echo "ERROR: duplicate evidence IDs found in manifest:" >&2
  echo "$duplicate_ids" >&2
  exit 1
fi

if [[ "$manifest_count" -ne "$EXPECTED_ID_COUNT" ]]; then
  fail "expected ${EXPECTED_ID_COUNT} evidence IDs in manifest, found ${manifest_count}."
fi

echo "Manifest OK: ${manifest_count} unique evidence IDs, no duplicates."

# ---------------------------------------------------------------------------
# Step 2: build once; every subsequent dotnet test call uses --no-build.
# ---------------------------------------------------------------------------

echo "Building ${SOLUTION} (${CONFIGURATION})..."
run_capture dotnet build "$SOLUTION" --configuration "$CONFIGURATION"
if [[ "$RUN_STATUS" -ne 0 ]]; then
  echo "$RUN_OUTPUT"
  fail "dotnet build failed (exit ${RUN_STATUS})."
fi
echo "Build succeeded."

# ---------------------------------------------------------------------------
# Step 3: empirically verify whether `--list-tests` honors `--filter`.
#
# Known SDK quirk: on some dotnet/xunit runner combinations, `--list-tests`
# ignores `--filter` entirely and lists every discovered test regardless of
# the requested trait filter. Detect this by probing a deliberately
# nonexistent Evidence ID across both test projects: if tests still show
# up, the filter is being ignored and we must fall back to full execution
# per ID.
# ---------------------------------------------------------------------------

echo "Checking whether 'dotnet test --list-tests --filter' honors the filter..."
probe_count=0
for project in "${TEST_PROJECTS[@]}"; do
  run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --list-tests --filter "Priority=P0&Evidence=${PROBE_ID}"
  project_probe_count="$(printf '%s\n' "$RUN_OUTPUT" | count_listed_tests)"
  probe_count=$((probe_count + project_probe_count))
done

if [[ "$probe_count" -gt 0 ]]; then
  discovery_mode="execute-fallback"
  echo "QUIRK DETECTED: --list-tests --filter ignored the filter (found ${probe_count} tests for a nonexistent Evidence ID)."
  echo "Discovery mode: execute-fallback (each ID verified by running its filtered tests)."
else
  discovery_mode="list-tests"
  echo "Filter honored correctly (0 tests found for a nonexistent Evidence ID)."
  echo "Discovery mode: list-tests (each ID verified by --list-tests discovery)."
fi

# ---------------------------------------------------------------------------
# Step 4: verify every manifest ID, using the mode chosen above. Each ID is
# checked against every test project individually and results are summed,
# since an ID's producing tests may live in either project.
# ---------------------------------------------------------------------------

missing_ids=()
failed_ids=()

for id in "${manifest_ids[@]}"; do
  filter="Priority=P0&Evidence=${id}"
  id_total=0
  id_exec_failed=0

  for project in "${TEST_PROJECTS[@]}"; do
    if [[ "$discovery_mode" == "list-tests" ]]; then
      run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --list-tests --filter "$filter"
      project_count="$(printf '%s\n' "$RUN_OUTPUT" | count_listed_tests)"
      id_total=$((id_total + project_count))
    else
      run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --filter "$filter"
      if [[ "$RUN_STATUS" -ne 0 ]]; then
        id_exec_failed=1
        continue
      fi
      project_executed="$(printf '%s\n' "$RUN_OUTPUT" | sum_total_executed)"
      id_total=$((id_total + project_executed))
    fi
  done

  if [[ "$id_exec_failed" -eq 1 ]]; then
    failed_ids+=("$id")
  elif [[ "$id_total" -lt 1 ]]; then
    missing_ids+=("$id")
  fi
done

if [[ "${#missing_ids[@]}" -gt 0 || "${#failed_ids[@]}" -gt 0 ]]; then
  echo "ERROR: P0 evidence verification failed (mode: ${discovery_mode})." >&2
  if [[ "${#missing_ids[@]}" -gt 0 ]]; then
    echo "  Missing (no discovered/executed test): ${missing_ids[*]}" >&2
  fi
  if [[ "${#failed_ids[@]}" -gt 0 ]]; then
    echo "  Execution failed (non-zero exit): ${failed_ids[*]}" >&2
  fi
  exit 1
fi

echo "All ${manifest_count} manifest evidence IDs verified (mode: ${discovery_mode})."

# ---------------------------------------------------------------------------
# Step 5: only after every ID verifies, run the full P0 suite (per project,
# to avoid the interleaved-output problem described above).
# ---------------------------------------------------------------------------

echo "Running full P0 suite (Priority=P0)..."
total_executed=0
suite_failed=0
for project in "${TEST_PROJECTS[@]}"; do
  run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --filter "Priority=P0"
  echo "$RUN_OUTPUT"
  if [[ "$RUN_STATUS" -ne 0 ]]; then
    suite_failed=1
    echo "ERROR: P0 suite failed for ${project} (exit ${RUN_STATUS})." >&2
    continue
  fi
  project_executed="$(printf '%s\n' "$RUN_OUTPUT" | sum_total_executed)"
  total_executed=$((total_executed + project_executed))
done

if [[ "$suite_failed" -ne 0 ]]; then
  fail "full P0 suite failed."
fi

if [[ "$total_executed" -lt "$SANITY_FLOOR" ]]; then
  echo "WARNING: sanity check only -- total executed P0 tests (${total_executed}) < ${SANITY_FLOOR}. This never substitutes for the exact per-ID manifest verification above, which already passed." >&2
else
  echo "Sanity check passed: ${total_executed} P0 tests executed (>= ${SANITY_FLOOR})."
fi

echo "P0 GATE PASSED: ${manifest_count}/${manifest_count} manifest IDs verified, full P0 suite green (${total_executed} tests executed, mode: ${discovery_mode})."
