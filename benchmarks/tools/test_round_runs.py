"""Tests for round_runs.py (T12, review-T12 Important 1). Run from the repository root:

    python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'
"""

import io
import json
import os
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True   # no __pycache__ next to the tools (the clone of a round stays clean)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import round_runs  # noqa: E402

FILTERS = """# comment
quoted     ra,v410-net10      *QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard
async      ra                 *EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null
prototype  v410-net10         *Prototypes.R6UpperBoundBenchmarks*
aot        aot-ra             *PackageAssets_ParseAndDiscard
"""


class FakeAb:
    """Stands in for ab.py: writes a result file, or fails for the globs in `fail`."""

    def __init__(self, fail=()):
        self.fail = set(fail)
        self.calls = []

    def __call__(self, cmd, log):
        out = cmd[cmd.index("--out") + 1]
        glob = cmd[cmd.index("--filter") + 1]
        self.calls.append((out, cmd))
        with open(log, "w", encoding="utf-8") as f:
            f.write("[A] x\n# B4 result\nrow\n")
        if glob in self.fail:
            return 1
        with open(out, "w", encoding="utf-8") as f:
            json.dump({"a": {}, "b": {}, "benchmarks": []}, f)
        return 0


class FilterTests(unittest.TestCase):
    def test_gate_lines_run_first(self):
        runs = round_runs.plan(round_runs.read_filters(FILTERS))
        self.assertEqual([(r[0], r[4].group) for r in runs[:1]], [("v410-net10", "prototype")])
        self.assertEqual(len(runs), 5)

    def test_aot_group_only_in_aot_pairs(self):
        with self.assertRaisesRegex(ValueError, "line 1: group 'aot' cannot run in pair 'ra'"):
            round_runs.read_filters("aot ra *PackageAssets_ParseAndDiscard\n")
        with self.assertRaisesRegex(ValueError, "cannot run in pair 'aot-ra'"):
            round_runs.read_filters("quoted aot-ra *Quoted*\n")
        with self.assertRaisesRegex(ValueError, "unknown pair 'nope'"):
            round_runs.read_filters("quoted nope *Quoted*\n")

    def test_commands(self):
        line = round_runs.read_filters("quoted v410-net10 *Q*\n")[0]
        cmd = round_runs.command("/src", "/bin", "v410-net10", "pkg-net10", "net10", "bdn", line, "/o.json", 7,
                                 False, None, 20)
        self.assertIn("--expect-a", cmd)
        self.assertNotIn("--expect-b", cmd)
        self.assertEqual(cmd[-3:], ["--", "--maxIterationCount", "20"])
        self.assertNotIn("--smoke", cmd)
        dry = round_runs.command("/src", "/bin", "v410-net10", "pkg-net10", "net10", "bdn", line, "/o.json", 5,
                                 True, "Dry", None)
        self.assertIn("--smoke", dry)
        self.assertEqual(dry[-3:], ["--", "--job", "Dry"])
        aot = round_runs.read_filters("aot aot-ra *P*\n")[0]
        cmd = round_runs.command("/src", "/bin", "aot-ra", "aot/net11-ra-off", "aot/net11-ra-on", "aot", aot,
                                 "/o.json", 7, False, "Dry", 20)
        self.assertIn("--aot", cmd)
        self.assertEqual(cmd[cmd.index("--") + 1:], round_runs.AOT_ARGS)   # no BenchmarkDotNet arguments


class RunTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.raw = self.tmp.name
        self.lines = round_runs.read_filters(FILTERS)

    def tearDown(self):
        self.tmp.cleanup()

    def run_round(self, ab, resume=False, lister=lambda folder, glob: True):
        out = io.StringIO()
        result = round_runs.run_round(self.raw, "/bin", "/src", self.lines, 7, False, None, None, resume,
                                      lister, ab, out=out)
        return result, out.getvalue()

    def read(self, name):
        with open(os.path.join(self.raw, name), encoding="utf-8") as f:
            return f.read()

    def test_a_failed_run_is_recorded_and_the_round_goes_on(self):
        ab = FakeAb(fail={"*Prototypes.R6UpperBoundBenchmarks*"})
        (results, failed), output = self.run_round(ab)
        self.assertEqual((results, failed), (4, 1))
        self.assertEqual(len(ab.calls), 5)                      # every run after the failure still ran
        failed_lines = self.read("failed.txt").splitlines()
        self.assertEqual(len(failed_lines), 1)
        pair, group, glob, log = failed_lines[0].split("\t")
        self.assertEqual((pair, group, glob), ("v410-net10", "prototype", "*Prototypes.R6UpperBoundBenchmarks*"))
        self.assertTrue(log.endswith(".log") and os.path.isfile(os.path.join(self.raw, log)))
        self.assertIn("FAILED exit 1", self.read("timing.txt"))
        self.assertIn("FAILED: v410-net10", output)

    def test_resume_keeps_finished_runs_and_retries_the_failed_one(self):
        self.run_round(FakeAb(fail={"*Prototypes.R6UpperBoundBenchmarks*"}))
        ab = FakeAb()
        (results, failed), output = self.run_round(ab, resume=True)
        self.assertEqual((results, failed), (5, 0))
        self.assertEqual(len(ab.calls), 1)                      # only the failed run again
        self.assertIn("R6UpperBound", ab.calls[0][0])
        self.assertEqual(self.read("failed.txt"), "")
        self.assertIn("resume: 4 result files kept", output)
        self.assertEqual(self.read("commands.txt").count("# "), 2)   # started, resumed

    def test_without_resume_every_run_runs(self):
        self.run_round(FakeAb())
        ab = FakeAb()
        (results, _), _ = self.run_round(ab, resume=False)
        self.assertEqual((results, len(ab.calls)), (5, 5))

    def test_a_glob_missing_in_a_build_is_not_run(self):
        (results, failed), _ = self.run_round(FakeAb(), lister=lambda folder, glob: folder != "pkg-net10")
        self.assertEqual((results, failed), (3, 0))            # quoted and the prototype in v410-net10
        self.assertEqual(len(self.read("not-run.txt").splitlines()), 2)

    def test_main_exit_code_and_last_line(self):
        filters = os.path.join(self.raw, "filters.txt")
        with open(filters, "w", encoding="utf-8") as f:
            f.write("bad line\n")
        self.assertEqual(round_runs.main(["--check-filters", filters]), 2)
        with open(filters, "w", encoding="utf-8") as f:
            f.write(FILTERS)
        self.assertEqual(round_runs.main(["--check-filters", filters]), 0)


if __name__ == "__main__":
    unittest.main()
