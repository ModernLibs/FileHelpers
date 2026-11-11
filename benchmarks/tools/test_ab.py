"""Tests for ab.py (T08). Run from the repository root:

    python3 -I -m unittest benchmarks/tools/test_ab.py
"""

import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ab  # noqa: E402  (the path above makes the module next to this file importable under -I)


class VerdictTests(unittest.TestCase):
    def test_verdicts(self):
        a = [100.0, 101.0, 99.0, 100.5, 99.5] * 4
        self.assertEqual(ab.verdict(a, [x * 0.90 for x in a]), "faster")      # 10 % faster
        self.assertEqual(ab.verdict(a, [x * 1.10 for x in a]), "slower")
        self.assertEqual(ab.verdict(a, [x * 0.99 for x in a]), "no-change")   # below the 3 % floor
        self.assertEqual(ab.verdict(a, [90.0, 110.0] * 10), "no-change")      # interval includes 1.0

    def test_three_percent_floor_is_inclusive(self):
        a = [100.0, 101.0, 99.0, 100.5, 99.5] * 4
        self.assertEqual(ab.verdict(a, [x * 0.97 for x in a]), "faster")
        self.assertEqual(ab.verdict(a, [x * 1.03 for x in a]), "slower")
        self.assertEqual(ab.verdict(a, [x * 0.975 for x in a]), "no-change")

    def test_compare_reports_ratio_interval_and_verdict(self):
        a = [100.0, 101.0, 99.0, 100.5, 99.5] * 4
        result = ab.compare(a, [x * 0.90 for x in a])
        self.assertAlmostEqual(result["median_ratio"], 0.90, places=12)
        self.assertLessEqual(result["ci_low"], result["median_ratio"])
        self.assertGreaterEqual(result["ci_high"], result["median_ratio"])
        self.assertLess(result["ci_high"], 1.0)
        self.assertEqual(result["confidence"], 0.999)
        self.assertEqual(result["resamples"], 10_000)
        self.assertEqual(result["verdict"], "faster")

    def test_bootstrap_uses_a_fixed_seed(self):
        a = [100.0, 104.0, 97.0, 101.0, 99.0, 103.0]
        b = [98.0, 99.0, 101.0, 96.0, 100.0, 97.0]
        self.assertEqual(ab.compare(a, b), ab.compare(a, b))

    def test_too_few_samples_is_an_error(self):
        with self.assertRaises(ValueError):
            ab.compare([100.0], [90.0, 91.0])


class ReportTests(unittest.TestCase):
    def test_parse_report_reads_the_median_of_every_benchmark(self):
        report = {
            "Title": "x",
            "Benchmarks": [
                {"FullName": "N.C.M(Rows: 10000)", "Statistics": {"Median": 1500.5}},
                {"FullName": "N.C.M(Rows: 100000)", "Statistics": {"Median": 16000.0}},
            ],
        }
        with tempfile.TemporaryDirectory() as tmp:
            results = os.path.join(tmp, "results")
            os.makedirs(results)
            with open(os.path.join(results, "N.C-report-full.json"), "w", encoding="utf-8") as f:
                json.dump(report, f)
            self.assertEqual(
                ab.parse_reports(tmp),
                {"N.C.M(Rows: 10000)": 1500.5, "N.C.M(Rows: 100000)": 16000.0})

    def test_parse_report_without_a_report_is_an_error(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(RuntimeError):
                ab.parse_reports(tmp)


class RunnerTests(unittest.TestCase):
    def test_launches_alternate_a_and_b_one_method_per_launch(self):
        calls = []

        def launch(side, bench_dir, method, artifacts):
            calls.append((side, method))
            base = 100.0 if side == "a" else 90.0
            return {method + "(Rows: 10)": base + len(calls) % 3}

        with tempfile.TemporaryDirectory() as tmp:
            result = ab.run_ab("A", "B", 3, ["N.C.M1", "N.C.M2"], launch, tmp)

        self.assertEqual(calls, [
            ("a", "N.C.M1"), ("b", "N.C.M1"), ("a", "N.C.M2"), ("b", "N.C.M2"),
            ("a", "N.C.M1"), ("b", "N.C.M1"), ("a", "N.C.M2"), ("b", "N.C.M2"),
            ("a", "N.C.M1"), ("b", "N.C.M1"), ("a", "N.C.M2"), ("b", "N.C.M2"),
        ])
        names = [r["name"] for r in result]
        self.assertEqual(names, ["N.C.M1(Rows: 10)", "N.C.M2(Rows: 10)"])
        for r in result:
            self.assertEqual(len(r["a_median_ns"]), 3)
            self.assertEqual(len(r["b_median_ns"]), 3)
            self.assertIn(r["verdict"], ("faster", "slower", "no-change"))

    def test_fewer_than_five_launches_are_refused(self):
        # With 2 launches per side the 99.9 % interval excludes 1.0 in about a third of runs without any
        # difference (simulated), with 5 in about 1 %, with 10 in none of 200.
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(SystemExit):
                ab.main(["--a", tmp, "--b", tmp, "--launches", "4", "--filter", "*", "--out", os.path.join(tmp, "o.json")])

    def test_a_benchmark_missing_from_one_launch_is_an_error(self):
        def launch(side, bench_dir, method, artifacts):
            return {} if side == "b" else {method: 1.0}

        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(RuntimeError):
                ab.run_ab("A", "B", 2, ["N.C.M1"], launch, tmp)


if __name__ == "__main__":
    unittest.main()
