"""Tests for round_runs.py (T12, review-T12 Important 1, rereview-T12 Important 1). Run from the repository root:

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


# What the fake builds hash to (ab.py records these per side; --resume compares them with the folders).
HASHES = {}


def fake_hasher(folder, harness):
    return HASHES.get(folder, "sha-" + folder)


class FakeAb:
    """Stands in for ab.py: writes a result file as ab.py does (glob, folders, binary hashes), or fails for the globs
    in `fail`."""

    def __init__(self, fail=()):
        self.fail = set(fail)
        self.calls = []

    def __call__(self, cmd, log):
        out = cmd[cmd.index("--out") + 1]
        glob = cmd[cmd.index("--filter") + 1]
        a, b = cmd[cmd.index("--a") + 1], cmd[cmd.index("--b") + 1]
        aot = "--aot" in cmd
        key = "executable_sha256" if aot else "library_sha256"
        self.calls.append((out, cmd))
        with open(log, "w", encoding="utf-8") as f:
            f.write("[A] x\n# B4 result\nrow\n")
        if glob in self.fail:
            return 1
        side = {}
        for name, folder in (("a", a), ("b", b)):
            side[name] = {"dir": folder, key: fake_hasher(os.path.relpath(folder, "/bin"), None)}
        document = {"a": side["a"], "b": side["b"], "filter": glob, "benchmarks": []}
        if aot:
            document["harness"] = "aot"
        with open(out, "w", encoding="utf-8") as f:
            json.dump(document, f)
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
        HASHES.clear()
        self.tmp.cleanup()

    def run_round(self, ab, resume=False, lister=lambda folder, glob: True):
        out = io.StringIO()
        result = round_runs.run_round(self.raw, "/bin", "/src", self.lines, 7, False, None, None, resume,
                                      lister, ab, out=out, hasher=fake_hasher)
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

    def test_resume_runs_again_a_result_made_from_another_binary(self):
        self.run_round(FakeAb())
        HASHES["net11-ra-on"] = "sha-rebuilt"           # net11-ra-on was rebuilt and differs
        ab = FakeAb()
        (results, failed), output = self.run_round(ab, resume=True)
        self.assertEqual((results, failed), (5, 0))
        rerun = sorted(os.path.basename(os.path.dirname(c[0])) for c in ab.calls)
        self.assertEqual(rerun, ["ra", "ra"])            # both lines of the pair with net11-ra-on, nothing else
        self.assertIn("side B was measured from another binary", output)

    def test_resume_runs_again_a_result_made_for_another_glob(self):
        self.run_round(FakeAb())
        line = [l for l in self.lines if l.group == "quoted"][0]
        path = round_runs.result_path(self.raw, "ra", line)
        with open(path, encoding="utf-8") as f:
            document = json.load(f)
        document["filter"] = "*SomethingElse*"           # the same file name, written for another line
        with open(path, "w", encoding="utf-8") as f:
            json.dump(document, f)
        ab = FakeAb()
        (_, _), output = self.run_round(ab, resume=True)
        self.assertEqual([c[0] for c in ab.calls], [path])
        self.assertIn("made for the glob '*SomethingElse*'", output)

    def test_resume_runs_again_a_result_of_other_folders_or_a_damaged_file(self):
        self.run_round(FakeAb())
        quoted = [l for l in self.lines if l.group == "quoted"][0]
        moved = round_runs.result_path(self.raw, "v410-net10", quoted)
        with open(moved, encoding="utf-8") as f:
            document = json.load(f)
        document["a"]["dir"] = "/old/bin/pkg-net10"
        with open(moved, "w", encoding="utf-8") as f:
            json.dump(document, f)
        proto = [l for l in self.lines if l.group == "prototype"][0]
        damaged = round_runs.result_path(self.raw, "v410-net10", proto)
        with open(damaged, "w", encoding="utf-8") as f:
            f.write('{"a": {"dir": "/bin/pkg')                # cut off by a kill
        ab = FakeAb()
        (_, _), output = self.run_round(ab, resume=True)
        self.assertEqual(sorted(c[0] for c in ab.calls), sorted([moved, damaged]))
        self.assertIn("side A was /old/bin/pkg-net10", output)
        self.assertIn("missing or damaged result file", output)

    def test_without_resume_every_run_runs(self):
        self.run_round(FakeAb())
        ab = FakeAb()
        (results, _), _ = self.run_round(ab, resume=False)
        self.assertEqual((results, len(ab.calls)), (5, 5))

    def test_a_glob_missing_in_a_build_is_not_run(self):
        (results, failed), _ = self.run_round(FakeAb(), lister=lambda folder, glob: folder != "pkg-net10")
        self.assertEqual((results, failed), (3, 0))            # quoted and the prototype in v410-net10
        self.assertEqual(len(self.read("not-run.txt").splitlines()), 2)

    def test_check_filters_exit_codes(self):
        filters = os.path.join(self.raw, "filters.txt")
        with open(filters, "w", encoding="utf-8") as f:
            f.write("bad line\n")
        self.assertEqual(round_runs.main(["--check-filters", filters]), 2)
        with open(filters, "w", encoding="utf-8") as f:
            f.write(FILTERS)
        self.assertEqual(round_runs.main(["--check-filters", filters]), 0)

    def main(self, ab, extra=()):
        filters = os.path.join(self.raw, "filters.txt")
        with open(filters, "w", encoding="utf-8") as f:
            f.write(FILTERS)
        argv = ["--raw", self.raw, "--bin", "/bin", "--src", "/src", "--filters", filters, "--checks-cwd", self.raw]
        argv += list(extra) or ["--launches", "7"]
        out = io.StringIO()
        old = sys.stdout
        sys.stdout = out
        try:
            code = round_runs.main(argv, lister=lambda folder, glob: True, runner=ab)
        finally:
            sys.stdout = old
        return code, out.getvalue().splitlines()[-1] if out.getvalue() else ""

    def test_main_last_line_and_exit_code(self):
        code, last = self.main(FakeAb())
        self.assertEqual((code, last), (0, "ROUND COMPLETE 5 result files, 0 FAILED"))

    def test_main_exit_1_when_a_run_failed(self):
        for name in ("not-run.txt", "failed.txt", "commands.txt", "timing.txt"):
            if os.path.exists(os.path.join(self.raw, name)):
                os.remove(os.path.join(self.raw, name))
        code, last = self.main(FakeAb(fail={"*PackageAssets_ParseAndDiscard"}))
        self.assertEqual((code, last), (1, "ROUND COMPLETE 4 result files, 1 FAILED"))

    def test_main_refuses_fewer_than_7_launches_without_dry(self):
        ab = FakeAb()
        code, _ = self.main(ab, extra=["--launches", "5"])
        self.assertEqual((code, ab.calls), (2, []))
        code, _ = self.main(FakeAb(), extra=["--launches", "5", "--dry"])
        self.assertEqual(code, 0)


MACHINE = """round: r
vm: v
started: 2026-11-12T00:00:00Z
mode: round (B4: 7 launches per side, job default, max iterations 20)
commit: 0123abcd (requested as HEAD)
repository: /repo (HEAD 0123abcd; round.sh and round_runs.py are the commit's copies)
filter file: file /repo/benchmarks/tools/filters-step0.txt, same as benchmarks/tools/filters-step0.txt in 0123abcd, sha256 f1f1
launches: 7

== uname -a
Linux vm

== resumed: 2026-11-12T05:00:00Z (mode: round (B4: 7 launches per side, job default))
"""
MODE = "round (B4: 7 launches per side, job default, max iterations 20)"


class ResumeCheckTests(unittest.TestCase):
    """round.sh --resume continues only the round recorded in machine.txt (b9a3394 and rereview-T12 Important 1)."""

    def test_the_same_commit_mode_and_filter_continue(self):
        self.assertEqual(round_runs.check_resume(MACHINE, "0123abcd", MODE, "f1f1"), [])

    def test_another_commit_is_refused(self):
        reasons = round_runs.check_resume(MACHINE, "9999ffff", MODE, "f1f1")
        self.assertEqual(len(reasons), 1)
        self.assertIn("measured at 0123abcd, not 9999ffff", reasons[0])

    def test_another_mode_is_refused(self):
        for mode in ("round (B4: 7 launches per side, job default)",                       # no cap
                     "round (B4: 8 launches per side, job default, max iterations 20)",
                     "DRY RUN (ab.py --smoke, 5 launches, job Dry): a smoke check of the script, not evidence (D1)"):
            reasons = round_runs.check_resume(MACHINE, "0123abcd", mode, "f1f1")
            self.assertEqual(len(reasons), 1, mode)
            self.assertIn("measured as '%s'" % MODE, reasons[0])

    def test_another_filter_file_is_refused(self):
        reasons = round_runs.check_resume(MACHINE, "0123abcd", MODE, "e2e2")
        self.assertEqual(reasons, ["measured with the filter file sha256 f1f1, this call has e2e2"])

    def test_main_check_resume(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "machine.txt")
            with open(path, "w", encoding="utf-8") as f:
                f.write(MACHINE)
            base = ["--check-resume", path, "--commit", "0123abcd", "--mode", MODE]
            self.assertEqual(round_runs.main(base + ["--filter-sha", "f1f1"]), 0)
            self.assertEqual(round_runs.main(base + ["--filter-sha", "e2e2"]), 1)


class AotFolderCheckTests(unittest.TestCase):
    """round.sh step 4 for the Native AOT folders (round_runs.py --check-aot)."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.aot = self.tmp.name
        for name, line, major in round_runs.AOT_FOLDERS:
            self.make(name, line, line, "native-aot=yes app-tfm=x rid=linux-x64", ".NET %s.0.1" % major)

    def tearDown(self):
        self.tmp.cleanup()

    def make(self, name, variant_line, printed, build, runtime, executable=True):
        folder = os.path.join(self.aot, name)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, "variant.txt"), "w", encoding="utf-8") as f:
            f.write("%s: %s\ncommit=abc\naot=true\n" % (name, variant_line))
        exe = os.path.join(folder, round_runs.AOT_EXECUTABLE)
        with open(exe, "w", encoding="utf-8") as f:
            f.write("#!/bin/sh\necho '// ModernLibs.FileHelpers variant: %s'\necho '// ModernLibs.FileHelpers library: x'\n"
                    "echo '// Build: %s'\necho '// Runtime: %s'\n" % (printed, build, runtime))
        os.chmod(exe, 0o755 if executable else 0o644)

    def check(self):
        out, err = io.StringIO(), io.StringIO()
        code = round_runs.check_aot(self.aot, out=out, err=err)
        return code, out.getvalue(), err.getvalue()

    def test_matching_folders_pass_and_give_the_variants_blocks(self):
        code, out, err = self.check()
        self.assertEqual((code, err), (0, ""))
        self.assertEqual(out.count("== aot/"), 5)
        self.assertIn("== aot/pkg-net11\npkg-net11: tfm=net10.0 runtime-async=off\ncommit=abc\naot=true\n"
                      "// ModernLibs.FileHelpers variant: tfm=net10.0 runtime-async=off", out)

    def assert_mismatch(self, folder, reason):
        code, _, err = self.check()
        self.assertEqual(code, 1)
        self.assertIn("VARIANT MISMATCH %s" % os.path.join(self.aot, folder), err)
        self.assertIn(reason, err)

    def test_wrong_printed_variant(self):
        self.make("net11-ra-on", "tfm=net11.0 runtime-async=on", "tfm=net11.0 runtime-async=off",
                  "native-aot=yes app-tfm=net11.0 rid=linux-x64", ".NET 11.0.0")
        self.assert_mismatch("net11-ra-on", "printed variant 'tfm=net11.0 runtime-async=off'")

    def test_a_jit_process(self):
        self.make("net10", "tfm=net10.0 runtime-async=off", "tfm=net10.0 runtime-async=off",
                  "native-aot=no app-tfm=net10.0 rid=linux-x64", ".NET 10.0.15")
        self.assert_mismatch("net10", "not native-aot=yes")

    def test_a_package_folder_on_the_wrong_runtime(self):
        self.make("pkg-net11", "tfm=net10.0 runtime-async=off", "tfm=net10.0 runtime-async=off",
                  "native-aot=yes app-tfm=net10.0 rid=linux-x64", ".NET 10.0.15")
        self.assert_mismatch("pkg-net11", "expected .NET 11")

    def test_variant_txt_of_another_folder(self):
        self.make("net10", "tfm=net11.0 runtime-async=on", "tfm=net10.0 runtime-async=off",
                  "native-aot=yes app-tfm=net10.0 rid=linux-x64", ".NET 10.0.15")
        self.assert_mismatch("net10", "variant.txt does not start with 'net10: tfm=net10.0 runtime-async=off'")

    def test_an_executable_that_cannot_start(self):
        exe = os.path.join(self.aot, "net10", round_runs.AOT_EXECUTABLE)
        with open(exe, "wb") as f:
            f.write(b"\x00\x01 not a program")          # executable bit set, but no valid format: OSError
        self.assert_mismatch("net10", "cannot start")

    def test_no_executable(self):
        self.make("net11-ra-off", "tfm=net11.0 runtime-async=off", "tfm=net11.0 runtime-async=off",
                  "native-aot=yes app-tfm=net11.0 rid=linux-x64", ".NET 11.0.0", executable=False)
        self.assert_mismatch("net11-ra-off", "no AOT harness executable")


if __name__ == "__main__":
    unittest.main()
