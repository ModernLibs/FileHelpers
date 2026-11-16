"""Tests for round_runs.py (T12, review-T12 Important 1, rereview-T12 Important 1). Run from the repository root:

    python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'
"""

import io
import json
import os
import shutil
import subprocess
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
        self.envs = []

    def __call__(self, cmd, log, env=None):
        out = cmd[cmd.index("--out") + 1]
        glob = cmd[cmd.index("--filter") + 1]
        a, b = cmd[cmd.index("--a") + 1], cmd[cmd.index("--b") + 1]
        aot = "--aot" in cmd
        key = "executable_sha256" if aot else "library_sha256"
        self.calls.append((out, cmd))
        self.envs.append(env)
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

    def test_a_missing_or_empty_machine_txt_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "machine.txt")
            args = ["--check-resume", path, "--commit", "0123abcd", "--mode", MODE, "--filter-sha", "f1f1"]
            self.assertEqual(round_runs.main(args), 1)                    # missing
            open(path, "w", encoding="utf-8").close()
            self.assertEqual(round_runs.main(args), 1)                    # empty


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


class RowCapEnvAndBasePairTests(unittest.TestCase):
    """T59: --max-rows, --env, the base pairs (--base-commit)."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.raw = self.tmp.name

    def tearDown(self):
        self.tmp.cleanup()

    def main(self, filters, ab, extra=()):
        path = os.path.join(self.raw, "filters.txt")
        with open(path, "w", encoding="utf-8") as f:
            f.write(filters)
        argv = ["--raw", self.raw, "--bin", "/bin", "--src", "/src", "--filters", path, "--checks-cwd", self.raw,
                "--launches", "7"] + list(extra)
        old, sys.stdout = sys.stdout, io.StringIO()
        try:
            code = round_runs.main(argv, lister=lambda folder, glob: True, runner=ab,
                                   methods_lister=lambda folder, glob: ["Ns.Cls.FileHelpers_A", "Ns.Cls.CsvHelper_B"])
        finally:
            output, sys.stdout = sys.stdout.getvalue(), old
        return code, output

    def test_max_rows_and_env_reach_every_child_process(self):
        ab = FakeAb()
        code, _ = self.main(FILTERS, ab, ["--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=B=C"])
        self.assertEqual(code, 0)
        self.assertEqual(len(ab.envs), 5)
        for env in ab.envs:
            self.assertEqual(env, {"DOTNET_EnableAVX2": "0", "A": "B=C", "FH_BENCH_MAX_ROWS": "100000"})

    def test_without_the_options_the_children_get_no_extra_variables(self):
        ab = FakeAb()
        self.assertEqual(self.main(FILTERS, ab)[0], 0)
        self.assertEqual(ab.envs, [{}] * 5)

    def test_the_variables_are_in_the_environment_of_the_started_process(self):
        log = os.path.join(self.raw, "child.log")
        code = round_runs.process_runner(
            [sys.executable, "-c", "import os; print(os.environ['FH_BENCH_MAX_ROWS'], os.environ['X_Y'])"], log,
            round_runs.child_env(100000, {"X_Y": "z"}))
        self.assertEqual(code, 0)
        with open(log, encoding="utf-8") as f:
            self.assertEqual(f.read().strip(), "100000 z")

    def test_a_malformed_or_repeated_env_is_refused(self):
        for items in (["NOEQUALS"], ["1BAD=x"], ["A=1", "A=2"], ["FH_BENCH_MAX_ROWS=5"]):
            with self.assertRaises(ValueError, msg=items):
                round_runs.parse_env(items)
        self.assertEqual(round_runs.parse_env(["A=", "B=x y"]), {"A": "", "B": "x y"})
        ab = FakeAb()
        self.assertEqual(self.main(FILTERS, ab, ["--env", "oops"])[0], 2)
        self.assertEqual(self.main(FILTERS, ab, ["--max-rows", "0"])[0], 2)
        self.assertEqual(ab.calls, [])

    BASE_FILTERS = "write base-net10,base-net11,ra *WriteBenchmarks.WriteString_FileHelpers\n"

    def test_the_base_pairs_are_refused_without_a_base_commit(self):
        ab = FakeAb()
        code, _ = self.main(self.BASE_FILTERS, ab)
        self.assertEqual((code, ab.calls), (2, []))
        check = os.path.join(self.raw, "f.txt")
        with open(check, "w", encoding="utf-8") as f:
            f.write(self.BASE_FILTERS)
        self.assertEqual(round_runs.main(["--check-filters", check]), 2)
        self.assertEqual(round_runs.main(["--check-filters", check, "--base-commit", "abc"]), 0)

    def test_the_base_pairs_run_with_a_base_commit(self):
        ab = FakeAb()
        code, last = self.main(self.BASE_FILTERS, ab, ["--base-commit", "abc"])
        self.assertEqual(code, 0)
        self.assertEqual(last.splitlines()[-1], "ROUND COMPLETE 3 result files, 0 FAILED")
        pairs = sorted(os.path.basename(os.path.dirname(call[0])) for call in ab.calls)
        self.assertEqual(pairs, ["base-net10", "base-net11", "ra"])
        by_pair = dict((os.path.basename(os.path.dirname(call[0])), call[1]) for call in ab.calls)
        self.assertEqual(by_pair["base-net10"][by_pair["base-net10"].index("--a") + 1], "/bin/base-net10")
        self.assertEqual(by_pair["base-net10"][by_pair["base-net10"].index("--b") + 1], "/bin/net10")
        self.assertEqual(by_pair["base-net11"][by_pair["base-net11"].index("--a") + 1], "/bin/base-net11-ra-on")
        self.assertEqual(by_pair["base-net11"][by_pair["base-net11"].index("--b") + 1], "/bin/net11-ra-on")
        self.assertIn("tfm=net11.0 runtime-async=on", by_pair["base-net11"])     # --expect-a for the base folder

    def test_all_does_not_name_the_base_pairs(self):
        [line] = round_runs.read_filters("component all *X*\n")
        self.assertNotIn("base-net10", line.pairs)
        self.assertIn("runtime-410", line.pairs)


class CompareTests(unittest.TestCase):
    """T59: round.sh --compare is the library comparison, not an A/B."""

    FILTERS_ALL = ("# F10: in --compare mode the pairs column is ignored; the release filter file writes all\n"
                   "component  all  *Components.UnquoteBenchmarks*\n"
                   "read25     all  *DelimitedParseBenchmarks*\n"
                   "aot        aot-ra *PackageAssets_ParseAndDiscard\n")

    class Runner:
        """Stands in for the benchmark process: writes one BenchmarkDotNet report into --artifacts."""

        def __init__(self, fail_config=None):
            self.cmds = []
            self.envs = []
            self.fail_config = fail_config

        def __call__(self, cmd, log, env=None):
            self.cmds.append(cmd)
            self.envs.append(env)
            artifacts = cmd[cmd.index("--artifacts") + 1]
            os.makedirs(os.path.join(artifacts, "results"), exist_ok=True)
            with open(log, "w", encoding="utf-8") as f:
                f.write("console\n")
            if self.fail_config and self.fail_config in cmd[1]:
                return 1
            with open(os.path.join(artifacts, "results", "x-report-full.json"), "w", encoding="utf-8") as f:
                json.dump({"Benchmarks": [{"FullName": "Ns.Cls.FileHelpers_A", "Statistics": {"Median": 1.0}}]}, f)
            return 0

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.raw = self.tmp.name
        self.lines = round_runs.read_filters(self.FILTERS_ALL)

    def tearDown(self):
        self.tmp.cleanup()

    def methods_of(self, folder, glob):
        return ["Ns.Cls.FileHelpers_A", "Ns.Cls.ModernLibs_B", "Ns.Cls.Sylvan_C", "Ns.Cls.CsvHelper_D"]

    def compare(self, runner, resume=False, launches=3):
        out = io.StringIO()
        result = round_runs.run_compare(self.raw, "/bin", self.lines, launches, False, None, 20, resume,
                                        self.methods_of, runner, out=out, env={"FH_BENCH_MAX_ROWS": "100000"},
                                        hasher=lambda folder, harness: "sha-" + folder)
        return result, out.getvalue()

    def test_three_configuration_folders_and_no_ab_pair(self):
        runner = self.Runner()
        (results, failed), _ = self.compare(runner)
        self.assertEqual((results, failed), (6, 0))                 # 3 configurations x 2 bdn lines (aot is skipped)
        self.assertEqual(sorted(os.listdir(os.path.join(self.raw, "compare"))),
                         ["net10", "net11-ra-off", "net11-ra-on"])
        self.assertEqual(len(runner.cmds), 18)                       # 6 runs x 3 launches
        for cmd in runner.cmds:
            self.assertNotIn("--a", cmd)
            self.assertFalse(any(part.endswith("ab.py") for part in cmd))
            self.assertEqual(cmd[-4:], ["--artifacts", cmd[cmd.index("--artifacts") + 1], "--maxIterationCount", "20"])
            self.assertNotIn("--job", cmd)                           # BenchmarkDotNet's default job
        self.assertEqual(set(map(tuple, (sorted(e.items()) for e in runner.envs))), {(("FH_BENCH_MAX_ROWS", "100000"),)})
        with open(os.path.join(self.raw, "commands.txt"), encoding="utf-8") as f:
            self.assertNotIn("ab.py", f.read())

    def test_net11_ra_off_runs_only_the_library_methods_by_name(self):
        runner = self.Runner()
        self.compare(runner)
        off = [c for c in runner.cmds if "net11-ra-off" in c[1]]
        on = [c for c in runner.cmds if "net11-ra-on" in c[1]]
        self.assertEqual(off[0][2:off[0].index("--artifacts")],
                         ["--filter", "Ns.Cls.FileHelpers_A", "Ns.Cls.ModernLibs_B"])
        self.assertEqual(on[0][2:on[0].index("--artifacts")], ["--filter", "*Components.UnquoteBenchmarks*"])

    def test_a_failed_launch_is_recorded_and_the_comparison_goes_on(self):
        runner = self.Runner(fail_config="net11-ra-on")
        (results, failed), _ = self.compare(runner)
        self.assertEqual((results, failed), (4, 2))
        with open(os.path.join(self.raw, "failed.txt"), encoding="utf-8") as f:
            self.assertEqual(len(f.read().splitlines()), 2)

    def test_resume_keeps_the_launches_that_finished(self):
        self.compare(self.Runner())
        runner = self.Runner()
        (results, _), output = self.compare(runner, resume=True)
        self.assertEqual((results, runner.cmds), (6, []))
        self.assertIn("kept (resume)", output)

    def main(self, extra):
        path = os.path.join(self.raw, "filters.txt")
        with open(path, "w", encoding="utf-8") as f:
            f.write(self.FILTERS_ALL)
        argv = ["--raw", self.raw, "--bin", "/bin", "--src", "/src", "--filters", path, "--checks-cwd", self.raw,
                "--compare"] + extra
        runner = self.Runner()
        old, sys.stdout = sys.stdout, io.StringIO()
        try:
            code = round_runs.main(argv, runner=runner, methods_lister=self.methods_of)
        finally:
            output, sys.stdout = sys.stdout.getvalue(), old
        return code, output, runner

    def test_main_needs_no_base_commit_for_all_and_runs_three_launches(self):
        code, output, runner = self.main(["--launches", "3"])
        self.assertEqual(code, 0)
        self.assertEqual(output.splitlines()[-1], "ROUND COMPLETE 6 result files, 0 FAILED")
        self.assertEqual(len(runner.cmds), 18)
        code, _, runner = self.main(["--launches", "7"])
        self.assertEqual((code, runner.cmds), (2, []))


@unittest.skipUnless(os.name == "posix" and shutil.which("git") and shutil.which("bash"), "runs round.sh")
class RoundShRecordTests(unittest.TestCase):
    """round.sh itself, up to the first build: a fake dotnet fails the build step after machine.txt is written, so the
    record of --max-rows, --base-commit, --env and the mode can be read and --resume can be tried (T59)."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = self.tmp.name
        self.repo = os.path.join(root, "repo")
        tools = os.path.join(self.repo, "benchmarks", "tools")
        os.makedirs(tools)
        here = os.path.dirname(os.path.abspath(__file__))
        for name in ("round.sh", "round_runs.py"):
            shutil.copy(os.path.join(here, name), tools)
        with open(os.path.join(tools, "filters.txt"), "w", encoding="utf-8") as f:
            f.write("write ra *WriteBenchmarks.WriteString_FileHelpers\n")
        with open(os.path.join(tools, "filters-base.txt"), "w", encoding="utf-8") as f:
            f.write("write ra,base-net10 *WriteBenchmarks.WriteString_FileHelpers\n")
        with open(os.path.join(tools, "filters-compare.txt"), "w", encoding="utf-8") as f:
            f.write("write all *WriteBenchmarks*\n")
        git = ["git", "-C", self.repo, "-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false"]
        subprocess.run(["git", "init", "-q", self.repo], check=True)
        subprocess.run(git + ["add", "."], check=True)
        subprocess.run(git + ["commit", "-q", "-m", "base"], check=True)
        subprocess.run(git + ["tag", "v4.9.1"], check=True)
        self.base = subprocess.run(git + ["rev-parse", "HEAD"], capture_output=True, text=True,
                                   check=True).stdout.strip()
        with open(os.path.join(self.repo, "second.txt"), "w", encoding="utf-8") as f:
            f.write("x\n")
        subprocess.run(git + ["add", "."], check=True)
        subprocess.run(git + ["commit", "-q", "-m", "second"], check=True)
        fake = os.path.join(root, "fakebin")
        os.makedirs(fake)
        dotnet = os.path.join(fake, "dotnet")
        with open(dotnet, "w", encoding="utf-8") as f:
            f.write('#!/bin/sh\n[ "$1" = "--info" ] && { echo ".NET SDK:"; exit 0; }\nexit 1\n')
        os.chmod(dotnet, 0o755)
        self.env = dict(os.environ, PATH=fake + os.pathsep + os.environ["PATH"], DOTNET_ROOT=fake)
        self.root = os.path.join(root, "fh-round")

    def tearDown(self):
        self.tmp.cleanup()

    def round_sh(self, *args, filters="filters.txt"):
        script = os.path.join(self.repo, "benchmarks", "tools", "round.sh")
        proc = subprocess.run(["bash", script, "r1", "HEAD", os.path.join(self.repo, "benchmarks", "tools", filters),
                               "--vm", "vm1", "--root", self.root] + list(args),
                              capture_output=True, text=True, env=self.env, check=False)
        return proc.returncode, proc.stdout + proc.stderr

    def machine(self):
        with open(os.path.join(self.root, "raw", "r1", "vm1", "machine.txt"), encoding="utf-8") as f:
            return f.read()

    ARGS = ["--dry", "--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=B"]

    def test_machine_txt_records_max_rows_env_and_the_base_commit(self):
        code, output = self.round_sh(*self.ARGS, "--base-commit", self.base)
        self.assertNotEqual(code, 0)                                   # the fake dotnet fails the build step
        self.assertIn("ROUND FAILED during: build-variants.sh", output)
        text = self.machine()
        self.assertIn("max-rows=100000\n", text)
        self.assertIn("env=DOTNET_EnableAVX2=0\n", text)
        self.assertIn("env=A=B\n", text)
        self.assertIn("base-commit=%s\n" % self.base, text)
        mode = [l for l in text.splitlines() if l.startswith("mode: ")][0]
        for part in ("max-rows=100000", "env=DOTNET_EnableAVX2=0", "env=A=B", "base-commit=" + self.base):
            self.assertIn(part, mode)

    def test_without_the_options_the_record_says_none(self):
        self.round_sh("--dry")
        self.assertNotIn("error:", self.machine())
        text = self.machine()
        for line in ("max-rows=none\n", "env=none\n", "base-commit=none\n", "compare=no\n"):
            self.assertIn(line, text)

    def test_resume_refuses_another_max_rows_env_base_commit_or_mode(self):
        self.round_sh(*self.ARGS, "--base-commit", self.base)
        same = self.round_sh(*self.ARGS, "--base-commit", self.base, "--resume")
        self.assertNotIn("error: --resume", same[1])                   # the same call continues (and fails at the build)
        other = [
            ["--dry", "--max-rows", "10000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=B", "--base-commit", self.base],
            ["--dry", "--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--base-commit", self.base],
            ["--dry", "--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=C", "--base-commit", self.base],
            ["--dry", "--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=B"],
        ]
        for args in other:
            code, output = self.round_sh(*args, "--resume")
            self.assertEqual(code, 1, args)
            self.assertIn("error: --resume: measured as", output)
        code, output = self.round_sh("--dry", "--max-rows", "100000", "--env", "DOTNET_EnableAVX2=0", "--env", "A=B",
                                     "--base-commit", self.base, "--compare", "--resume", filters="filters-compare.txt")
        self.assertEqual(code, 1)
        self.assertIn("error: --resume", output)          # another mode (--compare) and another filter file

    def test_compare_mode_is_recorded_and_resume_refuses_leaving_it(self):
        code, _ = self.round_sh("--compare", "--max-iterations", "20", filters="filters-compare.txt")
        self.assertNotEqual(code, 0)
        text = self.machine()
        self.assertIn("compare=yes\n", text)
        self.assertIn("mode: comparison, 3 launches, not B4", text)
        code, output = self.round_sh("--max-iterations", "20", "--resume", filters="filters-compare.txt")
        self.assertEqual(code, 1)
        self.assertIn("error: --resume: measured as 'comparison, 3 launches, not B4", output)

    def test_the_runs_step_passes_every_option_to_round_runs(self):
        # The fake dotnet stops the script at the build, so step 5 never runs here: check that it hands each option on
        # (a missing --base-commit made round_runs.py refuse the base pairs after the builds, found by the T59 dry run).
        with open(os.path.join(self.repo, "benchmarks", "tools", "round.sh"), encoding="utf-8") as f:
            text = f.read()
        step5 = text[text.index('step="A/B runs"'):]
        for option in ("--base-commit", "--max-rows", "--env", "--compare", "--max-iterations", "--job", "--dry", "--resume"):
            self.assertIn("runs_args+=(%s" % option, step5)

    def test_compare_refuses_launches_and_the_base_pairs_need_a_base_commit(self):
        code, output = self.round_sh("--compare", "--launches", "7", filters="filters-compare.txt")
        self.assertEqual(code, 2)
        self.assertIn("always runs 3 launches", output)
        code, output = self.round_sh("--dry", filters="filters-base.txt")
        self.assertEqual(code, 2)
        self.assertIn("needs --base-commit", output)
        code, output = self.round_sh("--dry", "--base-commit", "HEAD")
        self.assertEqual(code, 1)
        self.assertIn("is the measured commit itself", output)


