"""Tests for ab.py (T08). Run from the repository root:

    python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'
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


def _startup_log(bench_dir, variant="tfm=net11.0 runtime-async=on", runtime=".NET 11.0.0"):
    return (
        "// ModernLibs.FileHelpers variant: %s\n"
        "// ModernLibs.FileHelpers assembly: %s\n"
        "// Runtime: %s\n"
        "// Validating benchmarks:\n" % (variant, os.path.join(bench_dir, "ModernLibs.FileHelpers.dll"), runtime))


class StartupTests(unittest.TestCase):
    def test_expected_variant_comes_from_the_configuration_name(self):
        self.assertEqual(ab.expected_variant("/x/net10", None), ("tfm=net10.0 runtime-async=off", "10"))
        self.assertEqual(ab.expected_variant("/x/net11-ra-on", None), ("tfm=net11.0 runtime-async=on", "11"))
        self.assertEqual(ab.expected_variant("/x/net11-ra-off/", None), ("tfm=net11.0 runtime-async=off", "11"))

    def test_a_folder_with_another_name_needs_an_explicit_expectation(self):
        with self.assertRaises(ValueError):
            ab.expected_variant("/x/fh491", None)
        self.assertEqual(ab.expected_variant("/x/fh491", "tfm=net10.0 runtime-async=off"),
                         ("tfm=net10.0 runtime-async=off", None))

    def test_an_expectation_cannot_contradict_the_configuration_name(self):
        with self.assertRaises(ValueError):
            ab.expected_variant("/x/net11-ra-off", "tfm=net11.0 runtime-async=on")

    def test_check_startup_accepts_the_matching_variant(self):
        bench = "/x/net11-ra-on"
        startup = ab.check_startup(_startup_log(bench), bench, ab.expected_variant(bench, None))
        self.assertEqual(startup, {
            "variant": "tfm=net11.0 runtime-async=on",
            "assembly": "/x/net11-ra-on/ModernLibs.FileHelpers.dll",
            "runtime": ".NET 11.0.0",
        })

    def test_check_startup_refuses_another_variant(self):
        bench = "/x/net11-ra-off"
        with self.assertRaises(RuntimeError):
            ab.check_startup(_startup_log(bench), bench, ab.expected_variant(bench, None))

    def test_check_startup_refuses_a_library_from_another_folder(self):
        with self.assertRaises(RuntimeError):
            ab.check_startup(_startup_log("/elsewhere"), "/x/net11-ra-on", ab.expected_variant("/x/net11-ra-on", None))

    def test_check_startup_refuses_another_runtime(self):
        bench = "/x/net11-ra-on"
        with self.assertRaises(RuntimeError):
            ab.check_startup(_startup_log(bench, runtime=".NET 10.0.15"), bench, ab.expected_variant(bench, None))

    def test_check_startup_refuses_a_log_without_the_lines(self):
        with self.assertRaises(RuntimeError):
            ab.check_startup("// Validating benchmarks:\n", "/x/net10", ab.expected_variant("/x/net10", None))


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
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(SystemExit):
                ab.main(["--a", tmp, "--b", tmp, "--launches", "4", "--filter", "*", "--smoke",
                         "--out", os.path.join(tmp, "o.json")])

    def test_a_b4_result_needs_seven_launches(self):
        # With n launches per side and no real difference, the interval excludes 1.0 when all B samples lie
        # on one side of all A samples: 2 / C(2n, n), 0.79 % for n = 5 and 0.058 % for n = 7 (review-T08).
        self.assertEqual(ab.MIN_LAUNCHES_B4, 7)
        self.assertEqual(ab.MIN_LAUNCHES_SMOKE, 5)
        self.assertTrue(ab.launch_policy(7, smoke=False))
        with self.assertRaises(ValueError):
            ab.launch_policy(6, smoke=False)
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(SystemExit):
                ab.main(["--a", tmp, "--b", tmp, "--launches", "6", "--filter", "*", "--out", os.path.join(tmp, "o.json")])

    def test_smoke_allows_five_launches_and_is_not_b4(self):
        self.assertFalse(ab.launch_policy(5, smoke=True))
        self.assertFalse(ab.launch_policy(9, smoke=True))
        with self.assertRaises(ValueError):
            ab.launch_policy(4, smoke=True)
        self.assertIn("smoke, not B4", ab.table_header(b4=False, launches=5))
        self.assertNotIn("smoke", ab.table_header(b4=True, launches=7))

    def test_allow_dirty_is_never_b4(self):
        # Lead ruling R21: --allow-dirty forces "b4": false, also with 7 launches and without --smoke.
        b4, reason = ab.b4_status(7, smoke=False, allow_dirty=True)
        self.assertFalse(b4)
        self.assertIn("uncommitted changes", reason)
        header = ab.table_header(b4=False, launches=7, reason=reason)
        self.assertIn("not B4", header)
        self.assertIn("uncommitted changes", header)
        self.assertEqual(ab.b4_status(7, smoke=False, allow_dirty=False), (True, None))
        self.assertFalse(ab.b4_status(5, smoke=True, allow_dirty=True)[0])

    def test_options_that_measure_nothing_cannot_be_passed_to_benchmarkdotnet(self):
        for bad in (["--list", "flat"], ["--help"], ["-h"], ["--info"], ["--version"], ["--filter", "*"],
                    ["--artifacts", "x"]):
            with self.assertRaises(ValueError):
                ab.check_bdn_args(bad)
        ab.check_bdn_args(["--job", "short"])

    def test_a_benchmark_missing_from_one_launch_is_an_error(self):
        def launch(side, bench_dir, method, artifacts):
            return {} if side == "b" else {method: 1.0}

        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(RuntimeError):
                ab.run_ab("A", "B", 2, ["N.C.M1"], launch, tmp)


class _FakeRun:
    """Stands in for subprocess.run: writes a console log and, optionally, a report into the launch folder."""

    def __init__(self, bench_dir, write_report=True, returncode=0, variant="tfm=net11.0 runtime-async=on"):
        self.bench_dir = bench_dir
        self.write_report = write_report
        self.returncode = returncode
        self.variant = variant

    def __call__(self, cmd, cwd, stdout, stderr, check):
        stdout.write(_startup_log(self.bench_dir, variant=self.variant))
        if self.write_report:
            os.makedirs(os.path.join(cwd, "results"), exist_ok=True)
            with open(os.path.join(cwd, "results", "N.C-report-full.json"), "w", encoding="utf-8") as f:
                json.dump({"Benchmarks": [{"FullName": "N.C.M(Rows: 10)", "Statistics": {"Median": 42.0}}]}, f)

        class Result:
            pass

        result = Result()
        result.returncode = self.returncode
        return result


class LauncherTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.bench = os.path.join(self._tmp.name, "net11-ra-on")
        os.makedirs(self.bench)
        for name in (ab.BENCHMARK_DLL, ab.LIBRARY_DLL):
            with open(os.path.join(self.bench, name), "wb") as f:
                f.write(b"dll")
        self.artifacts = os.path.join(self._tmp.name, "work", "001-b-N.C.M")
        self.expected = {"b": ab.expected_variant(self.bench, None)}

    def tearDown(self):
        self._tmp.cleanup()

    def _launcher(self, run, startups=None, hashes=None):
        return ab._process_launcher([], self.expected, {} if startups is None else startups,
                                    {} if hashes is None else hashes, run=run, dotnet="dotnet")

    def test_a_launch_reads_its_own_report(self):
        startups = {}
        medians = self._launcher(_FakeRun(self.bench), startups)("b", self.bench, "N.C.M", self.artifacts)
        self.assertEqual(medians, {"N.C.M(Rows: 10)": 42.0})
        self.assertEqual(startups["b"]["variant"], "tfm=net11.0 runtime-async=on")

    def test_a_stale_report_is_not_read(self):
        stale = os.path.join(self.artifacts, "results")
        os.makedirs(stale)
        with open(os.path.join(stale, "N.C-report-full.json"), "w", encoding="utf-8") as f:
            json.dump({"Benchmarks": [{"FullName": "N.C.M(Rows: 10)", "Statistics": {"Median": 1.0}}]}, f)
        with self.assertRaises(RuntimeError):
            self._launcher(_FakeRun(self.bench, write_report=False))("b", self.bench, "N.C.M", self.artifacts)

    def test_a_launch_that_measures_nothing_refuses_the_run(self):
        with self.assertRaises(RuntimeError):
            self._launcher(_FakeRun(self.bench, write_report=False))("b", self.bench, "N.C.M", self.artifacts)

    def test_a_failed_process_is_reported_with_exit_code_and_log(self):
        with self.assertRaises(RuntimeError) as ctx:
            self._launcher(_FakeRun(self.bench, returncode=3))("b", self.bench, "N.C.M", self.artifacts)
        self.assertIn("exited with 3", str(ctx.exception))
        self.assertIn("console.log", str(ctx.exception))

    def test_a_wrong_variant_refuses_the_run(self):
        with self.assertRaises(RuntimeError):
            self._launcher(_FakeRun(self.bench, variant="tfm=net11.0 runtime-async=off"))(
                "b", self.bench, "N.C.M", self.artifacts)

    def test_a_library_that_changes_between_launches_refuses_the_run(self):
        launch = self._launcher(_FakeRun(self.bench))
        launch("b", self.bench, "N.C.M", self.artifacts)
        with open(os.path.join(self.bench, ab.LIBRARY_DLL), "wb") as f:
            f.write(b"rebuilt")
        with self.assertRaises(RuntimeError):
            launch("b", self.bench, "N.C.M", self.artifacts + "-2")


AOT_CASE = "ModernLibs.FileHelpers.Benchmarks.Aot.PackageAssets_ParseAndDiscard(Rows: 1000)"


def _aot_startup_log(variant="tfm=net10.0 runtime-async=off", native="yes", runtime=".NET 10.0.15"):
    return (
        "// ModernLibs.FileHelpers variant: %s\n"
        "// ModernLibs.FileHelpers library: project\n"
        "// Build: native-aot=%s app-tfm=net10.0 rid=linux-x64\n"
        "// Runtime: %s\n" % (variant, native, runtime))


def _aot_result_line(checksum="0x00000000000000aa", times=(300, 100, 200), rows=1000):
    return json.dumps({"harness": "aot", "scenario": "PackageAssets_ParseAndDiscard",
                       "variant": "tfm=net10.0 runtime-async=off", "rows": rows, "warmup": 1,
                       "iterations": len(times), "checksum": checksum, "times_ns": list(times)}) + "\n"


class AotStartupAndResultTests(unittest.TestCase):
    def test_aot_startup_has_no_assembly_line_and_must_be_native(self):
        bench = "/x/net10"
        startup = ab.check_startup(_aot_startup_log(), bench, ab.expected_variant(bench, None), aot=True)
        self.assertEqual(startup, {
            "variant": "tfm=net10.0 runtime-async=off",
            "library": "project",
            "build": "native-aot=yes app-tfm=net10.0 rid=linux-x64",
            "runtime": ".NET 10.0.15",
        })

    def test_aot_startup_refuses_a_jit_process(self):
        bench = "/x/net10"
        with self.assertRaises(RuntimeError):
            ab.check_startup(_aot_startup_log(native="no"), bench, ab.expected_variant(bench, None), aot=True)

    def test_aot_startup_refuses_another_variant_and_runtime(self):
        bench = "/x/net10"
        with self.assertRaises(RuntimeError):
            ab.check_startup(_aot_startup_log(variant="tfm=net11.0 runtime-async=on"), bench,
                             ab.expected_variant(bench, None), aot=True)
        with self.assertRaises(RuntimeError):
            ab.check_startup(_aot_startup_log(runtime=".NET 11.0.0"), bench, ab.expected_variant(bench, None), aot=True)

    def test_package_folders_have_a_runtime_and_a_library_expectation(self):
        self.assertEqual(ab.aot_expected_variant("/x/pkg-net10", None), ("tfm=net10.0 runtime-async=off", "10", "package "))
        self.assertEqual(ab.aot_expected_variant("/x/pkg-net11", "tfm=net10.0 runtime-async=off"),
                         ("tfm=net10.0 runtime-async=off", "11", "package "))
        self.assertEqual(ab.aot_expected_variant("/x/net11-ra-on", None), ("tfm=net11.0 runtime-async=on", "11", "project"))
        self.assertEqual(ab.aot_expected_variant("/x/other", "tfm=net10.0 runtime-async=off"),
                         ("tfm=net10.0 runtime-async=off", None, None))
        with self.assertRaises(ValueError):
            ab.aot_expected_variant("/x/pkg-net11", "tfm=net11.0 runtime-async=on")

    def test_aot_startup_refuses_a_package_build_on_the_wrong_runtime(self):
        # A pkg-net10 build passed where pkg-net11 was meant prints the same variant line; the runtime differs.
        log = _aot_startup_log().replace("library: project", "library: package 4.9.1")
        ab.check_startup(log, "/x/pkg-net10", ab.aot_expected_variant("/x/pkg-net10", None), aot=True)
        with self.assertRaises(RuntimeError):
            ab.check_startup(log, "/x/pkg-net11", ab.aot_expected_variant("/x/pkg-net11", None), aot=True)

    def test_aot_startup_refuses_the_wrong_library(self):
        log_package = _aot_startup_log().replace("library: project", "library: package 4.9.1")
        with self.assertRaises(RuntimeError):
            ab.check_startup(log_package, "/x/net10", ab.aot_expected_variant("/x/net10", None), aot=True)
        with self.assertRaises(RuntimeError):
            ab.check_startup(_aot_startup_log(), "/x/pkg-net10", ab.aot_expected_variant("/x/pkg-net10", None), aot=True)

    def test_a_benchmarkdotnet_log_is_not_an_aot_log(self):
        bench = "/x/net11-ra-on"
        with self.assertRaises(RuntimeError):
            ab.check_startup(_startup_log(bench), bench, ab.expected_variant(bench, None), aot=True)

    def test_aot_result_gives_the_case_name_and_the_median_of_the_iterations(self):
        result = ab.parse_aot_result(_aot_startup_log() + _aot_result_line())
        self.assertEqual(ab.aot_case_name(result), AOT_CASE)
        self.assertEqual(ab.aot_sample(result), 200.0)
        self.assertEqual(result["checksum"], "0x00000000000000aa")

    def test_aot_output_without_a_result_line_or_times_is_an_error(self):
        with self.assertRaises(RuntimeError):
            ab.parse_aot_result(_aot_startup_log())
        with self.assertRaises(RuntimeError):
            ab.aot_sample(ab.parse_aot_result(_aot_result_line(times=())))

    def test_arguments_ab_sets_itself_cannot_be_passed_to_the_aot_program(self):
        for bad in (["--expect-checksum", "0x1"], ["--checksum-only"], ["--info"], ["--data-dir", "x"]):
            with self.assertRaises(ValueError):
                ab.check_aot_args(bad)
        ab.check_aot_args(["--rows", "100000", "--warmup", "5", "--iterations", "20"])

    def test_the_filter_selects_aot_scenarios(self):
        self.assertEqual(ab.aot_methods("*"), ["ModernLibs.FileHelpers.Benchmarks.Aot.PackageAssets_ParseAndDiscard"])
        self.assertEqual(ab.aot_methods("*ParseAndDiscard*"), ab.aot_methods("*"))
        self.assertEqual(ab.aot_methods("*QuotedFields*"), [])


class _FakeAotRun:
    """Stands in for subprocess.run of the AOT program: prints the start-up lines and one result line."""

    def __init__(self, checksum="0x00000000000000aa", returncode=0, native="yes"):
        self.checksum = checksum
        self.returncode = returncode
        self.native = native
        self.commands = []

    def __call__(self, cmd, cwd, stdout, stderr, check):
        self.commands.append(cmd)
        stdout.write(_aot_startup_log(native=self.native))
        if self.returncode == 0:
            stdout.write(_aot_result_line(checksum=self.checksum))

        class Result:
            pass

        result = Result()
        result.returncode = self.returncode
        return result


class AotLauncherTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.bench = os.path.join(self._tmp.name, "net10")
        os.makedirs(self.bench)
        with open(os.path.join(self.bench, ab.AOT_EXECUTABLE), "wb") as f:
            f.write(b"\x7fELF")
        self.artifacts = os.path.join(self._tmp.name, "work", "001-b-aot")
        self.expected = {"b": ab.expected_variant(self.bench, None)}
        self.method = ab.aot_methods("*")[0]

    def tearDown(self):
        self._tmp.cleanup()

    def _launcher(self, run, startups=None, hashes=None, checksum="0x00000000000000aa"):
        return ab._aot_launcher(["--rows", "1000"], self.expected, {} if startups is None else startups,
                                {} if hashes is None else hashes, checksum, run=run)

    def test_a_launch_passes_the_expected_checksum_and_returns_the_median(self):
        run = _FakeAotRun()
        startups = {}
        medians = self._launcher(run, startups)("b", self.bench, self.method, self.artifacts)
        self.assertEqual(medians, {AOT_CASE: 200.0})
        self.assertEqual(run.commands[0][0], os.path.join(self.bench, ab.AOT_EXECUTABLE))
        self.assertEqual(run.commands[0][1:], ["--expect-checksum", "0x00000000000000aa", "--rows", "1000"])
        self.assertEqual(startups["b"]["build"], "native-aot=yes app-tfm=net10.0 rid=linux-x64")
        self.assertTrue(os.path.isfile(os.path.join(self.artifacts, "console.log")))

    def test_a_refused_launch_stops_the_run(self):
        with self.assertRaises(RuntimeError) as ctx:
            self._launcher(_FakeAotRun(returncode=3))("b", self.bench, self.method, self.artifacts)
        self.assertIn("exited with 3", str(ctx.exception))
        self.assertIn("refused", str(ctx.exception))

    def test_a_result_with_another_checksum_stops_the_run(self):
        with self.assertRaises(RuntimeError):
            self._launcher(_FakeAotRun(checksum="0x00000000000000bb"))("b", self.bench, self.method, self.artifacts)

    def test_a_jit_process_stops_the_run(self):
        with self.assertRaises(RuntimeError):
            self._launcher(_FakeAotRun(native="no"))("b", self.bench, self.method, self.artifacts)

    def test_an_executable_that_changes_between_launches_stops_the_run(self):
        launch = self._launcher(_FakeAotRun())
        launch("b", self.bench, self.method, self.artifacts)
        with open(os.path.join(self.bench, ab.AOT_EXECUTABLE), "wb") as f:
            f.write(b"\x7fELF rebuilt")
        with self.assertRaises(RuntimeError):
            launch("b", self.bench, self.method, self.artifacts + "-2")


_FAKE_AOT_PROGRAM = """#!/bin/sh
dir=$(dirname "$0")
sum=$(cat "$dir/checksum")
echo "// ModernLibs.FileHelpers variant: tfm=net10.0 runtime-async=off"
echo "// ModernLibs.FileHelpers library: $(cat "$dir/library")"
echo "// Build: native-aot=yes app-tfm=net10.0 rid=linux-x64"
echo "// Runtime: .NET 10.0.15"
expect=""
only=0
while [ $# -gt 0 ]; do
    case "$1" in
        --expect-checksum) expect="$2"; shift 2 ;;
        --checksum-only) only=1; shift ;;
        *) shift ;;
    esac
