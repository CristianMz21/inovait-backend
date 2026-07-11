#!/usr/bin/env python3
"""Fail when git numstat input exceeds the human review budget."""

from __future__ import annotations

import sys


HUMAN_LINE_LIMIT = 400


def main() -> int:
    total = 0

    for line_number, raw_line in enumerate(sys.stdin, start=1):
        line = raw_line.rstrip("\n")
        if not line:
            continue

        fields = line.split("\t", 2)
        if len(fields) != 3:
            print(f"invalid numstat row {line_number}: {line!r}", file=sys.stderr)
            return 2

        additions, deletions, path = fields
        if additions == "-" or deletions == "-":
            print(f"binary/unclassified human path: {path}", file=sys.stderr)
            return 2

        try:
            total += int(additions) + int(deletions)
        except ValueError:
            print(f"invalid numstat row {line_number}: {line!r}", file=sys.stderr)
            return 2

    print(total)
    if total > HUMAN_LINE_LIMIT:
        print(
            f"human gate failed: {total} > {HUMAN_LINE_LIMIT}",
            file=sys.stderr,
        )
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
