#!/usr/bin/env python3
"""Summarises Cobertura reports as a Markdown table: line and branch coverage of each assembly, and of the whole.

usage: coverage-summary.py [--min LINE_PERCENT] [--min-assembly NAME=LINE_PERCENT]... [--files N] [--files-of TEXT]
                           report.cobertura.xml [...]

With --min the exit code is 1 when the line coverage of the whole is below it (the CI uses 65). With --min-assembly (repeatable) it is 1
when the line coverage of the assembly NAME is below it. With --files N a second table lists the N files with the most lines that no test
runs (the place to write the next tests); --files-of keeps the assemblies whose name contains TEXT. A file shared between hosts
(Routeables.Shared is compiled into each of them) is listed once for each assembly that holds a copy.
"""
import re
import sys
import xml.etree.ElementTree as ET

_ROOT = re.compile(r"^.*?(?:^|/)((?:src|reshapes|samples|tests|benchmarks)/.*)$")


def repo_path(path):
    """The path of a source file from the root of the repository, whatever the runner: D:\\a\\Routeables\\Routeables\\src\\X.cs and /home/runner/work/Routeables/Routeables/src/X.cs are the same file."""
    path = path.replace("\\", "/")
    found = _ROOT.match(path)
    return found.group(1) if found else path


def ranges(numbers, limit=8):
    """[3, 4, 5, 9, 12, 13] -> "3-5, 9, 12-13" (the first `limit` ranges, then "...")."""
    numbers = sorted(set(numbers))
    out = []
    start = previous = None
    for number in numbers:
        if start is None:
            start = previous = number
        elif number == previous + 1:
            previous = number
        else:
            out.append(str(start) if start == previous else f"{start}-{previous}")
            start = previous = number
    if start is not None:
        out.append(str(start) if start == previous else f"{start}-{previous}")
    return ", ".join(out[:limit]) + (", ..." if len(out) > limit else "")


def main(argv):
    minimum = None
    per_assembly = {}
    top = 0
    only = ""
    files = []
    args = iter(argv)
    for arg in args:
        if arg == "--min":
            minimum = float(next(args))
        elif arg == "--min-assembly":
            name, _, percent = next(args).partition("=")
            per_assembly[name] = float(percent)
        elif arg == "--files":
            top = int(next(args))
        elif arg == "--files-of":
            only = next(args)
        else:
            files.append(arg)
    if not files:
        print(__doc__)
        return 2

    # Lines are counted once per file and number: the reports of several test projects can cover the same code.
    lines = {}      # assembly -> {(file, number): covered}
    branches = {}   # assembly -> {(file, number): (covered, total)}, the best of the reports
    for path in files:
        for package in ET.parse(path).getroot().iter("package"):
            name = package.get("name") or "?"
            seen = lines.setdefault(name, {})
            counts = branches.setdefault(name, {})
            for cls in package.iter("class"):
                file = repo_path(cls.get("filename") or "")
                for line in cls.findall("lines/line"):
                    key = (file, line.get("number"))
                    seen[key] = seen.get(key, False) or int(line.get("hits", "0")) > 0
                    if line.get("branch") == "True":
                        text = line.get("condition-coverage") or ""
                        if "(" in text:
                            covered, total = text.split("(")[1].rstrip(")").split("/")
                            best = counts.get(key)
                            if best is None or int(covered) > best[0]:
                                counts[key] = (int(covered), int(total))

    def pct(covered, total):
        return "-" if total == 0 else f"{100.0 * covered / total:.1f}%"

    print("| Assembly | Lines | Covered | Line coverage | Branch coverage |")
    print("|---|---:|---:|---:|---:|")
    all_lines = all_covered = all_bc = all_bt = 0
    for name in sorted(lines):
        total = len(lines[name])
        covered = sum(1 for hit in lines[name].values() if hit)
        bc = sum(c for c, _ in branches[name].values())
        bt = sum(t for _, t in branches[name].values())
        all_lines += total
        all_covered += covered
        all_bc += bc
        all_bt += bt
        print(f"| {name} | {total} | {covered} | {pct(covered, total)} | {pct(bc, bt)} |")
    print(f"| **All** | {all_lines} | {all_covered} | **{pct(all_covered, all_lines)}** | {pct(all_bc, all_bt)} |")

    if top > 0:
        rows = []
        for name in lines:
            if only not in name:
                continue
            by_file = {}
            missed = {}
            for (file, number), hit in lines[name].items():
                total, covered = by_file.get(file, (0, 0))
                by_file[file] = (total + 1, covered + (1 if hit else 0))
                if not hit:
                    missed.setdefault(file, []).append(int(number))
            for file, (total, covered) in by_file.items():
                if total > covered:
                    rows.append((total - covered, total, covered, name, file, ranges(missed[file])))
        rows.sort(key=lambda r: (-r[0], r[3], r[4]))
        print(f"\n| Not covered | Lines | Line coverage | Assembly | File | Lines not run |")
        print("|---:|---:|---:|---|---|---|")
        for missing, total, covered, name, file, where in rows[:top]:
            print(f"| {missing} | {total} | {pct(covered, total)} | {name} | {file} | {where} |")

    status = 0
    if minimum is not None and all_lines and 100.0 * all_covered / all_lines < minimum:
        print(f"\nLine coverage is below {minimum}%.", file=sys.stderr)
        status = 1
    for name, floor in per_assembly.items():
        total = len(lines.get(name, {}))
        covered = sum(1 for hit in lines.get(name, {}).values() if hit)
        if total == 0:
            print(f"\nNo line of {name} in the reports.", file=sys.stderr)
            status = 1
        elif 100.0 * covered / total < floor:
            print(f"\nLine coverage of {name} ({pct(covered, total)}) is below {floor}%.", file=sys.stderr)
            status = 1
    return status


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