done
if [ -n "$expect" ] && [ "$expect" != "$sum" ]; then
    echo "// REFUSED, nothing timed" >&2
    exit 3
fi
if [ "$only" = 1 ]; then
    echo '{"harness":"aot","scenario":"PackageAssets_ParseAndDiscard","rows":1000,"checksum":"'"$sum"'","times_ns":[]}'
    exit 0
fi
echo "timed" >> "$dir/timed.log"
echo '{"harness":"aot","scenario":"PackageAssets_ParseAndDiscard","rows":1000,"checksum":"'"$sum"'","times_ns":[100,110,105]}'
"""


@unittest.skipUnless(os.name == "posix", "runs a shell script as the AOT program")
class AotMainTests(unittest.TestCase):
    """ab.main --aot end to end, with a shell script in place of the native program."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.dirs = {}
        # Every fake program prints a .NET 10 runtime, so the pkg-net11 folder is a package build on the wrong
        # runtime (a pkg-net10 build copied or passed under the pkg-net11 name).
        for name, library in (("pkg-net10", "package 4.9.1"), ("net10", "project"), ("pkg-net11", "package 4.9.1")):
            folder = os.path.join(self._tmp.name, name)
            os.makedirs(folder)
            exe = os.path.join(folder, ab.AOT_EXECUTABLE)
            with open(exe, "w", encoding="utf-8") as f:
                # One comment line per folder: the executables differ, as two real builds do.
                f.write(_FAKE_AOT_PROGRAM + "# %s\n" % name)
            os.chmod(exe, 0o755)
            files = {
                "checksum": "0x00000000000000aa\n",
                "library": library + "\n",
                ab.VARIANT_FILE: "%s: tfm=net10.0 runtime-async=off\ncommit=aaa\nsdk=11.0.100\n" % name,
            }
            for file_name, text in files.items():
                with open(os.path.join(folder, file_name), "w", encoding="utf-8") as f:
                    f.write(text)
            self.dirs[name] = folder
        self.out = os.path.join(self._tmp.name, "aot.json")

    def tearDown(self):
        self._tmp.cleanup()

    def _main(self, *extra):
        return ab.main(["--aot", "--a", self.dirs["pkg-net10"], "--b", self.dirs["net10"],
                        "--expect-a", "tfm=net10.0 runtime-async=off", "--launches", "7", "--out", self.out]
                       + list(extra))

    def _timed(self, name):
        path = os.path.join(self.dirs[name], "timed.log")
        if not os.path.exists(path):
            return 0
        with open(path, encoding="utf-8") as f:
            return len(f.readlines())

    def test_seven_interleaved_launches_give_a_b4_document(self):
        self.assertEqual(self._main("--", "--rows", "1000"), 0)
        with open(self.out, encoding="utf-8") as f:
            document = json.load(f)
        self.assertTrue(document["b4"])
        self.assertEqual(document["harness"], "aot")
        self.assertEqual(document["aot_checksum"], "0x00000000000000aa")
        self.assertEqual(document["program_args"], ["--rows", "1000"])
        self.assertEqual(document["a"]["startup"]["library"], "package 4.9.1")
        self.assertEqual(document["b"]["startup"]["library"], "project")
        [row] = document["benchmarks"]
        self.assertEqual(row["name"], AOT_CASE)
        self.assertEqual(row["a_median_ns"], [105.0] * 7)
        self.assertEqual(row["verdict"], "no-change")
        self.assertEqual((self._timed("pkg-net10"), self._timed("net10")), (7, 7))

    def _document(self):
        with open(self.out, encoding="utf-8") as f:
            return json.load(f)

    def test_smoke_in_aot_mode_is_never_b4(self):
        # Lead ruling R21 / R20 in AOT mode (review-T47, Minor 3).
        self.assertEqual(ab.main(["--aot", "--a", self.dirs["pkg-net10"], "--b", self.dirs["net10"],
                                  "--launches", "5", "--smoke", "--out", self.out]), 0)
        document = self._document()
        self.assertFalse(document["b4"])
        self.assertEqual(document["not_b4_reason"], "smoke")

    def test_allow_dirty_in_aot_mode_is_never_b4(self):
        self.assertEqual(self._main("--allow-dirty"), 0)
        document = self._document()
        self.assertFalse(document["b4"])
        self.assertIn("uncommitted changes", document["not_b4_reason"])

    def test_the_same_executable_on_both_sides_is_refused(self):
        with self.assertRaises(SystemExit):
            ab.main(["--aot", "--a", self.dirs["net10"], "--b", self.dirs["net10"], "--launches", "7",
                     "--out", self.out])
        with open(os.path.join(self.dirs["net10"], ab.AOT_EXECUTABLE), "w", encoding="utf-8") as f:
            f.write(_FAKE_AOT_PROGRAM + "# pkg-net10\n")
        with self.assertRaises(SystemExit):
            self._main()
        self.assertEqual((self._timed("pkg-net10"), self._timed("net10")), (0, 0))

    def test_a_package_folder_on_the_wrong_runtime_is_refused_before_timing(self):
        with self.assertRaises(RuntimeError) as ctx:
            ab.main(["--aot", "--a", self.dirs["pkg-net11"], "--b", self.dirs["net10"], "--launches", "7",
                     "--out", self.out])
        self.assertIn("expected .NET 11", str(ctx.exception))
        self.assertEqual((self._timed("pkg-net11"), self._timed("net10")), (0, 0))

    def test_a_checksum_that_differs_between_the_builds_refuses_to_time(self):
        with open(os.path.join(self.dirs["net10"], "checksum"), "w", encoding="utf-8") as f:
            f.write("0x00000000000000bb\n")
        with self.assertRaises(RuntimeError) as ctx:
            self._main()
        self.assertIn("checksum", str(ctx.exception))
        self.assertEqual((self._timed("pkg-net10"), self._timed("net10")), (0, 0))
        self.assertFalse(os.path.exists(self.out))

    def test_aot_mode_still_needs_seven_launches_for_b4(self):
        with self.assertRaises(SystemExit):
            ab.main(["--aot", "--a", self.dirs["pkg-net10"], "--b", self.dirs["net10"],
                     "--expect-a", "tfm=net10.0 runtime-async=off", "--launches", "6", "--out", self.out])

    def test_benchmarkdotnet_mode_still_needs_a_filter(self):
        with self.assertRaises(SystemExit):
            ab.main(["--a", self.dirs["net10"], "--b", self.dirs["net10"], "--launches", "7", "--out", self.out])


