#!/usr/bin/env python3
"""Builds the Markdown tables of a benchmark round from the files round.sh wrote (T12).

Usage (from any directory):

    python3 -I benchmarks/tools/round-report.py raw/<round-name> > section.md

raw/<round-name> holds one folder per VM (round.sh writes raw/<round-name>/<vm>/). Each VM folder holds the ab.py
result files (<pair>/*.json), and, when round.sh wrote it, machine.txt, filters.txt and not-run.txt. Folders
named *.runs (the per-launch BenchmarkDotNet output) are not read.

What the numbers are:

- A value column (a time) is the median of all per-launch medians of that build and benchmark case on that VM,
  over every ab.py run the build took part in.
- A ratio is median(B) / median(A) with its 99.9 % bootstrap interval and the B4 verdict, as ab.py recorded them
  for the two builds of one run, interleaved. Below 1.0 the second build is faster.
- A comparison of two methods inside one build (gates R6 and G2, coding patterns, component benchmarks) uses the
  samples of one ab.py run, where the methods were launched in turn, and ab.py's own rule (compare()).
- A verdict marked * comes from a run that is not B4 (ab.py --smoke or --allow-dirty): it is no result.
- Gen0, Gen1 and Gen2 (collections per 1,000 operations, as BenchmarkDotNet prints them) and the bytes allocated
  per operation come from the BenchmarkDotNet reports in each result's <json>.runs folder (design 3.1 B1); a value
  is the median over the launches of that build. Without the .runs folders the GC columns show "-".
- failed.txt (an ab.py run that failed, round.sh goes on) and the methods ab.py found in one build only
  (only_in_a, only_in_b) are listed with not-run.txt.
- Results of ab.py --aot ("harness": "aot", T47) are kept apart from the BenchmarkDotNet results, although their
  folders have the same names (aot/net10, ...): they fill the "AOT read" section only.

Python standard library only.
"""

import glob as globmod
import json
import os
import re
import statistics
import sys

sys.dont_write_bytecode = True   # no __pycache__ next to the tools (the clone of a round stays clean)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ab  # noqa: E402  (the path above makes the module next to this file importable under -I)

SCENARIO_HEADER = "| Scenario | 4.9.1 pkg | net10 | net11-ra-on | net11-ra-off | on/off | verdict |"
PAIR_HEADER = "| Scenario | %s | %s | %s [99.9 %% CI] | verdict |"
PAIR_GC_HEADER = ("| Scenario | %s | %s | %s [99.9 %% CI] | verdict | Gen0 / Gen1 / Gen2; allocated, %s "
                  "| Gen0 / Gen1 / Gen2; allocated, %s |")
CAPPED_GATE_NOTE = ("**Capped round: on %s a gate ratio is under its limit without the B4 verdict `faster` (%s). The cap "
                    "(--max-iterations) can turn `faster` into `no-change`, never the reverse: rerun the gate lines "
                    "uncapped on this VM under a second round name before the VMs are deleted "
                    "(`round.sh <round-name>-gates <sha> benchmarks/tools/filters-gates.txt --vm <vm-name>`, about "
                    "1 to 6 h) and read the gate from that run.**")
G2_P5_NOTE = ("G2_SingleSpan still copies each record from the engine's record buffer into the result (it does not "
              "format straight into the PooledStringWriter chunk): it measures G2 without P5, so a result just under "
              "10 % understates what Phase 3 (G2, G3 and P5) can give.")
GC_BUILDS = ("pkg-net10", "pkg-net11", "control-net10", "control-net11-ra-on", "control-net11-ra-off", "net10",
             "net11-ra-on", "net11-ra-off")
METHOD_HEADER = ("| Benchmark | Params | Build | Baseline | Variant | variant/baseline [99.9 % CI] | verdict |")

# Report groups of the filter file, in report order: (group, title). Groups not listed here come after them.
SCENARIO_GROUPS = [
    ("read25", "25-column read"),
    ("quoted", "Quoted read"),
    ("read-other", "Other plain reads (long lines, 150 columns)"),
    ("fixed", "Fixed-width read"),
    ("write", "Writes"),
    ("async", "Async"),
    ("exception", "Exception path"),
    ("control", "Controls (code that is the same in every build)"),
]
METHOD_GROUPS = ("component", "prototype")
AOT_GROUP = "aot"
AOT_NOTE = ("There is no AOT control build (the 4.9.1 source compiled with SDK 11 under AOT), so each 4.9.1 -> 4.10 "
            "ratio below includes the compiler and SDK effect as well as the code change: read it as what a consumer "
            "gets. The AOT harness forces a full collection before every timed read and its x64 build does not assume "
            "AVX2, so its times are not comparable with the BenchmarkDotNet tables (BENCHMARKS.md, \"Native AOT "
            "timing harness\").")

