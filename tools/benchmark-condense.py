#!/usr/bin/env python3
"""Condenses the output of a comparison program (benchmarks/Routeables.Comparison*) into one short table for the job summary.

The full output stays in the log of the step (and in the artifacts of benchmark-repeat.yml, which reads it with benchmark-aggregate.py):
this only changes what is written to the summary. A line this script does not know is kept, folded, so nothing is lost.

    python tools/benchmark-condense.py output.txt >> "$GITHUB_STEP_SUMMARY"
"""
import re
import sys

HEADER = re.compile(r"^\.NET (\S+), (.+?), (\d+) cores, (\w+) GC, (\d+) steps, (\d+) runs after (\d+) warm-ups")
PASS = re.compile(r"^PASS\s+(.+?): (\d+) of (\d+) steps ended on the expected page(?: \((\d+) Forward steps skipped.*\))?")
AGAINST = re.compile(r"^- against (.+?): ([\d.]+) \(from ([\d.]+) to ([\d.]+)\); ratio of the two median times: ([\d.]+)")
ORDER = re.compile(r"^\s+- (.+?) measured first: ([\d.]+), measured second: ([\d.]+); the order cancelled: ([\d.]+)")
OVER = re.compile(r"^Time of (.+?) over the time of each other library")
LAYOUT = re.compile(r"^Layout, (.+?): (\d+) views seen after a step, (\d+) of them with no size \(([\d.]+) %\); (\d+) views built \(([\d.]+) % of the steps\)\.")
KEEPS = re.compile(r"^(.+?): the history keeps (\d+) entries before it is cleared\.")
ALIVE = re.compile(r"^(.+?), (before|after) the history is cleared: (\d+) of the (\d+) views built since the driver was created are alive\.")
REGION = re.compile(r"^(.+?): the region holds (\d+) views (before|after) the history is cleared\.")


