#!/usr/bin/env python3
"""Interleaved A/B runner for the BenchmarkDotNet suite, with the acceptance rule B4 of the 4.10 design.

Usage (from any directory; see BENCHMARKS.md):

    python3 -I benchmarks/tools/ab.py --a <dir> --b <dir> --launches N --filter '<glob>' --out <json> \
        [--work <dir>] [--expect-a <line>] [--expect-b <line>] [-- <extra BenchmarkDotNet arguments>]

<dir> is a folder that holds a built ModernLibs.FileHelpers.Benchmarks.dll, for example one of the folders
that build-variants.sh writes. The benchmark methods that match --filter in both folders are run N times
each, A and B alternately: A, B, A, B, ... with one method per BenchmarkDotNet launch, so every launch is
a process of its own and no other benchmark runs in it. One sample is the median time of one launch (the
"Median" statistic of the BenchmarkDotNet JSON report); the samples of N launches go into the rule.

Every process prints, before BenchmarkDotNet starts, the variant of the ModernLibs.FileHelpers it loaded,
where it loaded it from and the runtime (Program.cs). The run stops unless that variant is the one the
folder is named after (net10, net11-ra-on, net11-ra-off; --expect-a/--expect-b for other folders), the
library comes from the folder itself and, for a named folder, the .NET major version matches. The lines
go into the result file.

Rule (B4): B differs from A when the 99.9 % bootstrap interval of the ratio median(B) / median(A) excludes
1.0 and the median ratio differs from 1.0 by at least 0.03. The bootstrap draws 10,000 resamples with the
fixed seed 410, so the same samples always give the same interval. A ratio below 1.0 means B is faster.

Python standard library only.
"""

import argparse
import glob
import json
import os
import random
import re
import shutil
import statistics
import subprocess
import sys

RESAMPLES = 10_000
SEED = 410
CONFIDENCE = 0.999
MIN_CHANGE = 0.03
# Below 5 launches per side the interval is too narrow: with no difference, 2 launches exclude 1.0 in about a
# third of runs and 5 in about 1 % (simulated, see test_ab.py). BENCHMARKS.md recommends 10.
MIN_LAUNCHES = 5
BENCHMARK_DLL = "ModernLibs.FileHelpers.Benchmarks.dll"
VARIANT_FILE = "variant.txt"

# The three named configurations of design D6: folder name -> (verify-variant line, .NET major version).
CONFIGURATIONS = {
    "net10": ("tfm=net10.0 runtime-async=off", "10"),
    "net11-ra-on": ("tfm=net11.0 runtime-async=on", "11"),
    "net11-ra-off": ("tfm=net11.0 runtime-async=off", "11"),
}

# Start-up lines every benchmark process prints (Program.cs), before BenchmarkDotNet starts.
_STARTUP_PREFIXES = {
    "variant": "// ModernLibs.FileHelpers variant: ",
    "assembly": "// ModernLibs.FileHelpers assembly: ",
    "runtime": "// Runtime: ",
}

# Floating-point slack for the 3 % floor: 1.0 - 0.97 is 0.030000000000000027, 1.03 - 1.0 likewise.
_EPSILON = 1e-9


def _quantile(sorted_values, p):
    """Linear interpolation between the closest ranks (the default of numpy.quantile)."""
    pos = p * (len(sorted_values) - 1)
    lower = int(pos)
    upper = min(lower + 1, len(sorted_values) - 1)
    return sorted_values[lower] + (sorted_values[upper] - sorted_values[lower]) * (pos - lower)


def compare(a_samples, b_samples):
    """Median ratio B/A, its 99.9 % bootstrap interval and the B4 verdict for two sets of samples."""
    a = [float(x) for x in a_samples]
    b = [float(x) for x in b_samples]
    if len(a) < 2 or len(b) < 2:
        raise ValueError("each side needs at least two samples, got %d (A) and %d (B)" % (len(a), len(b)))
    if min(a) <= 0.0 or min(b) <= 0.0:
        raise ValueError("samples must be positive times")

    rng = random.Random(SEED)
    ratios = sorted(
        statistics.median(rng.choices(b, k=len(b))) / statistics.median(rng.choices(a, k=len(a)))
        for _ in range(RESAMPLES))
    tail = (1.0 - CONFIDENCE) / 2.0
    ci_low = _quantile(ratios, tail)
    ci_high = _quantile(ratios, 1.0 - tail)
    median_ratio = statistics.median(b) / statistics.median(a)

    excludes_one = ci_high < 1.0 or ci_low > 1.0
    large_enough = abs(median_ratio - 1.0) + _EPSILON >= MIN_CHANGE
    if excludes_one and large_enough:
        result = "faster" if median_ratio < 1.0 else "slower"
    else:
        result = "no-change"
    return {
        "median_ratio": median_ratio,
        "ci_low": ci_low,
        "ci_high": ci_high,
        "confidence": CONFIDENCE,
        "resamples": RESAMPLES,
        "verdict": result,
    }


def verdict(a_samples, b_samples):
    """'faster', 'slower' or 'no-change' for B against A, by the B4 rule."""
    return compare(a_samples, b_samples)["verdict"]


