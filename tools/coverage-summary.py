#!/usr/bin/env python3
"""Summarises Cobertura reports as a Markdown table: line and branch coverage of each assembly, and of the whole.

usage: coverage-summary.py [--min LINE_PERCENT] [--min-assembly NAME=LINE_PERCENT]... [--files N] [--files-of TEXT]
                           [--files-skip NAME]... report.cobertura.xml [...]
       coverage-summary.py --per-report report.cobertura.xml [...]
       coverage-summary.py --explain ASSEMBLY report.cobertura.xml [...]

With --min the exit code is 1 when the line coverage of the whole is below it (the CI uses 65). With --min-assembly (repeatable) it is 1
when the line coverage of the assembly NAME is below it. With --files N a second table lists the N files with the most lines that no test
runs (the place to write the next tests); --files-of keeps the assemblies whose name contains TEXT; --files-skip (repeatable) leaves the
assembly NAME out of that table and says in one line how many lines it has that no test runs (the hosts, which need a platform to run).
With --per-report one table gives, for each report, the coverage of the whole and of Routeables.Routing and Routeables.Core (to tell a
report that is missing or badly read from one that covers little). A file shared between hosts
(Routeables.Shared is compiled into each of them) is listed once for each assembly that holds a copy.

With --explain one table gives, for the assembly ASSEMBLY, what each report holds of it: its lines, those it runs, and those that only
this report lists. The merged table counts a line once, wherever it is listed, so a report whose tool lists lines that the others do not
(braces, the continuations of async methods) lowers the merged percentage below the one of the report that covers the code best.

The coverage tool counts the lines that hold only a brace as lines of code, and marks the closing brace of a method that always throws as
not run. In the second table such a line (only a brace, a blank or a comment) is left out of "Not covered" and of "Lines not run", when
the source file is found from the directory where the script runs (the root of the repository): what is listed is code to write a test for.
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


_SOURCES = {}


def is_trivial(file, number):
    """True when the line of the source file holds only a brace, a blank or a comment. False when the file is not there."""
    if file not in _SOURCES:
        try:
            with open(file, encoding="utf-8-sig") as handle:
                _SOURCES[file] = handle.read().split("\n")
        except OSError:
            _SOURCES[file] = None
    text = _SOURCES[file]
    if text is None or not 0 < number <= len(text):
        return False
    stripped = text[number - 1].strip()
    return stripped in ("", "{", "}", "};", "});", ");", "]", "];") or stripped.startswith("//")


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


def load(paths):
    """Lines are counted once per file and number: the reports of several test projects can cover the same code."""
    lines = {}      # assembly -> {(file, number): covered}
    branches = {}   # assembly -> {(file, number): (covered, total)}, the best of the reports
    for path in paths:
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
    return lines, branches


def percent(covered, total):
    return "-" if total == 0 else f"{100.0 * covered / total:.1f}%"


def per_report(paths):
    """One row for each report: what it covers of the whole, of Routeables.Routing and of Routeables.Core."""
    print("| Report | Lines | Line coverage | Routing | Core |")
    print("|---|---:|---:|---:|---:|")
    for path in paths:
        lines, _ = load([path])
        everything = [hit for assembly in lines.values() for hit in assembly.values()]

        def of(name):
            hits = list(lines.get(name, {}).values())
            return percent(sum(hits), len(hits))

        folder = path.replace("\\", "/").split("/")
        name = folder[-2] if len(folder) > 1 else folder[-1]
        stem = folder[-1].split(".")[0]
        if len(stem) > 8 and "-" in stem:
            name = f"{name} ({stem[:8]})"          # the coverage of the test project of the core is a file named by a Guid
        print(f"| {name} | {len(everything)} | {percent(sum(everything), len(everything))} | {of('Routeables.Routing')} | {of('Routeables.Core')} |")


def explain(paths, assembly):
    """What each report holds of one assembly, and the lines that only it lists."""
    reports = []
    for path in paths:
        lines, _ = load([path])
        reports.append((path, lines.get(assembly, {})))
    print(f"| Report | Lines of {assembly} | Run | Line coverage | Listed only here | of which run |")
    print("|---|---:|---:|---:|---:|---:|")
    for index, (path, own) in enumerate(reports):
        others = set()
        for other_index, (_, lines) in enumerate(reports):
            if other_index != index:
                others.update(lines)
        alone = [key for key in own if key not in others]
        folder = path.replace("\\", "/").split("/")
        name = folder[-2] if len(folder) > 1 else folder[-1]
        stem = folder[-1].split(".")[0]
        if len(stem) > 8 and "-" in stem:
            name = f"{name} ({stem[:8]})"
        run = sum(1 for hit in own.values() if hit)
        print(f"| {name} | {len(own)} | {run} | {percent(run, len(own))} | {len(alone)} | {sum(1 for key in alone if own[key])} |")
    merged, _ = load(paths)
    everything = merged.get(assembly, {})
    run = sum(1 for hit in everything.values() if hit)
    print(f"| **merged** | {len(everything)} | {run} | **{percent(run, len(everything))}** | | |")


def main(argv):
    minimum = None
    per_assembly = {}
    top = 0
    only = ""
    skip = []
    by_report = False
    explained = None
    files = []
    args = iter(argv)
    for arg in args:
        if arg == "--min":
            minimum = float(next(args))
        elif arg == "--min-assembly":
            name, _, floor_text = next(args).partition("=")
            per_assembly[name] = float(floor_text)
        elif arg == "--files":
            top = int(next(args))
        elif arg == "--files-of":
            only = next(args)
        elif arg == "--files-skip":
            skip.append(next(args))
        elif arg == "--per-report":
            by_report = True
        elif arg == "--explain":
            explained = next(args)
        else:
            files.append(arg)
    if not files:
        print(__doc__)
        return 2

    if by_report:
        per_report(files)
        return 0

    if explained is not None:
        explain(files, explained)
        return 0

    lines, branches = load(files)
    pct = percent

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
        skipped = {}      # assembly -> [lines that no test runs, files]
        for name in lines:
            if only not in name:
                continue
            by_file = {}
            missed = {}
            for (file, number), hit in lines[name].items():
                total, covered = by_file.get(file, (0, 0))
                by_file[file] = (total + 1, covered + (1 if hit else 0))
                if not hit and not is_trivial(file, int(number)):
                    missed.setdefault(file, []).append(int(number))
            for file, (total, covered) in by_file.items():
                if file in missed and name in skip:
                    count = skipped.setdefault(name, [0, 0])
                    count[0] += len(missed[file])
                    count[1] += 1
                elif file in missed:
                    rows.append((len(missed[file]), total, covered, name, file, ranges(missed[file], 12)))
        rows.sort(key=lambda r: (-r[0], r[3], r[4]))
        print(f"\n| Not covered | Lines | Line coverage | Assembly | File | Lines not run |")
        print("|---:|---:|---:|---|---|---|")
        for missing, total, covered, name, file, where in rows[:top]:
            print(f"| {missing} | {total} | {pct(covered, total)} | {name} | {file} | {where} |")
        if skipped:
            parts = [f"{name.removeprefix('Routeables.')} {lines_} in {count} file{'' if count == 1 else 's'}" for name, (lines_, count) in sorted(skipped.items(), key=lambda p: -p[1][0])]
            print(f"\nNot listed, the hosts (they need a platform to run), lines that no test runs: {'; '.join(parts)}.")

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
