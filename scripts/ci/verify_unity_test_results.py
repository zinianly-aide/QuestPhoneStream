#!/usr/bin/env python3
"""Fail closed unless Unity NUnit/JUnit XML proves real passing tests."""
import os
import sys
from pathlib import Path
from xml.etree import ElementTree as ET


def name(element):
    return element.tag.rsplit("}", 1)[-1]


def number(element, key, default=0):
    value = int(element.get(key, default))
    if value < 0:
        raise ValueError(f"negative {key}")
    return value


def summarize(root):
    tag = name(root)
    if tag in ("test-run", "test-results"):
        total = number(root, "total", number(root, "testcasecount"))
        failed = number(root, "failed", number(root, "failures")) + number(root, "errors")
        skipped = sum(number(root, key) for key in ("skipped", "ignored", "inconclusive", "not-run"))
        # NUnit 2 commonly reports 'not-run'; NUnit 3 uses 'skipped'.
        passed = number(root, "passed", max(0, total - failed - skipped))
    elif tag in ("testsuites", "testsuite"):
        cases = [node for node in root.iter() if name(node) == "testcase"]
        if cases:
            total = len(cases)
            failed = sum(any(name(child) in ("failure", "error") for child in case) for case in cases)
            skipped = sum(any(name(child) == "skipped" for child in case) for case in cases)
            passed = total - failed - skipped
        elif tag == "testsuites":
            suites = [node for node in root if name(node) == "testsuite"]
            if not suites:
                raise ValueError("no test suites")
            summaries = [summarize(suite) for suite in suites]
            return tuple(sum(s[index] for s in summaries) for index in range(4))
        else:
            total = number(root, "tests")
            failed = number(root, "failures") + number(root, "errors")
            skipped = number(root, "skipped")
            passed = total - failed - skipped
    else:
        raise ValueError(f"unsupported XML root: {tag}")

    if min(total, passed, failed, skipped) < 0 or passed + failed + skipped > total:
        raise ValueError("inconsistent test counts")
    return total, passed, failed, skipped


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    directory = Path(argv[0] if argv else "unity-test-results")
    reports = []
    errors = []
    for path in sorted(directory.rglob("*.xml")) if directory.is_dir() else []:
        try:
            reports.append((path, summarize(ET.parse(path).getroot())))
        except (ET.ParseError, ValueError, OSError) as exc:
            errors.append(f"{path}: {exc}")

    totals = tuple(sum(stats[i] for _, stats in reports) for i in range(4))
    total, passed, failed, skipped = totals
    executed = total - skipped
    print(f"Unity XML reports: {len(reports)}; total: {total}; executed: {executed}; "
          f"passed: {passed}; failed: {failed}; skipped: {skipped}")
    for path, stats in reports:
        print(f"  {path}: total={stats[0]} passed={stats[1]} failed={stats[2]} skipped={stats[3]}")
    for error in errors:
        print(f"::error::{error}", file=sys.stderr)
    valid = bool(reports) and not errors and executed > 0 and failed == 0 and passed > 0
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as output:
            output.write(f"### Quest Unity test execution\n\nReports: {len(reports)}; "
                         f"total: {total}; executed: {executed}; passed: {passed}; "
                         f"failed: {failed}; skipped: {skipped}; invalid: {len(errors)}.\n\n")
    if not valid:
        print("::error::Unity test gate failed: require valid XML, executed > 0, "
              "passed > 0 and failures = 0.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