def parse_reports(artifacts_dir):
    """Benchmark full name (with parameters) -> median time in ns, from the JSON reports of one launch."""
    paths = sorted(glob.glob(os.path.join(artifacts_dir, "results", "*-report-full.json")))
    if not paths:
        raise RuntimeError("no *-report-full.json under %s/results (is BenchConfig in use?)" % artifacts_dir)
    medians = {}
    for path in paths:
        with open(path, encoding="utf-8") as f:
            report = json.load(f)
        for bench in report.get("Benchmarks", []):
            stats = bench.get("Statistics")
            if not stats or stats.get("Median") is None:
                raise RuntimeError("%s: no statistics for %s (did the benchmark fail?)" % (path, bench.get("FullName")))
            medians[bench["FullName"]] = float(stats["Median"])
    return medians


def run_ab(a_dir, b_dir, launches, methods, launch, work_dir):
    """Runs every method `launches` times, A then B, and applies the rule to each benchmark case.

    launch(side, bench_dir, method, artifacts_dir) runs one BenchmarkDotNet process for one method and
    returns {benchmark full name: median ns}.
    """
    samples = {"a": {}, "b": {}}
    order = []
    for i in range(launches):
        for method in methods:
            for side, bench_dir in (("a", a_dir), ("b", b_dir)):
                artifacts = os.path.join(work_dir, "%03d-%s-%s" % (i + 1, side, method))
                medians = launch(side, bench_dir, method, artifacts)
                if not medians:
                    raise RuntimeError("launch %d of %s on side %s returned no results" % (i + 1, method, side))
                for name, median in medians.items():
                    if name not in samples[side]:
                        samples[side][name] = []
                        if side == "a":
                            order.append(name)
                    samples[side][name].append(median)

    results = []
    for name in order:
        a = samples["a"].get(name, [])
        b = samples["b"].get(name, [])
        if len(a) != launches or len(b) != launches:
            raise RuntimeError("%s: %d A and %d B samples, expected %d each" % (name, len(a), len(b), launches))
        row = {"name": name, "a_median_ns": a, "b_median_ns": b}
        row.update(compare(a, b))
        results.append(row)
    missing = sorted(set(samples["b"]) - set(order))
    if missing:
        raise RuntimeError("benchmark cases only on side B: %s" % ", ".join(missing))
    return results


def _dll(bench_dir):
    path = os.path.join(bench_dir, BENCHMARK_DLL)
    if not os.path.isfile(path):
        raise RuntimeError("%s not found" % path)
    return path


def _dotnet():
    exe = shutil.which("dotnet")
    if exe is None:
        raise RuntimeError("dotnet is not on PATH")
    return exe


def list_methods(bench_dir, pattern):
    """The benchmark methods (Namespace.Type.Method) of one build that match the BenchmarkDotNet glob."""
    out = subprocess.run(
        [_dotnet(), _dll(bench_dir), "--list", "flat", "--filter", pattern],
        check=True, capture_output=True, text=True).stdout
    name = re.compile(r"^[A-Za-z_][\w.]*\.\w+$")
    return sorted({line.strip() for line in out.splitlines() if name.match(line.strip())})


def _variant(bench_dir):
    path = os.path.join(bench_dir, VARIANT_FILE)
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as f:
        return f.read().strip()


def expected_variant(bench_dir, override):
    """(variant line, runtime major or None) that the processes started from bench_dir must print.

    For a folder named after a configuration (net10, net11-ra-on, net11-ra-off, as build-variants.sh names
    them) the line and the .NET major version follow from the name; for any other folder the caller states
    the line (--expect-a / --expect-b) and the runtime is recorded but not checked.
    """
    name = os.path.basename(os.path.normpath(bench_dir))
    if override is not None:
        if name in CONFIGURATIONS and CONFIGURATIONS[name][0] != override:
            raise ValueError("%s is the %s configuration, which is '%s', not '%s'"
                             % (bench_dir, name, CONFIGURATIONS[name][0], override))
        return (override, CONFIGURATIONS[name][1] if name in CONFIGURATIONS else None)
    if name not in CONFIGURATIONS:
        raise ValueError("%s is not named after a configuration (%s); state its variant with --expect-a/--expect-b"
                         % (bench_dir, ", ".join(sorted(CONFIGURATIONS))))
    return CONFIGURATIONS[name]


