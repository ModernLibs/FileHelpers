"""Tests for round-report.py (T12). Run from the repository root:

    python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'
"""

import contextlib
import importlib.util
import io
import json
import os
import sys
import tempfile
import unittest

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True   # no __pycache__ next to the tools (the clone of a round stays clean)
sys.path.insert(0, _HERE)

import ab  # noqa: E402  (the path above makes the module next to this file importable under -I)

# round-report.py has a dash in its name (it is a script), so it is loaded from its path.
_spec = importlib.util.spec_from_file_location("round_report", os.path.join(_HERE, "round-report.py"))
round_report = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(round_report)

HEADER = "| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |"
NS = "ModernLibs.FileHelpers.Benchmarks.Benchmarks."
QUOTED = NS + "QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard"
BASE = [100.0, 101.0, 99.0, 100.5, 99.5, 100.2, 99.8]


def scaled(factor, base=BASE):
    return [x * factor for x in base]


def bench(name, a, b):
    row = {"name": name, "a_median_ns": a, "b_median_ns": b}
    row.update(ab.compare(a, b))
    return row


def doc(a_dir, b_dir, glob, rows, b4=True):
    return {
        "b4": b4,
        "not_b4_reason": None if b4 else "smoke",
        "warnings": [],
        "a": {"dir": a_dir, "variant": None, "startup": None, "library_sha256": None},
        "b": {"dir": b_dir, "variant": None, "startup": None, "library_sha256": None},
        "launches": len(rows[0]["a_median_ns"]),
        "filter": glob,
        "bdn_args": [],
        "only_in_a": [],
        "only_in_b": [],
        "benchmarks": rows,
    }


def write(path, document):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(document, f)


class FixtureRound:
    """raw/<round>/vm1 with three ab.py result files: v410-net10, free-gains and ra."""

    def __init__(self, root):
        self.round = os.path.join(root, "raw", "r1")
        vm = os.path.join(self.round, "vm1")
        bin_dir = "/work/bin/"
        q10 = QUOTED + "(Rows: 10000)"
        q100 = QUOTED + "(Rows: 100000)"
        write(os.path.join(vm, "v410-net10", "003-quoted-x.json"), doc(
            bin_dir + "pkg-net10", bin_dir + "net10", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(q10, scaled(1_000), scaled(950)), bench(q100, scaled(10_000), scaled(9_500))]))
        write(os.path.join(vm, "free-gains", "003-quoted-x.json"), doc(
            bin_dir + "pkg-net10", bin_dir + "pkg-net11", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(q10, scaled(1_000), scaled(900)), bench(q100, scaled(10_000), scaled(9_000))]))
        write(os.path.join(vm, "ra", "003-quoted-x.json"), doc(
            bin_dir + "net11-ra-off", bin_dir + "net11-ra-on", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(q10, scaled(800), scaled(720)), bench(q100, scaled(8_000), scaled(8_080))]))


def table_rows(markdown, header):
    """The data rows of the first table under the given header row."""
    lines = markdown.splitlines()
    start = lines.index(header)
    rows = []
    for line in lines[start + 2:]:
        if not line.startswith("|"):
            break
        rows.append([cell.strip() for cell in line.strip("|").split("|")])
    return rows


class BuildTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fixture = FixtureRound(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_scenario_table_has_header_one_row_per_scenario_and_verdicts(self):
        markdown = round_report.build(self.fixture.round)
        self.assertIn(HEADER, markdown)
        rows = table_rows(markdown, HEADER)
        self.assertEqual(len(rows), 2)
        by_name = {row[0]: row for row in rows}
        r100 = by_name["QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K)"]
        self.assertTrue(r100[5].startswith("1.010"), r100[5])
        self.assertEqual(r100[6], "no-change")
        r10 = by_name["QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K)"]
        self.assertTrue(r10[5].startswith("0.900"), r10[5])
        self.assertEqual(r10[6], "faster")

    def test_all_five_value_columns_are_filled(self):
        rows = table_rows(round_report.build(self.fixture.round), HEADER)
        for row in rows:
            for cell in row[1:6]:
                self.assertNotIn(cell, ("", "-"), row)
        r100 = [row for row in rows if "100K" in row[0]][0]
        self.assertEqual(r100[1], "1.000 ms / 900.0 μs")    # 4.9.1 package on .NET 10 / on .NET 11
        self.assertEqual(r100[2], "950.0 μs")
        self.assertEqual(r100[3], "808.0 μs")
        self.assertEqual(r100[4], "800.0 μs")

    def test_free_gains_table_uses_the_package_pair(self):
        markdown = round_report.build(self.fixture.round)
        a, b = "4.9.1 on .NET 10", "4.9.1 on .NET 11"
        rows = table_rows(markdown, round_report.PAIR_GC_HEADER % (a, b, "11/10", a, b))
        self.assertEqual(len(rows), 2)
        self.assertTrue(all(row[4] == "faster" for row in rows))
        self.assertTrue(all(row[5] == "-" and row[6] == "-" for row in rows))   # no .runs folders in this fixture

    def test_gc_columns_come_from_the_benchmarkdotnet_reports(self):
        # One launch per side of the free-gains result, with the memory block BenchmarkDotNet writes.
        result = os.path.join(self.fixture.round, "vm1", "free-gains", "003-quoted-x.json")
        for side, gen0, alloc in (("a", 140, 2_355_344), ("b", 120, 2_000_000)):
            report = {"Benchmarks": [{
                "FullName": QUOTED + "(Rows: 10000)",
                "Memory": {"Gen0Collections": gen0, "Gen1Collections": 20, "Gen2Collections": 0,
                           "TotalOperations": 1000, "BytesAllocatedPerOperation": alloc}}]}
            write(os.path.join(result + ".runs", "001-%s-%s" % (side, QUOTED), "results", "x-report-full.json"),
                  report)
        markdown = round_report.build(self.fixture.round)
        a, b = "4.9.1 on .NET 10", "4.9.1 on .NET 11"
        rows = {row[0]: row for row in table_rows(markdown, round_report.PAIR_GC_HEADER % (a, b, "11/10", a, b))}
        r10 = rows["QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K)"]
        self.assertEqual(r10[5], "140 / 20 / -; 2.246 MB")
        self.assertEqual(r10[6], "120 / 20 / -; 1.907 MB")
        gc_rows = table_rows(markdown, "| Scenario | Build | Gen0 | Gen1 | Gen2 | Allocated per operation |")
        self.assertIn(["QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 10K)", "pkg-net11", "120", "20", "-",
                       "1.907 MB"], gc_rows)

    def test_failed_runs_and_methods_of_one_build_are_listed(self):
        vm = os.path.join(self.fixture.round, "vm1")
        with open(os.path.join(vm, "failed.txt"), "w", encoding="utf-8") as f:
            f.write("v410-net11\tprototype\t*Prototypes.R6UpperBoundBenchmarks*\tv410-net11/040-prototype-x.log\n")
        path = os.path.join(vm, "ra", "003-quoted-x.json")
        with open(path, encoding="utf-8") as f:
            document = json.load(f)
        document["only_in_b"] = [NS + "QuotedFieldsBenchmarks.NewMethod"]
        write(path, document)
        markdown = round_report.build(self.fixture.round)
        self.assertIn("1 ab.py run(s) failed on this VM", markdown)
        failed = table_rows(markdown, "| Pair | Group | Glob | Log |")
        self.assertEqual(failed, [["v410-net11", "prototype", "`*Prototypes.R6UpperBoundBenchmarks*`",
                                   "`v410-net11/040-prototype-x.log`"]])
        not_run = table_rows(markdown, "| Pair | Group | Glob | Reason |")
        self.assertEqual(len(not_run), 1)
        self.assertIn("only in B (net11-ra-on): %sQuotedFieldsBenchmarks.NewMethod" % NS, not_run[0][3])


class MachineTests(unittest.TestCase):
    def test_machine_facts(self):
        text = ("mode: round (B4: 7 launches per side, job default, max iterations 20)\n"
                "commit: 0123456789abcdef (requested as HEAD)\n"
                "== uname -a\nLinux vm 6.8.0-1017-azure #20 SMP x86_64 GNU/Linux\n"
                'PRETTY_NAME="Ubuntu 24.04.1 LTS"\n'
                "Architecture:            x86_64\nCPU(s):                  8\n"
                "Model name:              AMD EPYC 7763 64-Core Processor\n"
                "Flags:                   fpu sse avx2 avx512f\n"
                ".NET SDK:\n Version:           11.0.100\n"
                "  Microsoft.NETCore.App 10.0.15 [/x]\n  Microsoft.NETCore.App 11.0.0 [/x]\n")
        f = round_report.machine_facts(text)
        self.assertEqual((f["cpu"], f["arch"], f["cpus"], f["simd"]),
                         ("AMD EPYC 7763 64-Core Processor", "x86_64", "8", "avx2, avx512f"))
        self.assertEqual((f["os"], f["kernel"], f["sdk"], f["runtimes"]),
                         ("Ubuntu 24.04.1 LTS", "6.8.0-1017-azure", "11.0.100", "10.0.15, 11.0.0"))
        self.assertEqual(f["mode"], "round (B4: 7 launches per side, job default, max iterations 20)")


class FormatTests(unittest.TestCase):
    def test_time_units(self):
        self.assertEqual(round_report.fmt_time(950.0), "950.0 ns")
        self.assertEqual(round_report.fmt_time(12_345.0), "12.35 μs")
        self.assertEqual(round_report.fmt_time(1_000_000.0), "1.000 ms")
        self.assertEqual(round_report.fmt_time(2_500_000_000.0), "2.500 s")
        self.assertEqual(round_report.fmt_time(None), "-")

    def test_case_names(self):
        self.assertEqual(round_report.case_label(QUOTED + "(Rows: 100000)"),
                         "QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard (Rows 100K)")
        self.assertEqual(round_report.case_label(NS + "Components.UnquoteBenchmarks.Unquote_Current(Values: DoubledOnly)"),
                         "UnquoteBenchmarks.Unquote_Current (Values DoubledOnly)")
        self.assertEqual(round_report.case_label(NS + "Components.ConverterCallShapeBenchmarks.Format_DirectCall"),
                         "ConverterCallShapeBenchmarks.Format_DirectCall")


class GateAndGapTests(unittest.TestCase):
    """Gate R6 (ruling R32), smoke marking, and scenarios that did not run."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.round = os.path.join(self.tmp.name, "raw", "r2")
        self.vm = os.path.join(self.round, "vm1")
        os.makedirs(self.vm)

    def tearDown(self):
        self.tmp.cleanup()

    def r6(self, pair, a, b, ratio_precomputed, ratio_fast, b4=True):
        rows = []
        for method, factor in (("R6_Current", 1.0), ("R6_PrecomputedEnds", ratio_precomputed),
                               ("R6_PrecomputedEnds_FastUnquote", ratio_fast)):
            name = NS + "Prototypes.R6UpperBoundBenchmarks.%s(Rows: 100000)" % method
            rows.append(bench(name, scaled(1_000_000), scaled(1_000_000 * factor)))
        write(os.path.join(self.vm, pair, "040-prototype-r6.json"),
              doc("/w/bin/" + a, "/w/bin/" + b, "*Prototypes.R6UpperBoundBenchmarks*", rows, b4))

    def g2(self, ratio, noisy=False):
        rows = []
        for rows_value in (10_000, 100_000):
            base = scaled(1_000_000)
            current = NS + "Prototypes.G2PrototypeBenchmarks.G2_Current(Rows: %d)" % rows_value
            span = NS + "Prototypes.G2PrototypeBenchmarks.G2_SingleSpan(Rows: %d)" % rows_value
            variant = [x * ratio for x in base]
            if noisy:          # the same median ratio, but launches spread so wide that the interval includes 1.0
                variant = [x * f for x, f in zip(variant, (0.5, 1.6, 0.6, 1.5, 0.7, 1.4, 1.0))]
            rows.append(bench(current, base, base))
            rows.append(bench(span, base, variant))
        write(os.path.join(self.vm, "v410-net11", "041-prototype-g2.json"),
              doc("/w/bin/pkg-net11", "/w/bin/net11-ra-on", "*Prototypes.G2PrototypeBenchmarks*", rows))

    def gate_cell(self, markdown, header, build, rows_label):
        for row in table_rows(markdown, header):
            if row[0] == build and row[1] == rows_label:
                return row[-1]
        return None

    G2_HEADER = "| Build | Rows | G2_Current | G2_SingleSpan | ratio [99.9 % CI] | gate (at least 10 % faster) |"

    def test_g2_gate_meets_at_100k_only_and_prints_the_p5_note(self):
        self.g2(0.88)
        markdown = round_report.build(self.round)
        self.assertEqual(self.gate_cell(markdown, self.G2_HEADER, "net11-ra-on", "Rows 100K"), "meets")
        self.assertEqual(self.gate_cell(markdown, self.G2_HEADER, "net11-ra-on", "Rows 10K"), "(informative)")
        self.assertIn(round_report.G2_P5_NOTE, markdown)

    def test_g2_gate_misses_above_0_90(self):
        self.g2(0.92)
        markdown = round_report.build(self.round)
        self.assertEqual(self.gate_cell(markdown, self.G2_HEADER, "net11-ra-on", "Rows 100K"), "misses")

    def test_a_ratio_below_the_gate_without_a_faster_verdict_misses(self):
        self.g2(0.85, noisy=True)
        markdown = round_report.build(self.round)
        self.assertEqual(self.gate_cell(markdown, self.G2_HEADER, "net11-ra-on", "Rows 100K"), "misses")
        self.r6("v410-net11", "pkg-net11", "net11-ra-on", 0.90, 0.85)
        r = round_report.compare_methods(round_report.load(self.round)[0], "net11-ra-on", "G2PrototypeBenchmarks",
                                         "G2_Current", "G2_SingleSpan", {"Rows": "100000"})
        self.assertLessEqual(r["median_ratio"], round_report.G2_GATE)
        self.assertNotEqual(r["verdict"], "faster")

    def test_a_capped_round_with_a_gate_ratio_under_the_limit_but_no_verdict_asks_for_an_uncapped_rerun(self):
        self.g2(0.85, noisy=True)
        machine = os.path.join(self.vm, "machine.txt")
        with open(machine, "w", encoding="utf-8") as f:
            f.write("mode: round (B4: 7 launches per side, job default, max iterations 20)\n")
        self.assertIn("rerun the gate lines uncapped", round_report.build(self.round))
        self.assertIn("G2 on net11-ra-on", round_report.build(self.round))
        with open(machine, "w", encoding="utf-8") as f:
            f.write("mode: round (B4: 7 launches per side, job default)\n")          # uncapped: no such note
        self.assertNotIn("rerun the gate lines uncapped", round_report.build(self.round))

    def test_r6_gate_both_variants_meet(self):
        self.r6("v410-net11", "pkg-net11", "net11-ra-on", 0.90, 0.85)
        markdown = round_report.build(self.round)
        self.assertIn("R6_PrecomputedEnds meets the 6 % gate on net11-ra-on", markdown)
        self.assertNotIn("different sides of 6 %", markdown)
        self.assertNotIn(HEADER, markdown)      # prototype methods are not scenario rows

    def test_r6_variants_on_different_sides_leave_the_decision_to_the_owner(self):
        self.r6("v410-net11", "pkg-net11", "net11-ra-on", 0.96, 0.90)
        markdown = round_report.build(self.round)
        self.assertIn("R6_PrecomputedEnds misses the 6 % gate on net11-ra-on", markdown)
        self.assertIn("different sides of 6 %", markdown)
        self.assertIn("decision is the owner's", markdown)

    def test_smoke_results_are_marked(self):
        self.r6("v410-net11", "pkg-net11", "net11-ra-on", 0.90, 0.85, b4=False)
        markdown = round_report.build(self.round)
        self.assertIn("not B4", markdown)

    def test_missing_scenario_is_reported_as_not_run(self):
        self.r6("v410-net11", "pkg-net11", "net11-ra-on", 0.90, 0.85)
        with open(os.path.join(self.vm, "filters.txt"), "w", encoding="utf-8") as f:
            f.write("# comment\n"
                    "prototype  v410-net11  *Prototypes.R6UpperBoundBenchmarks*\n"
                    "async      ra,v410-net10  *EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null\n")
        markdown = round_report.build(self.round)
        self.assertRegex(markdown, r"EngineAsyncBenchmarks\.Engine_WriteStreamAsync_Null[^\n]*not run")

    def test_a_line_with_two_groups_fills_both_tables(self):
        glob = "*EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows"
        name = NS + "EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows(Rows: 100000)"
        write(os.path.join(self.vm, "ra", "060-async+exception-x.json"),
              doc("/w/bin/net11-ra-off", "/w/bin/net11-ra-on", glob, [bench(name, scaled(1_000), scaled(1_010))]))
        with open(os.path.join(self.vm, "filters.txt"), "w", encoding="utf-8") as f:
            f.write("async,exception ra,runtime-410 %s\n" % glob)
        markdown = round_report.build(self.round)
        label = "EngineAsyncBenchmarks.Engine_WriteStreamAsync_Null_SourceThrows (Rows 100K)"
        for title in ("### Async", "### Exception path"):
            section = markdown.split(title + "\n", 1)[1].split("\n### ", 1)[0]
            self.assertIn(label, section, title)


class AotTests(unittest.TestCase):
    """ab.py --aot results (T47): their own table, never mixed with the BenchmarkDotNet folders of the same name."""

    AOT_CASE = "ModernLibs.FileHelpers.Benchmarks.Aot.PackageAssets_ParseAndDiscard(Rows: 100000)"
    GLOB = "*PackageAssets_ParseAndDiscard"

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.round = os.path.join(self.tmp.name, "raw", "r3")
        vm = os.path.join(self.round, "vm1")
        bdn = "/w/bin/"
        aot = "/w/bin/aot/"
        for pair, a, b, fa, fb in (("aot-v410-net10", "pkg-net10", "net10", 10_000, 9_000),
                                   ("aot-v410-net11", "pkg-net11", "net11-ra-on", 9_500, 8_000),
                                   ("aot-v410-net11-ra-off", "pkg-net11", "net11-ra-off", 9_500, 8_100),
                                   ("aot-ra", "net11-ra-off", "net11-ra-on", 8_100, 8_000)):
            document = doc(aot + a, aot + b, self.GLOB, [bench(self.AOT_CASE, scaled(fa), scaled(fb))])
            document["harness"] = "aot"
            write(os.path.join(vm, pair, "090-aot-x.json"), document)
        # A BenchmarkDotNet result for folders with the same names: it must not take the AOT values.
        write(os.path.join(vm, "ra", "006-quoted-x.json"), doc(
            bdn + "net11-ra-off", bdn + "net11-ra-on", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(QUOTED + "(Rows: 100000)", scaled(50_000), scaled(50_000))]))
        with open(os.path.join(vm, "filters.txt"), "w", encoding="utf-8") as f:
            f.write("quoted ra *QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard\n"
                    "aot aot-v410-net10,aot-v410-net11,aot-v410-net11-ra-off,aot-ra %s\n" % self.GLOB)

    def tearDown(self):
        self.tmp.cleanup()

    def aot_section(self):
        markdown = round_report.build(self.round)
        return markdown.split("### AOT read\n", 1)[1].split("\n### ", 1)[0]

    def test_aot_table_has_every_column(self):
        section = self.aot_section()
        rows = table_rows(section, HEADER)
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertEqual(row[0], "Aot.PackageAssets_ParseAndDiscard (Rows 100K)")
        self.assertEqual(row[1], "1.000 ms / 950.0 μs")
        self.assertEqual(row[2:5], ["900.0 μs", "800.0 μs", "810.0 μs"])
        self.assertTrue(row[5].startswith("0.988"), row[5])

    def test_aot_results_stay_out_of_the_benchmarkdotnet_tables(self):
        markdown = round_report.build(self.round)
        before_aot = markdown.split("### AOT read\n", 1)[0]
        self.assertNotIn("PackageAssets_ParseAndDiscard", before_aot)
        quoted = table_rows(markdown, HEADER)[0]          # the first scenario table is the quoted one
        self.assertEqual(quoted[3], "5.000 ms")           # net11-ra-on of the BenchmarkDotNet folder only

    def test_aot_section_names_the_missing_control_build(self):
        section = self.aot_section()
        self.assertIn("compiler and SDK effect", section)
        self.assertIn("4.9.1 pkg (AOT)", section)       # the per-runtime pair tables


class SectionTests(unittest.TestCase):
    """runtime_async, slope, r6_against_package (review-T12 Minor 8) and damaged result files (rereview Minor 2)."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.round = os.path.join(self.tmp.name, "raw", "r4")
        self.vm = os.path.join(self.round, "vm1")
        os.makedirs(self.vm)

    def tearDown(self):
        self.tmp.cleanup()

    def filters(self, text):
        with open(os.path.join(self.vm, "filters.txt"), "w", encoding="utf-8") as f:
            f.write(text)

    def load_vm(self):
        return round_report.load(self.round)[0]

    def test_runtime_async_lists_slower_cases_against_control_noise(self):
        sylvan = NS + "DelimitedParseBenchmarks.Sylvan_ParseAndDiscard(Rows: 10000)"
        write(os.path.join(self.vm, "ra", "030-control-x.json"), doc(
            "/w/bin/net11-ra-off", "/w/bin/net11-ra-on", "*Sylvan*", [bench(sylvan, scaled(1_000), scaled(1_020))]))
        slow = NS + "EngineAsyncBenchmarks.Engine_ReadAsync_Path(Rows: 10000)"
        small = NS + "EngineAsyncBenchmarks.Engine_ReadAsync_Path(Rows: 100000)"
        write(os.path.join(self.vm, "ra", "020-async-x.json"), doc(
            "/w/bin/net11-ra-off", "/w/bin/net11-ra-on", "*Engine_ReadAsync_Path",
            [bench(slow, scaled(1_000), scaled(1_100)), bench(small, scaled(1_000), scaled(1_010))]))
        self.filters("async ra *Engine_ReadAsync_Path\ncontrol ra *Sylvan*\n")
        text = "\n".join(round_report.runtime_async(self.load_vm()))
        self.assertIn("**net11-ra-off -> net11-ra-on**: 3 cases; 2 no-change, 1 slower.", text)
        self.assertIn("largest |on/off - 1| of 1 control cases = 2.0 %", text)
        rows = table_rows(text, "| Slower with runtime async on | on/off [99.9 % CI] | verdict | beyond control noise |")
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0][0], "EngineAsyncBenchmarks.Engine_ReadAsync_Path (Rows 10K)")
        self.assertTrue(rows[0][1].startswith("1.100"))
        self.assertEqual(rows[0][2:], ["slower", "yes"])

    def test_slope_gives_ns_per_row_and_the_growth(self):
        name = NS + "DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard(Rows: %d)"
        write(os.path.join(self.vm, "v410-net10", "001-read25-x.json"), doc(
            "/w/bin/pkg-net10", "/w/bin/net10", "*DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(name % 10_000, scaled(10_000), scaled(9_000)),           # 1,000,000 ns: 100 ns per row
             bench(name % 100_000, scaled(150_000), scaled(120_000)),      # 15,000,000 ns: 150 ns per row
             bench(name % 1_000_000, scaled(2_000_000), scaled(2_000_000))]))
        self.filters("read25 v410-net10 *DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard\n")
        rows = {(r[0], r[1]): r for r in table_rows(
            "\n".join(round_report.slope(self.load_vm())),
            "| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K "
            "| 10K: Gen0 / Gen1 / Gen2; allocated | 100K: Gen0 / Gen1 / Gen2; allocated |")}
        pkg = rows[("DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard", "pkg-net10")]
        self.assertEqual(pkg[2:5], ["100.0", "150.0", "1.500"])
        net10 = rows[("DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard", "net10")]
        self.assertEqual(net10[2:5], ["90.0", "120.0", "1.333"])
        self.assertEqual(len(rows), 2)                                     # the 1M row is not a slope point

    def test_r6_against_package(self):
        r6 = NS + "Prototypes.R6UpperBoundBenchmarks.%s(Rows: 100000)"
        write(os.path.join(self.vm, "v410-net11", "040-prototype-x.json"), doc(
            "/w/bin/pkg-net11", "/w/bin/net11-ra-on", "*Prototypes.R6UpperBoundBenchmarks*",
            [bench(r6 % "R6_Current", scaled(10_000), scaled(10_000)),
             bench(r6 % "R6_PrecomputedEnds", scaled(10_000), scaled(9_000))]))
        write(os.path.join(self.vm, "free-gains", "003-quoted-x.json"), doc(
            "/w/bin/pkg-net10", "/w/bin/pkg-net11", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(QUOTED + "(Rows: 100000)", scaled(11_000), scaled(10_500))]))
        self.filters("prototype v410-net11 *Prototypes.R6UpperBoundBenchmarks*\n"
                     "quoted free-gains *QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard\n")
        text = "\n".join(round_report.r6_against_package(self.load_vm()))
        rows = table_rows(text, "| Pair | Rows | R6_Current 4.9.1 pkg | R6_Current 4.10 | 4.10/pkg [99.9 % CI] | verdict "
                                "| R6_PrecomputedEnds 4.10 / R6_Current pkg [99.9 % CI] | quoted parse-and-discard, 4.9.1 pkg |")
        self.assertEqual(len(rows), 1)
        row = rows[0]
        self.assertEqual(row[:4], ["pkg-net11 -> net11-ra-on", "Rows 100K", "1.000 ms", "1.000 ms"])
        self.assertTrue(row[4].startswith("1.000"))
        self.assertEqual(row[5], "no-change")
        self.assertTrue(row[6].startswith("0.900") and row[6].endswith("faster"), row[6])
        self.assertEqual(row[7], "1.050 ms")                               # the package on .NET 11, same work

    def test_a_damaged_result_file_is_listed_not_fatal(self):
        write(os.path.join(self.vm, "ra", "006-quoted-x.json"), doc(
            "/w/bin/net11-ra-off", "/w/bin/net11-ra-on", "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard",
            [bench(QUOTED + "(Rows: 10000)", scaled(1_000), scaled(1_000))]))
        with open(os.path.join(self.vm, "ra", "007-async-x.json"), "w", encoding="utf-8") as f:
            f.write('{"b4": true, "a": {"dir": "/w/bin/net11-ra-off"')     # cut off by a kill
        markdown = round_report.build(self.round)
        self.assertIn("### Damaged results", markdown)
        rows = table_rows(markdown, "| Result file | Reason |")
        self.assertEqual([r[0] for r in rows], ["`%s`" % os.path.join("ra", "007-async-x.json")])
        self.assertEqual(len(table_rows(markdown, HEADER)), 1)             # the good file is still reported