class VariantFileTests(unittest.TestCase):
    CLEAN_A = "net11-ra-off: tfm=net11.0 runtime-async=off\ncommit=aaa\nsdk=11.0.100\n"
    CLEAN_B = "net11-ra-on: tfm=net11.0 runtime-async=on\ncommit=aaa\nsdk=11.0.100\n"
    DIRTY_B = "net11-ra-on: tfm=net11.0 runtime-async=on\ncommit=aaa (uncommitted changes)\nsdk=11.0.100\n"
    OTHER_B = "net11-ra-on: tfm=net11.0 runtime-async=on\ncommit=bbb\nsdk=11.0.100\n"

    def test_same_clean_commit_gives_no_warning(self):
        self.assertEqual(ab.check_variant_files(self.CLEAN_A, self.CLEAN_B, allow_dirty=False), [])

    def test_a_build_from_uncommitted_changes_is_refused(self):
        with self.assertRaises(ValueError):
            ab.check_variant_files(self.CLEAN_A, self.DIRTY_B, allow_dirty=False)
        self.assertEqual(len(ab.check_variant_files(self.CLEAN_A, self.DIRTY_B, allow_dirty=True)), 1)

    def test_different_commits_give_a_warning(self):
        warnings = ab.check_variant_files(self.CLEAN_A, self.OTHER_B, allow_dirty=False)
        self.assertEqual(len(warnings), 1)
        self.assertIn("commit", warnings[0])

    def test_a_missing_variant_file_gives_a_warning(self):
        self.assertEqual(len(ab.check_variant_files(None, self.CLEAN_B, allow_dirty=False)), 1)


if __name__ == "__main__":
    unittest.main()