def cells(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


def number(text):
    return text.replace(" ", "").strip()


def condense(text):
    title = None
    header = None
    checks = []          # PASS lines already turned into short texts
    skipped = []
    failures = []
    verdict = None
    measure = {}         # library -> (ms, us, kib)
    memory = {}          # library -> (heap, kept, after)
    heap_title = None
    versus = {}          # library -> (ratio, low, high)
    first_name = None
    order = {}           # library -> (first, second)
    layout = {}
    keeps = {}
    alive = {}
    region = {}
    unknown = []
    order_of_names = []
    section = None

    for raw in text.splitlines():
        line = raw.rstrip()
        if not line.strip():
            continue
        if line.startswith("### ") and title is None:
            title = line[4:]
            continue
        if (m := HEADER.match(line)):
            header = m
            continue
        if line.startswith("Runner:"):
            continue
        if (m := PASS.match(line)):
            if m.group(4):
                skipped.append(f"{m.group(1)}: {m.group(4)} Forward steps skipped (no Forward)")
            continue
        if line.startswith("FAIL"):
            failures.append(line)
            continue
        if line.startswith("All checks passed") or re.match(r"^\d+ check\(s\) failed", line):
            verdict = line
            continue
        if line.startswith("|"):
            row = cells(line)
            if set("".join(row)) <= set("-:"):
                continue
            if row[0] == "Library":
                section = "memory" if "heap after" in row[1] else "measure"
                if section == "memory":
                    heap_title = row[1]
                continue
            if section == "measure" and len(row) >= 5:
                measure[row[0]] = (row[1], row[2], row[3])
                order_of_names.append(row[0])
            elif section == "memory" and len(row) >= 4:
                memory[row[0]] = (row[1], row[2], row[3])
                if row[0] not in order_of_names:
                    order_of_names.append(row[0])
            else:
                unknown.append(line)
            continue
        if (m := OVER.match(line)):
            first_name = m.group(1)
            continue
        if (m := AGAINST.match(line)):
            versus[m.group(1)] = (m.group(2), m.group(3), m.group(4))
            last = m.group(1)
            continue
        if (m := ORDER.match(line)):
            order[last if versus else m.group(1)] = (m.group(2), m.group(3))
            continue
        if (m := LAYOUT.match(line)):
            layout[m.group(1)] = m
            continue
        if (m := KEEPS.match(line)):
            keeps[m.group(1)] = m.group(2)
            continue
        if (m := ALIVE.match(line)):
            alive.setdefault(m.group(1), {})[m.group(2)] = (m.group(3), m.group(4))
            continue
        if (m := REGION.match(line)):
            region.setdefault(m.group(1), {})[m.group(3)] = m.group(2)
            continue
        unknown.append(line)

    out = []
    out.append(f"### {title}" if title else "### Comparison")
    facts = []
    if header:
        facts.append(f".NET {header.group(1)}")
        facts.append(header.group(2))
        facts.append(f"{header.group(3)} cores")
        facts.append(f"{header.group(5)} steps × {header.group(6)} runs")
    if failures or (verdict and "failed" in verdict):
        facts.append("❌ checks failed")
    elif verdict:
        facts.append("✅ all checks passed")
    out.append(" · ".join(facts))
    for text_ in failures + skipped:
        out.append(f"- {text_}")
    out.append("")

    columns = ["Library"]
    if measure:
        columns += ["ms / run", "µs / step", "KiB / run"]
    if versus:
        columns.append(f"{first_name} time ÷ this")
    if memory:
        columns += [heap_title.replace("heap after ", "heap, ").replace(" changes (KiB)", " changes (KiB)"), "pages kept → after clear"]
    if len(columns) > 1:
        out.append("| " + " | ".join(columns) + " |")
        alignment = ["---"] + ["---:"] * (len(columns) - 1)
        if memory:
            alignment[-1] = "---"       # the last column is text ("100/100 \u2192 0")
        out.append("|" + "|".join(alignment) + "|")
        for name in order_of_names:
            row = [name]
            if measure:
                row += list(measure.get(name, ("", "", "")))
            if versus:
                if name == first_name:
                    row.append("—")
                elif name in versus:
                    r, lo, hi = versus[name]
                    row.append(f"{r} ({lo}–{hi})")
                else:
                    row.append("")
            if memory:
                heap, kept, after = memory.get(name, ("", "", ""))
                row += [heap, f"{kept.replace(' of ', '/')} → {after}" if kept else ""]
            out.append("| " + " | ".join(row) + " |")
        out.append("")

    # The ratio changes with the place of the library in the run: say so only when it does (a quarter or more).
    for name, (first, second) in order.items():
        try:
            f, s = float(first), float(second)
        except ValueError:
            continue
        if f > 0 and s > 0 and not 0.8 <= f / s <= 1.25:
            out.append(f"⚠ against {name}: {first_name or 'the first library'} measured first {first}, second {second}: the order weighs on the ratio, read the range.")
    if layout:
        parts = [f"{d} {m.group(2)} / {m.group(3)} / {m.group(6)} %" for d, m in layout.items()]
        out.append("Views seen / with no size / built per step: " + "; ".join(parts))
    if alive or keeps or region:
        parts = []
        for d, when in alive.items():
            b = when.get("before")
            a = when.get("after")
            parts.append(f"{d}: {b[0] + ' of ' + b[1] if b else '?'} → {a[0] if a else '?'}")
        for d, when in region.items():
            parts.append(f"{d} region holds {when.get('before', '?')} → {when.get('after', '?')}")
        for d, n in keeps.items():
            parts.append(f"{d} keeps {n} entries")
        out.append("Views alive, before → after the history is cleared: " + "; ".join(parts))
    if unknown:
        out.append("")
        out.append("<details><summary>Other lines</summary>")
        out.append("")
        out.append("```")
        out.extend(unknown)
        out.append("```")
        out.append("</details>")
    return "\n".join(out).rstrip() + "\n"


if __name__ == "__main__":
    source = open(sys.argv[1], encoding="utf-8").read() if len(sys.argv) > 1 else sys.stdin.read()
    sys.stdout.write(condense(source))