# Coding-pattern choices of design 2.5 (items 1, 2, 3): (item, class, baseline, variants).
PATTERN_CHOICES = [
    ("1: safe span vector loop against the Unsafe loop", "VectorLoopShapeBenchmarks", "Scan_LoadUnsafe",
     ["Scan_SpanCreate"]),
    ("2: converter call shapes for the write path", "ConverterCallShapeBenchmarks", "Format_GenericHelper",
     ["Format_GenericWriter", "Format_DirectCall"]),
    ("3: \"any match first\" block test in the tokenizer", "TokenizerBenchmarks", "Tokenizer_SinglePassVector",
     ["Tokenizer_SinglePassVector_AnyMatchFirst"]),
]
# Component benchmarks (B3): baseline (today's code) against the candidates.
COMPONENTS = [
    ("TokenizerBenchmarks", "Tokenizer_CountPlusIndexOf",
     ["Tokenizer_SinglePassScalar", "Tokenizer_SinglePassVector", "Tokenizer_SinglePassVector_AnyMatchFirst"]),
    ("UnquoteBenchmarks", "Unquote_Current", ["Unquote_IndexOf"]),
    ("CreateStringBenchmarks", "CreateString_New", ["CreateString_Cached"]),
    ("FormatOneRecordBenchmarks", "FormatRecord_Current", ["FormatRecord_SingleSpan"]),
]
METHOD_CLASSES = {cls for _, cls, _, _ in PATTERN_CHOICES} | {cls for cls, _, _ in COMPONENTS} | {
    "R6UpperBoundBenchmarks", "G2PrototypeBenchmarks"}
R6_CLASS = "R6UpperBoundBenchmarks"
R6_GATE = 0.94          # R6_PrecomputedEnds at least 6 % faster than R6_Current
G2_CLASS = "G2PrototypeBenchmarks"
G2_GATE = 0.90          # G2_SingleSpan at least 10 % faster than G2_Current
GATE_ROWS = "100000"
BUILDS_410 = ("net10", "net11-ra-on", "net11-ra-off")
SLOPE_BUILDS = ("pkg-net10", "net10", "net11-ra-on")
_EPSILON = 1e-9


# --- names and formatting -----------------------------------------------------------------------------------------

_NAME = re.compile(r"^(?P<method>[^(]+?)(?:\((?P<params>.*)\))?$")


def parse_case(full_name):
    """(class name, method name, {param: value}) of a BenchmarkDotNet full name such as
    'Ns.Type.Method(Rows: 100000)'."""
    m = _NAME.match(full_name)
    parts = m.group("method").split(".")
    params = {}
    if m.group("params"):
        for item in m.group("params").split(", "):
            key, _, value = item.partition(": ")
            params[key.strip()] = value.strip()
    return parts[-2] if len(parts) >= 2 else "", parts[-1], params


