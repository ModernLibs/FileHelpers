"""Tests for round-report.py (T12). Run from the repository root:

    python3 -I -m unittest discover -s benchmarks/tools -p 'test_*.py'
"""

import importlib.util
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
        rows = table_rows(markdown, round_report.PAIR_HEADER % ("4.9.1 on .NET 10", "4.9.1 on .NET 11", "11/10"))
        self.assertEqual(len(rows), 2)
        self.assertTrue(all(row[-1] == "faster" for row in rows))


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


if __name__ == "__main__":
    unittest.main()