class CheckRowsTests(unittest.TestCase):
    """The program switch --check-rows prints the Rows values of the three classes with a Rows source (BenchRows.cs).
    Needs a Release build of the benchmark project (dotnet build -c Release); skipped without one."""

    @staticmethod
    def dll():
        project = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ModernLibs.FileHelpers.Benchmarks",
                               "bin", "Release")
        found = [p for p in (os.path.join(project, tfm, "ModernLibs.FileHelpers.Benchmarks.dll")
                             for tfm in ("net10.0", "net11.0")) if os.path.isfile(p)]
        return max(found, key=os.path.getmtime) if found else None

    def rows(self, max_rows):
        env = dict(os.environ)
        env.pop("FH_BENCH_MAX_ROWS", None)
        if max_rows:
            env["FH_BENCH_MAX_ROWS"] = max_rows
        proc = subprocess.run(["dotnet", self.dll(), "--check-rows"], capture_output=True, text=True, env=env,
                              check=False)
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)
        return [line for line in proc.stdout.splitlines() if line.startswith("Rows ")]

    @unittest.skipUnless(shutil.which("dotnet"), "no dotnet on PATH")
    def test_the_cap_cuts_the_values_of_all_three_classes(self):
        if self.dll() is None:
            self.skipTest("no Release build of the benchmark project")
        classes = ["DelimitedParseBenchmarks", "SepComparisonBenchmarks", "WriteBenchmarks"]
        full = self.rows(None)
        self.assertEqual(full, ["Rows %s: 10000 100000 1000000" % c for c in classes])
        capped = self.rows("100000")
        self.assertEqual(capped, ["Rows %s: 10000 100000" % c for c in classes])
        self.assertFalse(any("1000000" in line for line in capped))
        self.assertEqual(self.rows("10000"), ["Rows %s: 10000" % c for c in classes])


if __name__ == "__main__":
    unittest.main()