def fmt_param(key, value):
    if key == "Rows" and value.isdigit():
        n = int(value)
        if n >= 1_000_000 and n % 1_000_000 == 0:
            return "%s %dM" % (key, n // 1_000_000)
        if n >= 1_000 and n % 1_000 == 0:
            return "%s %dK" % (key, n // 1_000)
    return "%s %s" % (key, value)


def fmt_params(params):
    return ", ".join(fmt_param(k, v) for k, v in params.items())


def case_label(full_name):
    """'Type.Method (Rows 100K)' for a full name."""
    cls, method, params = parse_case(full_name)
    label = "%s.%s" % (cls, method) if cls else method
    return "%s (%s)" % (label, fmt_params(params)) if params else label


def fmt_time(ns):
    if ns is None:
        return "-"
    for unit, scale in (("s", 1e9), ("ms", 1e6), ("μs", 1e3)):
        if ns >= scale:
            return _three_digits(ns / scale) + " " + unit
    return _three_digits(ns) + " ns"


def _three_digits(x):
    if x >= 100:
        return "%.1f" % x
    if x >= 10:
        return "%.2f" % x
    return "%.3f" % x


def fmt_ratio(r):
    return "%.3f [%.3f, %.3f]" % (r["median_ratio"], r["ci_low"], r["ci_high"])


def fmt_verdict(r, b4):
    return r["verdict"] + ("" if b4 else "*")


def median(values):
    return statistics.median(values) if values else None


def _per_1000(x):
    return "-" if not x else ("%.4g" % x)


def fmt_bytes(n):
    if n is None:
        return "-"
    for unit, scale in (("MB", 1024.0 ** 2), ("KB", 1024.0)):
        if n >= scale:
            return _three_digits(n / scale) + " " + unit
    return "%d B" % n


def fmt_gc(g):
    """'Gen0 / Gen1 / Gen2; allocated' of (gen0, gen1, gen2, bytes), '-' without data."""
    if g is None:
        return "-"
    return "%s / %s / %s; %s" % (_per_1000(g[0]), _per_1000(g[1]), _per_1000(g[2]), fmt_bytes(g[3]))


# --- loading ------------------------------------------------------------------------------------------------------

class Result:
    """One ab.py result file."""

    def __init__(self, path, document):
        self.path = path
        self.doc = document
        self.a = os.path.basename(os.path.normpath(document["a"]["dir"]))
        self.b = os.path.basename(os.path.normpath(document["b"]["dir"]))
        self.filter = document.get("filter", "")
        self.b4 = bool(document.get("b4"))
        self.aot = document.get("harness") == "aot"
        self.pair_name = os.path.basename(os.path.dirname(path))
        self.only = [("A", self.a, m) for m in document.get("only_in_a") or []] + \
                    [("B", self.b, m) for m in document.get("only_in_b") or []]
        self.rows = document.get("benchmarks", [])
        self._gc = None

    def gc(self):
        """{'a'|'b': {case: [(gen0, gen1, gen2, bytes) per launch]}} from the BenchmarkDotNet reports in
        <json>.runs (launch folders are named <nnn>-<a|b>-<method>, ab.py)."""
        if self._gc is None:
            self._gc = {"a": {}, "b": {}}
            runs = self.path + ".runs"
            if os.path.isdir(runs):
                for launch in sorted(os.listdir(runs)):
                    parts = launch.split("-", 2)
                    if len(parts) < 3 or parts[1] not in ("a", "b"):
                        continue
                    for report in sorted(globmod.glob(os.path.join(runs, launch, "results", "*-report-full.json"))):
                        try:
                            with open(report, encoding="utf-8") as f:
                                data = json.load(f)
                        except (OSError, ValueError):
                            continue
                        for bench in data.get("Benchmarks", []):
                            mem = bench.get("Memory") or {}
                            ops = mem.get("TotalOperations")
                            if not ops or "FullName" not in bench:
                                continue
                            self._gc[parts[1]].setdefault(bench["FullName"], []).append(tuple(
                                [mem.get(k, 0) * 1000.0 / ops for k in
                                 ("Gen0Collections", "Gen1Collections", "Gen2Collections")]
                                + [float(mem.get("BytesAllocatedPerOperation", 0))]))
        return self._gc

    def side(self, build):
        """'a', 'b' or None."""
        return "a" if self.a == build else "b" if self.b == build else None


class Vm:
    def __init__(self, name, path):
        self.name = name
        self.path = path
        self.results = []
        self.filters = []          # (group, pairs, glob)
        self.not_run = []          # (pair, group, glob, reason)
        self.failed = []           # (pair, group, glob, log)
        self.damaged = []          # (path relative to the VM folder, reason): result files that do not parse
        self.machine = ""
        self.aot = None            # a Vm with the ab.py --aot results of this VM (see load)

    @property
    def b4(self):
        return all(r.b4 for r in self.results) and (self.aot is None or self.aot.b4)

    def groups_of(self, glob):
        """The report groups of a glob: a filter line may name several, comma-separated ("async,exception").
        {None} for a glob that no filter line names (or without a filter file)."""
        groups = set()
        for group, _, g in self.filters:
            if g == glob:
                groups.update(group.split(","))
        return groups or {None}

    def samples(self, build, case):
        """Every per-launch median of one case on one build, over all runs on this VM."""
        values = []
        for result in self.results:
            side = result.side(build)
            if side is None:
                continue
            for row in result.rows:
                if row["name"] == case:
                    values.extend(row[side + "_median_ns"])
        return values

    def value(self, build, case):
        return median(self.samples(build, case))

    def gc(self, build, case):
        """(gen0, gen1, gen2, bytes) medians over every launch of one build and case on this VM, or None."""
        launches = []
        for result in self.results:
            side = result.side(build)
            if side is not None:
                launches.extend(result.gc()[side].get(case, []))
        if not launches:
            return None
        return tuple(statistics.median(x[i] for x in launches) for i in range(4))

    def pair(self, a, b):
        return [r for r in self.results if r.a == a and r.b == b]

    def pair_row(self, a, b, case):
        for result in self.pair(a, b):
            for row in result.rows:
                if row["name"] == case:
                    return row, result.b4
        return None, True


def _read_text(path):
    if not os.path.isfile(path):
        return ""
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read()


def read_filters(text):
    filters = []
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        if len(parts) == 3:
            filters.append(tuple(parts))
    return filters


def load(round_dir):
    """The VMs of a round folder, in name order."""
    vms = []
    for name in sorted(os.listdir(round_dir)):
        path = os.path.join(round_dir, name)
        if not os.path.isdir(path):
            continue
        vm = Vm(name, path)
        for dirpath, dirnames, filenames in os.walk(path):
            dirnames[:] = sorted(d for d in dirnames if not d.endswith(".runs"))
            for filename in sorted(filenames):
                if not filename.endswith(".json"):
                    continue
                full = os.path.join(dirpath, filename)
                try:
                    with open(full, encoding="utf-8") as f:
                        document = json.load(f)
                except (OSError, ValueError) as e:
                    # A file cut off by a kill: listed, never a crash (rereview-T12 Minor 2). --resume reruns it.
                    vm.damaged.append((os.path.relpath(full, path), str(e).splitlines()[0][:120]))
                    continue
                if isinstance(document, dict) and "benchmarks" in document and "a" in document:
                    vm.results.append(Result(full, document))
        vm.filters = read_filters(_read_text(os.path.join(path, "filters.txt")))
        vm.aot = Vm(name, path)
        vm.aot.results = [r for r in vm.results if r.aot]
        vm.aot.filters = vm.filters
        vm.results = [r for r in vm.results if not r.aot]
        for line in _read_text(os.path.join(path, "not-run.txt")).splitlines():
            parts = line.split("\t")
            if len(parts) == 4:
                vm.not_run.append(tuple(parts))
        for line in _read_text(os.path.join(path, "failed.txt")).splitlines():
            parts = line.split("\t")
            if len(parts) == 4:
                vm.failed.append(tuple(parts))
        vm.machine = _read_text(os.path.join(path, "machine.txt"))
        if vm.results or vm.aot.results or vm.filters or vm.failed or vm.damaged:
            vms.append(vm)
    return vms


# --- machine record -----------------------------------------------------------------------------------------------

def machine_facts(text):
    def find(pattern, default="-"):
        m = re.search(pattern, text, re.MULTILINE)
        return m.group(1).strip() if m else default

    flags = find(r"^Flags:\s*(.*)$", "").split()
    runtimes = sorted(set(re.findall(r"Microsoft\.NETCore\.App (\d+\.\d+\.\d+\S*)", text)))
    uname = find(r"^== uname -a\n(.*)$", "")
    return {
        "cpu": find(r"^Model name:\s*(.*)$"),
        "arch": find(r"^Architecture:\s*(.*)$"),
        "cpus": find(r"^CPU\(s\):\s*(.*)$"),
        "simd": ", ".join(f for f in ("avx2", "avx512f", "avx512vbmi2", "asimd", "sve") if f in flags) or "-",
        "os": find(r'^PRETTY_NAME="?([^"\n]*)"?$'),
        "kernel": uname.split()[2] if len(uname.split()) > 2 else "-",
        "sdk": find(r"^\.NET SDK:\s*\n\s*Version:\s*(\S+)"),
        "runtimes": ", ".join(runtimes) or "-",
        "commit": find(r"^commit: (\S+)"),
        "mode": find(r"^mode: (.*)$"),
    }


# --- tables -------------------------------------------------------------------------------------------------------

def _table(header, rows):
    columns = header.count("|") - 1
    lines = [header, "|" + "---|" * columns]
    lines.extend("| " + " | ".join(row) + " |" for row in rows)
    return lines


def _cases_in_order(vm, results):
    seen = []
    for result in results:
        for row in result.rows:
            if row["name"] not in seen:
                seen.append(row["name"])
    return seen


def _sort_key(case):
    cls, method, params = parse_case(case)
    rows = params.get("Rows", "")
    return (cls, method, int(rows) if rows.isdigit() else 0, fmt_params(params))


def scenario_groups(vm):
    """[(title, [case]), ...] for the scenario tables; one table "Scenarios" without a filter file."""
    method_results = [r for r in vm.results if vm.groups_of(r.filter) & set(METHOD_GROUPS)]
    method_cases = set(_cases_in_order(vm, method_results))
    method_cases.update(c for c in _cases_in_order(vm, vm.results) if parse_case(c)[0] in METHOD_CLASSES)
    if not vm.filters:
        cases = [c for c in _cases_in_order(vm, vm.results) if c not in method_cases]
        return [("Scenarios", sorted(cases, key=_sort_key), [])]
    groups = []
    known = [g for g, _ in SCENARIO_GROUPS]
    extra = [g for grp, _, _ in vm.filters for g in grp.split(",")
             if g not in known and g not in METHOD_GROUPS and g != AOT_GROUP]
    titles = dict(SCENARIO_GROUPS)
    for group in known + sorted(set(extra), key=extra.index):
        globs = [g for grp, _, g in vm.filters if group in grp.split(",")]
        if not globs:
            continue
        results = [r for r in vm.results if r.filter in globs]
        cases = [c for c in _cases_in_order(vm, results) if c not in method_cases]
        missing = [g for g in globs if not any(r.filter == g for r in vm.results)]
        groups.append((titles.get(group, group), sorted(cases, key=_sort_key), missing))
    return groups


def scenario_table(vm, cases, missing):
    rows = []
    for case in cases:
        pkg10, pkg11 = vm.value("pkg-net10", case), vm.value("pkg-net11", case)
        pkg = "-" if pkg10 is None and pkg11 is None else "%s / %s" % (fmt_time(pkg10), fmt_time(pkg11))
        row, b4 = vm.pair_row("net11-ra-off", "net11-ra-on", case)
        rows.append([case_label(case), pkg, fmt_time(vm.value("net10", case)),
                     fmt_time(vm.value("net11-ra-on", case)), fmt_time(vm.value("net11-ra-off", case)),
                     fmt_ratio(row) if row else "-", fmt_verdict(row, b4) if row else "not run"])
    for glob in missing:
        rows.append(["`%s`" % glob, "-", "-", "-", "-", "-", "not run"])
    return _table(SCENARIO_HEADER, rows)


def aot_section(vm):
    """The AOT read section: the scenario table of the AOT folders and the 4.9.1 -> 4.10 pair of each runtime."""
    aot = vm.aot
    globs = [g for grp, _, g in vm.filters if AOT_GROUP in grp.split(",")]
    if not aot.results:
        if globs:
            return ["AOT read: not run on this VM (%s)." % ", ".join("`%s`" % g for g in globs)]
        return ["AOT read: not run on this VM."]
    cases = sorted(_cases_in_order(aot, aot.results), key=_sort_key)
    missing = [g for g in globs if not any(r.filter == g for r in aot.results)]
    lines = [AOT_NOTE, ""]
    lines += scenario_table(aot, cases, missing)
    for a, b in (("pkg-net10", "net10"), ("pkg-net11", "net11-ra-on"), ("pkg-net11", "net11-ra-off")):
        table = pair_table(aot, a, b, "4.9.1 pkg (AOT)", "4.10 %s (AOT)" % b, "4.10/pkg")
        if table:
            lines += ["", "aot/%s -> aot/%s:" % (a, b), ""] + table
    return lines


def pair_table(vm, a, b, a_label, b_label, ratio_label, groups=None, gc=False):
    """Rows of every case that ran in the pair a -> b, with ab.py's ratio and verdict (and the GC columns)."""
    results = [r for r in vm.pair(a, b) if groups is None or vm.groups_of(r.filter) & set(groups)]
    if not results:
        return None
    rows = []
    for result in results:
        for row in sorted(result.rows, key=lambda r: _sort_key(r["name"])):
            cells = [case_label(row["name"]), fmt_time(median(row["a_median_ns"])),
                     fmt_time(median(row["b_median_ns"])), fmt_ratio(row), fmt_verdict(row, result.b4)]
            if gc:
                cells += [fmt_gc(vm.gc(a, row["name"])), fmt_gc(vm.gc(b, row["name"]))]
            rows.append(cells)
    if gc:
        return _table(PAIR_GC_HEADER % (a_label, b_label, ratio_label, a_label, b_label), rows)
    return _table(PAIR_HEADER % (a_label, b_label, ratio_label), rows)


def method_params(vm, cls, method):
    """Every parameter set measured for a method, in order."""
    found = []
    for result in vm.results:
        for row in result.rows:
            c, m, p = parse_case(row["name"])
            if c == cls and m == method and p not in found:
                found.append(p)
    return sorted(found, key=lambda p: [int(v) if v.isdigit() else v for v in p.values()])


def compare_methods(vm, build, cls, baseline, variant, params):
    """ab.compare of two methods of one build, measured in the same ab.py run; None when that run does not exist."""
    for result in vm.results:
        side = result.side(build)
        if side is None:
            continue
        base = var = None
        for row in result.rows:
            c, m, p = parse_case(row["name"])
            if c == cls and p == params:
                if m == baseline:
                    base = row[side + "_median_ns"]
                elif m == variant:
                    var = row[side + "_median_ns"]
        if base and var:
            r = ab.compare(base, var)
            r["base"], r["var"], r["b4"] = median(base), median(var), result.b4
            return r
    return None


def method_table(vm, comparisons, builds, item_label=None):
    rows = []
    for entry in comparisons:
        if item_label:
            item, cls, baseline, variants = entry
        else:
            cls, baseline, variants = entry
            item = None
        for params in method_params(vm, cls, baseline):
            for variant in variants:
                for build in builds:
                    r = compare_methods(vm, build, cls, baseline, variant, params)
                    if r is None:
                        continue
                    name = "%s: %s against %s" % (item, variant, baseline) if item else "%s.%s against %s" % (
                        cls, variant, baseline)
                    rows.append([name, fmt_params(params) or "-", build, fmt_time(r["base"]), fmt_time(r["var"]),
                                 fmt_ratio(r), fmt_verdict(r, r["b4"])])
    return _table(METHOD_HEADER, rows) if rows else None


# --- gates --------------------------------------------------------------------------------------------------------

def _meets(r, gate):
    return r is not None and r["median_ratio"] <= gate + _EPSILON and r["verdict"] == "faster"


def gate_r6(vm):
    """(lines, {build: (precomputed result, fast result)}) for gate R6."""
    lines = []
    header = ("| Build | Rows | R6_Current | R6_PrecomputedEnds | ratio [99.9 % CI] | gate | "
              "R6_PrecomputedEnds_FastUnquote | ratio [99.9 % CI] | at 6 % |")
    rows = []
    decisions = {}
    for build in BUILDS_410:
        for params in method_params(vm, R6_CLASS, "R6_Current"):
            pre = compare_methods(vm, build, R6_CLASS, "R6_Current", "R6_PrecomputedEnds", params)
            fast = compare_methods(vm, build, R6_CLASS, "R6_Current", "R6_PrecomputedEnds_FastUnquote", params)
            if pre is None and fast is None:
                continue
            gate_row = params.get("Rows") == GATE_ROWS
            if gate_row:
                decisions[build] = (pre, fast)

            def verdict_cell(r):
                if r is None:
                    return "-"
                text = "meets" if _meets(r, R6_GATE) else "misses"
                return text + ("" if r["b4"] else "*") if gate_row else "(informative)"

            ref = pre or fast
            rows.append([build, fmt_params(params), fmt_time(ref["base"]),
                         fmt_time(pre["var"]) if pre else "-", fmt_ratio(pre) if pre else "-", verdict_cell(pre),
                         fmt_time(fast["var"]) if fast else "-", fmt_ratio(fast) if fast else "-",
                         verdict_cell(fast)])
    if not rows:
        return ["Gate R6: not run on this VM."], decisions
    lines.extend(_table(header, rows))
    lines.append("")
    for build, (pre, fast) in decisions.items():
        if pre is not None:
            lines.append("- R6_PrecomputedEnds %s the 6 %% gate on %s at 100K rows (ratio %.3f, verdict %s)%s."
                         % ("meets" if _meets(pre, R6_GATE) else "misses", build, pre["median_ratio"],
                            pre["verdict"], "" if pre["b4"] else ", not B4"))
        if pre is not None and fast is not None and _meets(pre, R6_GATE) != _meets(fast, R6_GATE):
            lines.append("- On %s, R6_PrecomputedEnds (R6 alone) and R6_PrecomputedEnds_FastUnquote (R6 with R3) fall "
                         "on different sides of 6 %%: the decision is the owner's (ruling R32)." % build)
    lines.append("")
    lines.extend(r6_against_package(vm))
    return lines, decisions


def r6_against_package(vm):
    """R6_Current of the 4.10 build against R6_Current and the quoted parse-and-discard of the 4.9.1 package."""
    header = ("| Pair | Rows | R6_Current 4.9.1 pkg | R6_Current 4.10 | 4.10/pkg [99.9 % CI] | verdict | "
              "R6_PrecomputedEnds 4.10 / R6_Current pkg [99.9 % CI] | quoted parse-and-discard, 4.9.1 pkg |")
    rows = []
    for pkg, build in (("pkg-net10", "net10"), ("pkg-net11", "net11-ra-on")):
        for result in vm.pair(pkg, build):
            for params in method_params(vm, R6_CLASS, "R6_Current"):
                cur = pre = None
                for row in result.rows:
                    c, m, p = parse_case(row["name"])
                    if c == R6_CLASS and p == params:
                        if m == "R6_Current":
                            cur = row
                        elif m == "R6_PrecomputedEnds":
                            pre = row
                if cur is None:
                    continue
                against = ab.compare(cur["a_median_ns"], pre["b_median_ns"]) if pre else None
                quoted = None
                for r in vm.results:
                    for row in r.rows:
                        c, m, p = parse_case(row["name"])
                        if c == "QuotedFieldsBenchmarks" and m == "FileHelpers_ParseAndDiscard" and p == params:
                            quoted = vm.value(pkg, row["name"])
                rows.append(["%s -> %s" % (pkg, build), fmt_params(params), fmt_time(median(cur["a_median_ns"])),
                             fmt_time(median(cur["b_median_ns"])), fmt_ratio(cur), fmt_verdict(cur, result.b4),
                             "%s %s" % (fmt_ratio(against), fmt_verdict(against, result.b4)) if against else "-",
                             fmt_time(quoted)])
    if not rows:
        return ["R6_Current against the 4.9.1 package: not run on this VM (pairs v410-net10, v410-net11)."]
    return ["R6_Current against the 4.9.1 package (same VM, interleaved):", ""] + _table(header, rows)


def gate_g2(vm):
    header = "| Build | Rows | G2_Current | G2_SingleSpan | ratio [99.9 % CI] | gate (at least 10 % faster) |"
    rows = []
    decisions = {}
    for build in BUILDS_410:
        for params in method_params(vm, G2_CLASS, "G2_Current"):
            r = compare_methods(vm, build, G2_CLASS, "G2_Current", "G2_SingleSpan", params)
            if r is None:
                continue
            gate_row = params.get("Rows") == GATE_ROWS
            if gate_row:
                decisions[build] = r
            verdict = ("meets" if _meets(r, G2_GATE) else "misses") + ("" if r["b4"] else "*")
            rows.append([build, fmt_params(params), fmt_time(r["base"]), fmt_time(r["var"]), fmt_ratio(r),
                         verdict if gate_row else "(informative)"])
    if not rows:
        return ["Gate G2: not run on this VM."], decisions
    return _table(header, rows) + ["", G2_P5_NOTE], decisions


# --- runtime async and slope --------------------------------------------------------------------------------------

def runtime_async(vm):
    lines = []
    for a, b in (("net11-ra-off", "net11-ra-on"), ("control-net11-ra-off", "control-net11-ra-on")):
        results = vm.pair(a, b)
        if not results:
            continue
        rows = [(row, r) for r in results for row in r.rows]
        controls = [abs(row["median_ratio"] - 1.0) for row, r in rows if "control" in vm.groups_of(r.filter)]
        noise = max(controls) if controls else None
        counts = {}
        for row, _ in rows:
            counts[row["verdict"]] = counts.get(row["verdict"], 0) + 1
        lines.append("**%s -> %s**: %d cases; %s. Control noise: %s." % (
            a, b, len(rows), ", ".join("%d %s" % (n, v) for v, n in sorted(counts.items())),
            "largest |on/off - 1| of %d control cases = %.1f %%" % (len(controls), noise * 100)
            if noise is not None else "no control case in this pair"))
        lines.append("")
        slower = [(row, r) for row, r in rows if row["verdict"] == "slower"]
        if not slower:
            lines.append("No case is slower with runtime async on by the B4 rule.")
        else:
            table = []
            for row, r in sorted(slower, key=lambda x: _sort_key(x[0]["name"])):
                beyond = "-" if noise is None else ("yes" if row["median_ratio"] - 1.0 > noise else "no")
                table.append([case_label(row["name"]), fmt_ratio(row), fmt_verdict(row, r.b4), beyond])
            lines.extend(_table("| Slower with runtime async on | on/off [99.9 % CI] | verdict | beyond control noise |",
                                table))
        lines.append("")
    return lines or ["Runtime async: not run on this VM."]


def slope(vm):
    rows = []
    families = []
    for result in vm.results:
        if vm.groups_of(result.filter) & set(METHOD_GROUPS):
            continue
        for row in result.rows:
            cls, method, params = parse_case(row["name"])
            if "Rows" in params and cls not in METHOD_CLASSES:
                key = (cls, method, tuple((k, v) for k, v in params.items() if k != "Rows"))
                if key not in families:
                    families.append(key)
    names = {}
    for result in vm.results:
        for row in result.rows:
            cls, method, params = parse_case(row["name"])
            names[(cls, method, tuple(sorted(params.items())))] = row["name"]
    for cls, method, other in sorted(families):
        for build in SLOPE_BUILDS:
            per_row = {}
            gc = {}
            for rows_value in ("10000", "100000"):
                params = dict(other)
                params["Rows"] = rows_value
                case = names.get((cls, method, tuple(sorted(params.items()))))
                # Pooled over every run of this build, like the value columns.
                value = vm.value(build, case) if case else None
                if value is not None:
                    per_row[rows_value] = value / int(rows_value)
                    gc[rows_value] = vm.gc(build, case)
            if len(per_row) == 2:
                label = "%s.%s" % (cls, method) + (" (%s)" % fmt_params(dict(other)) if other else "")

                def gc_cell(rows_value):
                    g = gc.get(rows_value)
                    if g is None:
                        return "-"
                    return "%s / %s / %s; %.0f B per row" % (_per_1000(g[0]), _per_1000(g[1]), _per_1000(g[2]),
                                                             g[3] / int(rows_value))
                rows.append([label, build, "%.1f" % per_row["10000"], "%.1f" % per_row["100000"],
                             "%.3f" % (per_row["100000"] / per_row["10000"]), gc_cell("10000"), gc_cell("100000")])
    if not rows:
        return ["No benchmark ran with 10K and 100K rows on this VM."]
    return _table("| Benchmark | Build | 10K rows: ns per row | 100K rows: ns per row | 100K/10K "
                  "| 10K: Gen0 / Gen1 / Gen2; allocated | 100K: Gen0 / Gen1 / Gen2; allocated |", rows)


def gc_table(vm):
    """Gen0, Gen1, Gen2 and allocated bytes of every BenchmarkDotNet case and build (design 3.1 B1)."""
    rows = []
    for case in sorted(_cases_in_order(vm, vm.results), key=_sort_key):
        for build in GC_BUILDS:
            g = vm.gc(build, case)
            if g is not None:
                rows.append([case_label(case), build, _per_1000(g[0]), _per_1000(g[1]), _per_1000(g[2]),
                             fmt_bytes(g[3])])
    if not rows:
        return ["No BenchmarkDotNet report with memory data on this VM (the *.runs folders are missing)."]
    return _table("| Scenario | Build | Gen0 | Gen1 | Gen2 | Allocated per operation |", rows)


# --- report -------------------------------------------------------------------------------------------------------

def build(round_dir):
    """The Markdown tables of a round folder (raw/<round-name>)."""
    vms = load(round_dir)
    out = ["<!-- generated by benchmarks/tools/round-report.py from %s -->" % os.path.basename(
        os.path.normpath(round_dir)), ""]
    if not vms:
        return "\n".join(out + ["No result files under %s." % round_dir, ""])

    out += ["## Machines", ""]
    machine_rows = []
    for vm in vms:
        f = machine_facts(vm.machine)
        machine_rows.append([vm.name, f["cpu"], f["arch"], f["cpus"], f["simd"], f["os"], f["kernel"], f["sdk"],
                             f["runtimes"], f["commit"][:12], f["mode"] if vm.b4 else "not B4: " + f["mode"]])
    out += _table("| VM | CPU | Architecture | Logical CPUs | SIMD flags | OS | Kernel | SDK | Runtimes | Commit "
                  "| Mode |", machine_rows)
    out.append("")

    summary = []
    sections = []
    for vm in vms:
        s = ["## VM %s" % vm.name, ""]
        if not vm.b4:
            s += ["**Not B4 (smoke run or uncommitted build): no verdict on this VM is a result. Verdicts marked * "
                  "come from such runs.**", ""]
        if vm.failed:
            s += ["**%d ab.py run(s) failed on this VM; see \"Failed\". Their cells are empty.**" % len(vm.failed), ""]
        s += ["### Free gains of .NET 11", ""]
        s += pair_table(vm, "pkg-net10", "pkg-net11", "4.9.1 on .NET 10", "4.9.1 on .NET 11", "11/10", gc=True) or [
            "Not run on this VM (pair free-gains)."]
        s += ["", "### Compiler and SDK effect", ""]
        for a, b, a_label, b_label, ratio, pair in (
                ("pkg-net10", "control-net10", "4.9.1 pkg", "control (SDK 11)", "control/pkg", "sdk-net10"),
                ("pkg-net11", "control-net11-ra-on", "4.9.1 pkg", "control net11.0, ra on", "control/pkg",
                 "sdk-net11"),
                ("control-net11-ra-off", "control-net11-ra-on", "control ra off", "control ra on", "on/off",
                 "ra-control")):
            s += ["%s -> %s:" % (a, b), ""]
            s += pair_table(vm, a, b, a_label, b_label, ratio) or ["Not run on this VM (pair %s)." % pair]
            s.append("")
        for title, cases, missing in scenario_groups(vm):
            s += ["### %s" % title, ""]
            s += scenario_table(vm, cases, missing) if cases or missing else ["No case ran."]
            s.append("")
        s += ["### AOT read", ""]
        s += aot_section(vm) + [""]
        s += ["### Gate R6", ""]
        r6_lines, r6 = gate_r6(vm)
        s += r6_lines + [""]
        s += ["### Gate G2", ""]
        g2_lines, g2 = gate_g2(vm)
        s += g2_lines + [""]
        if "max iterations" in machine_facts(vm.machine)["mode"]:
            under = []
            for build, (pre, _) in sorted(r6.items()):
                if pre is not None and pre["median_ratio"] <= R6_GATE + _EPSILON and pre["verdict"] != "faster":
                    under.append("R6 on %s" % build)
            for build, g in sorted(g2.items()):
                if g["median_ratio"] <= G2_GATE + _EPSILON and g["verdict"] != "faster":
                    under.append("G2 on %s" % build)
            if under:
                s += [CAPPED_GATE_NOTE % (vm.name, ", ".join(under)), ""]
        s += ["### Coding-pattern choices", ""]
        s += method_table(vm, PATTERN_CHOICES, ("net10", "net11-ra-on", "net11-ra-off"), item_label=True) or [
            "Not run on this VM."]
        s += ["", "### Component benchmarks", ""]
        s += method_table(vm, COMPONENTS, ("net10", "net11-ra-on", "net11-ra-off")) or ["Not run on this VM."]
        s += ["", "### Runtime async", ""]
        s += runtime_async(vm)
        s += ["### 4.10 build against the 4.9.1 package", ""]
        for a, b in (("pkg-net10", "net10"), ("pkg-net11", "net11-ra-on")):
            table = pair_table(vm, a, b, "4.9.1 pkg", "4.10 " + b, "4.10/pkg",
                               groups=[g for g, _ in SCENARIO_GROUPS] + [None])
            if table:
                s += ["%s -> %s:" % (a, b), ""] + table + [""]
        s += ["### Slope from 10K to 100K rows", ""]
        s += slope(vm)
        s.append("")
        s += ["### GC and allocation", ""]
        s += gc_table(vm)
        s.append("")
        only = [[r.pair_name, ",".join(sorted(g for g in vm.groups_of(r.filter) if g)) or "-", "`%s`" % r.filter,
                 "only in %s (%s): %s" % (side, folder, method)]
                for r in vm.results + vm.aot.results for side, folder, method in r.only]
        if vm.not_run or only:
            s += ["### Not run", ""]
            s += _table("| Pair | Group | Glob | Reason |",
                        [[p, g, "`%s`" % pattern, why] for p, g, pattern, why in vm.not_run] + only)
            s.append("")
        if vm.failed:
            s += ["### Failed", ""]
            s += _table("| Pair | Group | Glob | Log |", [[p, g, "`%s`" % pattern, "`%s`" % log]
                                                         for p, g, pattern, log in vm.failed])
            s += ["", "`round.sh ... --resume` reruns these runs and keeps every finished one."]
            s.append("")
        if vm.damaged:
            s += ["### Damaged results", ""]
            s += _table("| Result file | Reason |", [["`%s`" % f, why] for f, why in vm.damaged])
            s += ["", "These files do not parse (a run cut off by a kill); their cells are empty. "
                  "`round.sh ... --resume` runs them again."]
            s.append("")
        sections += s
        for build in BUILDS_410:
            if build in r6 or build in g2:
                pre, fast = r6.get(build, (None, None))
                g = g2.get(build)
                summary.append([vm.name, build,
                                "%.3f %s" % (pre["median_ratio"], "meets" if _meets(pre, R6_GATE) else "misses")
                                if pre else "-",
                                "%.3f %s" % (fast["median_ratio"], "meets" if _meets(fast, R6_GATE) else "misses")
                                if fast else "-",
                                "%.3f %s" % (g["median_ratio"], "meets" if _meets(g, G2_GATE) else "misses")
                                if g else "-",
                                "yes" if vm.b4 else "no"])

    out += ["## Gate summary", ""]
    out += _table("| VM | Build | R6_PrecomputedEnds / R6_Current (gate 0.94) | R6_PrecomputedEnds_FastUnquote / "
                  "R6_Current (informative) | G2_SingleSpan / G2_Current (gate 0.90) | B4 |", summary) if summary else [
        "No gate prototype ran."]
    out.append("")
    out += sections
    return "\n".join(out) + "\n"


def main(argv):
    if len(argv) != 1 or not os.path.isdir(argv[0]):
        print("usage: round-report.py raw/<round-name>", file=sys.stderr)
        return 2
    sys.stdout.write(build(argv[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
