#!/usr/bin/env python3
"""Require real Unity test execution before treating the Quest CI as green.

Accept NUnit 2/3 or JUnit test-result XML. Do not infer successful tests
from the presence of C# test source files or from GameCI action exit alone.
"""
import os
import sys
from pathlib import Path
from xml.etree import ElementTree


def summarize(root):
    tag = root.tag.rsplit('}', 1)[-1]
    attrs = root.attrib
    if tag in ('test-run', 'test-results'):
        total = int(attrs.get('total', attrs.get('testcasecount', 0)))
        failed = int(attrs.get('failed', attrs.get('failures', 0))) + int(attrs.get('errors', 0))
        skipped = int(attrs.get('skipped', attrs.get('ignored', 0))) + int(attrs.get('inconclusive', 0))
        passed = int(attrs.get('passed', max(0, total - failed - skipped)))
        return total, passed, failed, skipped
    if tag == 'testsuites':
        suites = root.findall('testsuite')
        if not suites:
            return None
        return tuple(sum(v[i] for v in [summarize(s) for s in suites] if v is not None) for i in range(4))
    if tag == 'testsuite':
        total = int(attrs.get('tests', 0))
        failed = int(attrs.get('failures', 0)) + int(attrs.get('errors', 0))
        skipped = int(attrs.get('skipped', 0))
        return total, max(0, total - failed - skipped), failed, skipped
    return None


def main():
    directory = Path(sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else 'unity-test-results')
    reports = []
    if directory.is_dir():
        for path in sorted(directory.rglob('*.xml')):
            try:
                stats = summarize(ElementTree.parse(path).getroot())
            except (ElementTree.ParseError, ValueError):
                continue
            if stats is not None:
                reports.append((path, stats))
    totals = tuple(sum(stats[i] for _, stats in reports) for i in range(4))
    print(f'Unity XML reports: {len(reports)}; executed: {totals[0]}, passed: {totals[1]}, failed: {totals[2]}, skipped: {totals[3]}')
    for path, stats in reports:
        print(f'  {path}: total={stats[0]}, passed={stats[1]}, failed={stats[2]}, skipped={stats[3]}')
    valid = bool(reports) and totals[0] > 0 and totals[2] == 0
    summary = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary:
        with open(summary, 'a', encoding='utf-8') as output:
            output.write(f'### Quest Unity test execution\n\nReports: {len(reports)}; executed: {totals[0]}; passed: {totals[1]}; failed: {totals[2]}; skipped: {totals[3]}.\n\n')
    if not valid:
        print('::error::Unity test gate NOT RUN or failed: require at least one executed test in result XML and zero failures.', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
