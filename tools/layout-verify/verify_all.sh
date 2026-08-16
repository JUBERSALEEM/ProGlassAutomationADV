#!/usr/bin/env bash
# ══════════════════════════════════════════════════════════════════════
# Single pre-push gate. Run from the repo root:
#     bash tools/layout-verify/verify_all.sh
#
# Exists because I shipped three separate build breaks by running SOME
# of the checks and not others:
#   MC3061  attribute text emitted into element content
#   MC3072  property element after content (App.xaml)
#   MC3072  property element after content (TrendChartControl.xaml)
#
# Every check must pass. Non-zero exit = do not commit.
# ══════════════════════════════════════════════════════════════════════
set -u
cd "$(dirname "$0")/../.." || exit 2

XAML=$(git ls-files '*.xaml')
# Include files not yet tracked by git (new controls, new styles).
UNTRACKED=$(git ls-files --others --exclude-standard '*.xaml')
XAML="$XAML $UNTRACKED"

CS=$(git ls-files '*.cs' | grep -v '^Tests/')
CS_NEW=$(git ls-files --others --exclude-standard '*.cs' | grep -v '^Tests/')
CS="$CS $CS_NEW"

FAIL=0
run() {
  local name="$1"; shift
  local out
  out=$("$@" 2>&1)
  local rc=$?
  local tail_line
  tail_line=$(echo "$out" | grep -E '[0-9]+ (file|XAML file)' | tail -1)
  if [ $rc -ne 0 ] || echo "$out" | grep -qE '[1-9][0-9]* (failing|issue|stray-attribute issue)'; then
    echo "FAIL  $name"
    echo "$out" | grep -vE '^\s*ok\s' | head -20
    FAIL=1
  else
    echo "ok    $name  ${tail_line}"
  fi
}

echo "── XAML ──────────────────────────────────────────────"
run "well-formed        " python3 tools/layout-verify/xaml_wellformed.py $XAML
run "stray attributes   " python3 tools/layout-verify/stray_attr_check.py $XAML
run "property el. order " python3 tools/layout-verify/property_element_order.py $XAML

echo "── C# ────────────────────────────────────────────────"
run "brace balance      " python3 tools/layout-verify/brace_scan.py $CS

echo "──────────────────────────────────────────────────────"
if [ $FAIL -ne 0 ]; then
  echo "VERIFY FAILED — do not commit."
  exit 1
fi
echo "All checks passed."
