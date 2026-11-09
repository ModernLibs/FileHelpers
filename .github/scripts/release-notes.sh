#!/usr/bin/env bash
# Prints the CHANGELOG.md section of one version as GitHub release notes.
#
# Usage: .github/scripts/release-notes.sh VERSION TAG [CHANGELOG]
#   VERSION    the version as written in the "## [VERSION] - date" heading, e.g. 4.9.0
#   TAG        the git tag the release points at, e.g. v4.9.0; relative links in the section
#              (docs/..., BENCHMARKS.md) become links to those files at this tag
#   CHANGELOG  path of the changelog (default CHANGELOG.md)
#
# Fails when the changelog has no section, or an empty one, for VERSION, so a release is never
# created without its notes. Used by publish.yml; run it locally to preview or backfill notes.
set -euo pipefail

version="$1"
tag="$2"
changelog="${3:-CHANGELOG.md}"
repo="${GITHUB_REPOSITORY:-ModernLibs/FileHelpers}"

# The section runs from its heading to the next "## [" heading or the link reference definitions
# ("[4.9.0]: https://...") at the end of the file.
notes=$(awk -v heading="## [$version]" '
  index($0, heading) == 1 { on = 1; next }
  on && (/^## \[/ || /^\[[^]]+\]: /) { exit }
  on { print }' "$changelog")

if [ -z "$(printf '%s' "$notes" | tr -d '[:space:]')" ]; then
  echo "::error::$changelog has no section for $version." >&2
  exit 1
fi

# Leading blank lines are dropped; links without a scheme or anchor-only target are made absolute.
printf '%s\n' "$notes" \
  | sed -e '/./,$!d' \
  | sed -E "s#\]\((\./)?([^):#][^):]*)\)#](https://github.com/$repo/blob/$tag/\2)#g"
