#!/usr/bin/env python3
"""Aggregates the time ratios of several runs of the comparison job.

Each input file is the output of one run (the text the benchmark programs write: the "Time of X over the time of each other library" block
and its "- against Y: r (from a to b); ratio of the two median times: c" lines). For each pair of libraries in each section the script writes
the number of runs, and the median, the smallest and the largest of the two figures, so that a ratio that moves from 0.7 to 1.3 from one run
to the next is seen as the noise it is, and a ratio that stays between 0.85 and 0.95 as a difference.

Usage: python3 tools/benchmark-aggregate.py run1.txt run2.txt ...
"""
import re
import statistics
import sys
from collections import OrderedDict

AGAINST = re.compile(r"^(?:- )?against (?P<other>.+?): (?P<median>[\d.]+) \(from (?P<low>[\d.]+) to (?P<high>[\d.]+)\)(?:; ratio of the two median times: (?P<medians>[\d.]+))?\s*$")
OVER = re.compile(r"^Time of (?P<first>.+?) over the time of each other library")


def parse(path):
    """Yields (section, first library, other library, median of the ratios, ratio of the median times or None)."""
    section = ""
    previous = ""
    first = ""
    with open(path, encoding="utf-8-sig") as handle:
        for raw in handle:
            line = raw.rstrip("\n")
            if line.startswith(".NET "):
                section = previous.lstrip("# ").strip()
            elif (over := OVER.match(line)):
                first = over.group("first")
            elif (match := AGAINST.match(line)):
                medians = match.group("medians")
                yield (section, first, match.group("other"), float(match.group("median")), float(medians) if medians else None)
            if line.strip():
                previous = line


def spread(values):
    return f"{statistics.median(values):.2f} ({min(values):.2f} to {max(values):.2f})"


def main(paths):
    if not paths:
        print(__doc__)
        return 2
    groups = OrderedDict()
    for path in paths:
        for section, first, other, ratio, medians in parse(path):
            entry = groups.setdefault((section, first, other), ([], []))
            entry[0].append(ratio)
            if medians is not None:
                entry[1].append(medians)

    print(f"### Time ratios over {len(paths)} runs")
    print()
    print("Median, then the smallest and the largest, of what each run measured (above 1 means the first library is slower).")
    print()
    print("| Section | First library | Against | Runs | Median of the run-by-run ratios | Ratio of the median times |")
    print("|---|---|---|---:|---|---|")
    for (section, first, other, ), (ratios, medians) in groups.items():
        print(f"| {section} | {first} | {other} | {len(ratios)} | {spread(ratios)} | {spread(medians) if medians else '-'} |")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
