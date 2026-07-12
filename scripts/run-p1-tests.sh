#!/usr/bin/env bash
# run-p1-tests.sh
#
# Verifies every P1 evidence ID declared in the canonical manifest sentence in
# docs/testing-strategy.md ("## Staging y manifests de evidencia", the
# paragraph beginning "El manifest P1 consumido por V2-T101 permanece
# separado:") is discovered by at least one test, then runs the full P1
# suite. Mirrors scripts/run-p0-tests.sh; see that script for the rationale
# behind each step. The one structural difference: the P0 manifest lives in a
# markdown table under its own "### Manifest P0 canonico" heading, while the
# P1 manifest is a single prose sentence with a comma-separated, back-ticked
# ID list -- so the parsing step below extracts backtick tokens from that one
# line instead of table rows.
#
# The manifest is parsed at runtime -- IDs are never hardcoded here beyond the
# EXPECTED_ID_COUNT cross-check, which mirrors run-p0-tests.sh's use of the
# count asserted in the doc's own prose (37 for P0); the P1 manifest sentence
# does not carry a separate stated count, so EXPECTED_ID_COUNT below is the
# count of backtick tokens on that manifest line as read from the doc, used
# purely as a self-consistency guard against silent truncation of the parse.
#
# IMPORTANT: see run-p0-tests.sh for why every dotnet test invocation below
# targets a single project at a time (parallel-project stdout interleaving
# corrupts line-oriented parsing of --list-tests/run-summary output).
#
# Exit codes: 0 on full success; non-zero (with a clear message and the
# offending evidence IDs) on any manifest, discovery, or execution failure.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MANIFEST_FILE="docs/testing-strategy.md"
MANIFEST_ANCHOR="El manifest P1 consumido por V2-T101 permanece separado:"
SOLUTION="Inovait.slnx"
CONFIGURATION="Debug"
EXPECTED_ID_COUNT=13
SANITY_FLOOR=30
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
# Step 1: parse the canonical P1 evidence manifest sentence.
# ---------------------------------------------------------------------------

[[ -f "$MANIFEST_FILE" ]] || fail "manifest file not found: $MANIFEST_FILE"

anchor_line="$(grep -n -F "$MANIFEST_ANCHOR" "$MANIFEST_FILE" | head -n1 | cut -d: -f1 || true)"
[[ -n "$anchor_line" ]] || fail "could not find manifest anchor sentence in $MANIFEST_FILE"

mapfile -t manifest_ids < <(
  sed -n "${anchor_line}p" "$MANIFEST_FILE" \
    | grep -oE '`[^`]+`' \
    | sed -E 's/^`([^`]+)`$/\1/'
)

manifest_count="${#manifest_ids[@]}"
echo "Parsed ${manifest_count} evidence IDs from the P1 manifest (${MANIFEST_FILE})."

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
  run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --list-tests --filter "Priority=P1&Evidence=${PROBE_ID}"
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
  filter="Priority=P1&Evidence=${id}"
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
  echo "ERROR: P1 evidence verification failed (mode: ${discovery_mode})." >&2
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
# Step 5: only after every ID verifies, run the full P1 suite (per project,
# to avoid the interleaved-output problem described above).
# ---------------------------------------------------------------------------

echo "Running full P1 suite (Priority=P1)..."
total_executed=0
suite_failed=0
for project in "${TEST_PROJECTS[@]}"; do
  run_capture dotnet test "$project" --no-build --configuration "$CONFIGURATION" --filter "Priority=P1"
  echo "$RUN_OUTPUT"
  if [[ "$RUN_STATUS" -ne 0 ]]; then
    suite_failed=1
    echo "ERROR: P1 suite failed for ${project} (exit ${RUN_STATUS})." >&2
    continue
  fi
  project_executed="$(printf '%s\n' "$RUN_OUTPUT" | sum_total_executed)"
  total_executed=$((total_executed + project_executed))
done

if [[ "$suite_failed" -ne 0 ]]; then
  fail "full P1 suite failed."
fi

if [[ "$total_executed" -lt "$SANITY_FLOOR" ]]; then
  echo "WARNING: sanity check only -- total executed P1 tests (${total_executed}) < ${SANITY_FLOOR}. This never substitutes for the exact per-ID manifest verification above, which already passed." >&2
else
  echo "Sanity check passed: ${total_executed} P1 tests executed (>= ${SANITY_FLOOR})."
fi

echo "P1 GATE PASSED: ${manifest_count}/${manifest_count} manifest IDs verified, full P1 suite green (${total_executed} tests executed, mode: ${discovery_mode})."
