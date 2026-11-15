import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from verify_unity_test_results import main, summarize
from xml.etree import ElementTree as ET


class XmlGateTests(unittest.TestCase):
    def test_formats(self):
        samples = [
            ('<test-run total="2" passed="2" failed="0" skipped="0"/>', (2, 2, 0, 0)),
            ('<test-results total="2" passed="1" failures="0" ignored="1"/>', (2, 1, 0, 1)),
            ('<testsuite tests="2" failures="0" skipped="1"/>', (2, 1, 0, 1)),
            ('<testsuites><testsuite tests="1"/><testsuite tests="1"/></testsuites>', (2, 2, 0, 0)),
            ('<testsuite><testcase name="a"/><testcase name="b"><skipped/></testcase></testsuite>', (2, 1, 0, 1)),
        ]
        for xml, expected in samples:
            with self.subTest(xml=xml):
                self.assertEqual(summarize(ET.fromstring(xml)), expected)

    def test_gate_cases(self):
        cases = [
            ('<test-run total="1" passed="1"/>', 0),
            ('<test-run total="0"/>', 1),
            ('<test-run total="2" skipped="2"/>', 1),
            ('<test-run total="1" failed="1"/>', 1),
            ('<test-run total="1"', 1),
            ('<unknown/>', 1),
            ('<testsuites><testsuite tests="1" failures="1"/></testsuites>', 1),
        ]
        for xml, expected in cases:
            with self.subTest(xml=xml), tempfile.TemporaryDirectory() as folder:
                Path(folder, "result.xml").write_text(xml)
                self.assertEqual(main([folder]), expected)

    def test_no_reports(self):
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(main([folder]), 1)

    def test_corrupt_plus_valid_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            Path(folder, "ok.xml").write_text('<test-run total="1" passed="1"/>')
            Path(folder, "bad.xml").write_text("<test-run")
            self.assertEqual(main([folder]), 1)


if __name__ == "__main__":
    unittest.main()
