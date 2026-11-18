#!/usr/bin/env python3
"""Summarises Cobertura reports as a Markdown table: line and branch coverage of each assembly, and of the whole.

usage: coverage-summary.py [--min LINE_PERCENT] report.cobertura.xml [...]

With --min the exit code is 1 when the line coverage of the whole is below it (the CI uses 65).
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


def main(argv):
    minimum = None
    files = []
    args = iter(argv)
    for arg in args:
        if arg == "--min":
            minimum = float(next(args))
        else:
            files.append(arg)
    if not files:
        print(__doc__)
        return 2

    # Lines are counted once per file and number: the reports of several test projects can cover the same code.
    lines = {}      # assembly -> {(file, number): covered}
    branches = {}   # assembly -> [covered, total]
    for path in files:
        for package in ET.parse(path).getroot().iter("package"):
            name = package.get("name") or "?"
            seen = lines.setdefault(name, {})
            counts = branches.setdefault(name, [0, 0])
            for cls in package.iter("class"):
                file = repo_path(cls.get("filename") or "")
                for line in cls.findall("lines/line"):
                    key = (file, line.get("number"))
                    seen[key] = seen.get(key, False) or int(line.get("hits", "0")) > 0
                    if line.get("branch") == "True":
                        text = line.get("condition-coverage") or ""
                        if "(" in text:
                            covered, total = text.split("(")[1].rstrip(")").split("/")
                            counts[0] += int(covered)
                            counts[1] += int(total)

    def pct(covered, total):
        return "-" if total == 0 else f"{100.0 * covered / total:.1f}%"

    print("| Assembly | Lines | Covered | Line coverage | Branch coverage |")
    print("|---|---:|---:|---:|---:|")
    all_lines = all_covered = all_bc = all_bt = 0
    for name in sorted(lines):
        total = len(lines[name])
        covered = sum(1 for hit in lines[name].values() if hit)
        bc, bt = branches[name]
        all_lines += total
        all_covered += covered
        all_bc += bc
        all_bt += bt
        print(f"| {name} | {total} | {covered} | {pct(covered, total)} | {pct(bc, bt)} |")
    print(f"| **All** | {all_lines} | {all_covered} | **{pct(all_covered, all_lines)}** | {pct(all_bc, all_bt)} |")

    if minimum is not None and all_lines and 100.0 * all_covered / all_lines < minimum:
        print(f"\nLine coverage is below {minimum}%.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