class T59Tests(unittest.TestCase):
    """T59: Format_WriterDirectCall in the call-shape table, the base-commit pairs, the library comparison."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.round = os.path.join(self.tmp.name, "raw", "r3")
        self.vm = os.path.join(self.round, "vm1")
        os.makedirs(self.vm)

    def tearDown(self):
        self.tmp.cleanup()

    def test_format_writer_direct_call_is_in_the_converter_call_shape_table(self):
        cls = NS + "Components.ConverterCallShapeBenchmarks."
        rows = [bench(cls + method, scaled(1_000), scaled(1_000 * factor))
                for method, factor in (("Format_GenericHelper", 1.0), ("Format_GenericWriter", 0.9),
                                       ("Format_DirectCall", 0.8), ("Format_WriterDirectCall", 0.7))]
        write(os.path.join(self.vm, "runtime-410", "050-component-x.json"),
              doc("/w/bin/net10", "/w/bin/net11-ra-on", "*Components.ConverterCallShapeBenchmarks*", rows))
        markdown = round_report.build(self.round)
        self.assertIn("converter call shapes for the write path: Format_WriterDirectCall against Format_GenericHelper", markdown)
        self.assertIn("converter call shapes for the write path: Format_DirectCall against Format_GenericHelper", markdown)

    def test_the_t22_reopening_row_and_the_g5_row(self):
        cls = NS + "Components.ConverterCallShapeBenchmarks."
        rows = [bench(cls + m, scaled(1_000), scaled(1_000 * f)) for m, f in
                (("Format_GenericHelper", 1.0), ("Format_GenericWriter", 1.0), ("Format_WriterDirectCall", 1.1))]
        write(os.path.join(self.vm, "runtime-410", "050-component-x.json"),
              doc("/w/bin/net10", "/w/bin/net11-ra-on", "*Components.ConverterCallShapeBenchmarks*", rows))
        slice_ns = NS + "Components.FixedSliceBenchmarks."
        rows = [bench(slice_ns + m, scaled(1_000), scaled(1_000 * f)) for m, f in
                (("Slice_MathMin", 1.0), ("Slice_Direct", 0.9))]
        write(os.path.join(self.vm, "runtime-410", "051-component-y.json"),
              doc("/w/bin/net10", "/w/bin/net11-ra-on", "*FixedSliceBenchmarks*", rows))
        markdown = round_report.build(self.round)
        # the fixture scales only the net11-ra-on side (the B side of the pair), so net10 shows 1.000
        for build, ratio in (("net10", "1.000"), ("net11-ra-on", "1.100")):  # both runtimes
            row = [l for l in markdown.splitlines()
                   if "Format_WriterDirectCall against Format_GenericWriter" in l and "| %s |" % build in l]
            self.assertEqual(len(row), 1, build)
            self.assertIn(ratio, row[0])
        self.assertIn("converter call shapes for the write path: Format_WriterDirectCall against Format_GenericHelper",
                      markdown)                                             # the existing row is unchanged
        g5 = [l for l in markdown.splitlines() if "FixedSliceBenchmarks.Slice_Direct against Slice_MathMin" in l]
        self.assertEqual(len(g5), 2)                                        # net10 and net11-ra-on
        self.assertTrue(any("0.900" in l for l in g5))

    def test_t23_component_rows_quote_and_format_record_writer(self):
        quote = NS + "Components.QuoteBenchmarks."
        rows = [bench(quote + m, scaled(1_000), scaled(1_000 * f)) for m, f in
                (("Write_Only", 1.0), ("Quote_Clean", 1.2), ("Quote_WithQuotes", 1.4),
                 ("WriteQuoted_Clean", 1.1), ("WriteQuoted_WithQuotes", 1.3))]
        write(os.path.join(self.vm, "runtime-410", "052-component-q.json"),
              doc("/w/bin/net10", "/w/bin/net11-ra-on", "*Components.QuoteBenchmarks*", rows))
        fmt = NS + "Components.FormatOneRecordBenchmarks."
        rows = [bench(fmt + m, scaled(1_000), scaled(1_000 * f)) for m, f in
                (("FormatRecord_Current", 1.0), ("FormatRecord_SingleSpan", 0.9), ("FormatRecord_Writer", 0.8))]
        write(os.path.join(self.vm, "runtime-410", "053-component-f.json"),
              doc("/w/bin/net10", "/w/bin/net11-ra-on", "*Components.FormatOneRecordBenchmarks*", rows))
        self.assertIn(("QuoteBenchmarks", "Write_Only", ["Quote_Clean", "Quote_WithQuotes", "WriteQuoted_Clean",
                                                          "WriteQuoted_WithQuotes"]), round_report.COMPONENTS)
        self.assertIn(("FormatOneRecordBenchmarks", "FormatRecord_Current", ["FormatRecord_SingleSpan",
                                                                              "FormatRecord_Writer"]),
                      round_report.COMPONENTS)
        markdown = round_report.build(self.round)
        for line in ("QuoteBenchmarks.Quote_Clean against Write_Only",
                     "QuoteBenchmarks.Quote_WithQuotes against Write_Only",
                     "QuoteBenchmarks.WriteQuoted_Clean against Write_Only",
                     "QuoteBenchmarks.WriteQuoted_WithQuotes against Write_Only",
                     "FormatOneRecordBenchmarks.FormatRecord_Writer against FormatRecord_Current"):
            self.assertEqual(len([l for l in markdown.splitlines() if line in l]), 2, line)  # net10 and net11-ra-on

    def test_the_base_commit_pairs_have_their_own_section(self):
        glob = "*QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard"
        write(os.path.join(self.vm, "base-net10", "003-quoted-x.json"), doc(
            "/w/bin/base-net10", "/w/bin/net10", glob, [bench(QUOTED + "(Rows: 100000)", scaled(10_000), scaled(9_000))]))
        write(os.path.join(self.vm, "base-net11", "003-quoted-x.json"), doc(
            "/w/bin/base-net11-ra-on", "/w/bin/net11-ra-on", glob,
            [bench(QUOTED + "(Rows: 100000)", scaled(10_000), scaled(9_500))]))
        markdown = round_report.build(self.round)
        section = markdown.split("### Against the base commit\n", 1)[1].split("\n### ", 1)[0]
        self.assertIn("base-net10 -> net10:", section)
        self.assertIn("base-net11-ra-on -> net11-ra-on:", section)
        self.assertIn("| Scenario | base commit | this build net10 | build/base [99.9 % CI] | verdict |", section)
        self.assertNotIn("Not run on this VM", section)
        # without the pairs the section says how to get them
        os.remove(os.path.join(self.vm, "base-net10", "003-quoted-x.json"))
        os.remove(os.path.join(self.vm, "base-net11", "003-quoted-x.json"))
        write(os.path.join(self.vm, "ra", "003-quoted-x.json"), doc(
            "/w/bin/net11-ra-off", "/w/bin/net11-ra-on", glob, [bench(QUOTED + "(Rows: 10000)", scaled(800), scaled(720))]))
        markdown = round_report.build(self.round)
        self.assertIn("pairs base-net10, base-net11 need round.sh --base-commit", markdown)

    def bdn_report(self, folder, medians):
        os.makedirs(os.path.join(folder, "results"))
        with open(os.path.join(folder, "results", "x-report-full.json"), "w", encoding="utf-8") as f:
            json.dump({"Benchmarks": [{"FullName": name, "Statistics": {"Median": value}}
                                      for name, value in medians.items()]}, f)

    def comparison_fixture(self):
        cls = NS + "DelimitedParseBenchmarks."
        fh, sylvan = cls + "FileHelpers_ParseAndDiscard(Rows: 100000)", cls + "Sylvan_ParseAndDiscard(Rows: 100000)"
        compare = os.path.join(self.vm, "compare")
        # launch medians: net10 FileHelpers 10, 12, 11 ms; net11-ra-off ran only the FileHelpers method
        for config, factor, libraries in (("net10", 1.0, (fh, sylvan)), ("net11-ra-on", 0.9, (fh, sylvan)),
                                          ("net11-ra-off", 0.95, (fh,))):
            for launch, base in enumerate((10e6, 12e6, 11e6), start=1):
                self.bdn_report(os.path.join(compare, config, "001-read25-x", "launch-%d" % launch),
                                {name: base * factor * (2 if name == sylvan else 1) for name in libraries})
        with open(os.path.join(self.vm, "machine.txt"), "w", encoding="utf-8") as f:
            f.write("round: r3\nmode: comparison, 3 launches, not B4 (job default, max iterations 20)\n"
                    "commit: 0123456789abcdef (requested as HEAD)\n")

    def test_the_comparison_has_a_table_per_class_three_columns_and_no_verdict(self):
        self.comparison_fixture()
        markdown = round_report.build_compare(self.round)
        self.assertIn("### DelimitedParseBenchmarks", markdown)
        header = "| Library (method) | Params | net10 | net11-ra-on | net11-ra-off |"
        rows = table_rows(markdown, header)
        self.assertEqual([r[0] for r in rows], ["FileHelpers_ParseAndDiscard", "Sylvan_ParseAndDiscard"])
        self.assertEqual(rows[0][1], "Rows 100K")
        self.assertEqual(rows[0][2], "11.00 ms (10.00 ms to 12.00 ms)")           # median of the launch medians, range
        self.assertEqual(rows[0][3], "9.900 ms (9.000 ms to 10.80 ms)")
        self.assertEqual(rows[0][4], "10.45 ms (9.500 ms to 11.40 ms)")
        self.assertEqual(rows[1][4], "-")                                         # not run in net11-ra-off
        self.assertNotIn("verdict", markdown.lower().replace("no verdict", ""))
        self.assertNotIn("[99.9", markdown)
        self.assertIn("comparison, 3 launches, not B4", markdown)                 # the mode cell
        self.assertIn("0123456789ab", markdown)

    def test_the_comparison_folder_stays_out_of_the_ab_report(self):
        self.comparison_fixture()
        markdown = round_report.build(self.round)
        self.assertIn("No result files", markdown)
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(round_report.main(["--compare", self.round]), 0)
            self.assertEqual(round_report.main(["--compare"]), 2)


if __name__ == "__main__":
    unittest.main()