def check_startup(log_text, bench_dir, expected):
    """The start-up lines of one benchmark process (printed by Program.cs), checked against the folder.

    Refuses a process whose library variant is not the expected one, whose ModernLibs.FileHelpers.dll was not
    loaded from bench_dir, or whose .NET major version is not the expected one.
    """
    found = {}
    for line in log_text.splitlines():
        for key, prefix in _STARTUP_PREFIXES.items():
            if line.startswith(prefix) and key not in found:
                found[key] = line[len(prefix):].strip()
    missing = [key for key in _STARTUP_PREFIXES if key not in found]
    if missing:
        raise RuntimeError("no start-up %s line in the output of a process from %s" % (", ".join(missing), bench_dir))

    variant, runtime_major = expected
    if found["variant"] != variant:
        raise RuntimeError("process from %s printed '%s', expected '%s'" % (bench_dir, found["variant"], variant))
    loaded_from = os.path.dirname(os.path.realpath(found["assembly"]))
    if loaded_from != os.path.realpath(bench_dir):
        raise RuntimeError("process from %s loaded ModernLibs.FileHelpers from %s" % (bench_dir, found["assembly"]))
    if runtime_major is not None and not found["runtime"].startswith(".NET %s." % runtime_major):
        raise RuntimeError("process from %s ran on '%s', expected .NET %s" % (bench_dir, found["runtime"], runtime_major))
    return found


def _process_launcher(bdn_args, expected, startups):
    """Launches one BenchmarkDotNet process; checks its start-up lines and records them in startups[side]."""
    def launch(side, bench_dir, method, artifacts_dir):
        os.makedirs(artifacts_dir, exist_ok=True)
        log_path = os.path.join(artifacts_dir, "console.log")
        cmd = [_dotnet(), _dll(bench_dir), "--filter", method, "--artifacts", artifacts_dir] + bdn_args
        print("[%s] %s" % (side.upper(), method), flush=True)
        with open(log_path, "w", encoding="utf-8") as log:
            code = subprocess.run(cmd, cwd=artifacts_dir, stdout=log, stderr=subprocess.STDOUT, check=False).returncode
        with open(log_path, encoding="utf-8") as log:
            startup = check_startup(log.read(), bench_dir, expected[side])
        if startups.setdefault(side, startup) != startup:
            raise RuntimeError("side %s: start-up lines changed between launches: %s, then %s"
                               % (side, startups[side], startup))
        if code != 0:
            raise RuntimeError("%s exited with %d, see %s" % (" ".join(cmd), code, log_path))
        return parse_reports(artifacts_dir)
    return launch


def main(argv):
    if "--" in argv:
        split = argv.index("--")
        argv, bdn_args = argv[:split], argv[split + 1:]
    else:
        bdn_args = []
    parser = argparse.ArgumentParser(description="Interleaved A/B BenchmarkDotNet runs with the B4 rule.")
    parser.add_argument("--a", required=True, help="folder with the A build (the baseline)")
    parser.add_argument("--b", required=True, help="folder with the B build (the candidate)")
    parser.add_argument("--launches", required=True, type=int, help="launches per side and method (at least %d)" % MIN_LAUNCHES)
    parser.add_argument("--filter", required=True, help="BenchmarkDotNet --filter glob")
    parser.add_argument("--out", required=True, help="JSON file to write")
    parser.add_argument("--work", help="folder for the per-launch BenchmarkDotNet artifacts (default: <out>.runs)")
    parser.add_argument("--expect-a", help="variant line the A processes must print, for a folder not named "
                        "net10, net11-ra-on or net11-ra-off (for example 'tfm=net10.0 runtime-async=off')")
    parser.add_argument("--expect-b", help="the same for B")
    args = parser.parse_args(argv)
    if args.launches < MIN_LAUNCHES:
        parser.error("--launches must be at least %d" % MIN_LAUNCHES)

    a_dir = os.path.abspath(args.a)
    b_dir = os.path.abspath(args.b)
    try:
        expected = {"a": expected_variant(a_dir, args.expect_a), "b": expected_variant(b_dir, args.expect_b)}
    except ValueError as e:
        parser.error(str(e))
    work_dir = os.path.abspath(args.work or args.out + ".runs")
    a_methods = list_methods(a_dir, args.filter)
    b_methods = list_methods(b_dir, args.filter)
    methods = sorted(set(a_methods) & set(b_methods))
    if not methods:
        raise RuntimeError("no benchmark matches %r in both builds" % args.filter)

    startups = {}
    results = run_ab(a_dir, b_dir, args.launches, methods, _process_launcher(bdn_args, expected, startups), work_dir)
    document = {
        "rule": {
            "ratio": "median(B) / median(A) of the per-launch medians; below 1.0 means B is faster",
            "confidence": CONFIDENCE,
            "resamples": RESAMPLES,
            "seed": SEED,
            "min_change": MIN_CHANGE,
        },
        "a": {"dir": a_dir, "variant": _variant(a_dir), "startup": startups.get("a")},
        "b": {"dir": b_dir, "variant": _variant(b_dir), "startup": startups.get("b")},
        "launches": args.launches,
        "filter": args.filter,
        "bdn_args": bdn_args,
        "only_in_a": sorted(set(a_methods) - set(b_methods)),
        "only_in_b": sorted(set(b_methods) - set(a_methods)),
        "benchmarks": results,
    }
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(document, f, indent=2)
        f.write("\n")

    for r in results:
        print("%-90s %.4f [%.4f, %.4f] %s" % (r["name"], r["median_ratio"], r["ci_low"], r["ci_high"], r["verdict"]))
    if document["only_in_a"] or document["only_in_b"]:
        print("not compared (in one build only): %s" % ", ".join(document["only_in_a"] + document["only_in_b"]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
