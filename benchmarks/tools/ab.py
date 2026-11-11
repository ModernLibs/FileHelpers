#!/usr/bin/env python3
"""Interleaved A/B runner for the BenchmarkDotNet suite, with the acceptance rule B4 of the 4.10 design.

Usage (from any directory; see BENCHMARKS.md):

    python3 -I benchmarks/tools/ab.py --a <dir> --b <dir> --launches N --filter '<glob>' --out <json> \
        [--work <dir>] [--expect-a <line>] [--expect-b <line>] [--smoke] [--allow-dirty] \
        [-- <extra BenchmarkDotNet arguments>]

A B4 result needs at least 7 launches; --smoke allows 5 and marks the result "b4": false. Folders built from
a tree with uncommitted changes (variant.txt) are refused unless --allow-dirty. Each launch folder is
emptied before its launch, and a launch that writes no report stops the run.

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
import hashlib
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
# Launch minimum (lead ruling R20, review-T08 Important 2). With n launches per side and no real difference
# the 99.9 % interval excludes 1.0 when all B samples lie on one side of all A samples: 2 / C(2n, n), which
# is 0.79 % for n = 5 (8 times the nominal 0.1 %) and 0.058 % for n = 7. A B4 result needs 7 launches;
# 5 are allowed with --smoke, and such a result is marked as not meeting B4.
MIN_LAUNCHES_B4 = 7
MIN_LAUNCHES_SMOKE = 5
BENCHMARK_DLL = "ModernLibs.FileHelpers.Benchmarks.dll"
LIBRARY_DLL = "ModernLibs.FileHelpers.dll"
VARIANT_FILE = "variant.txt"
DIRTY_MARK = "(uncommitted changes)"

# BenchmarkDotNet options that make a launch measure nothing or that ab.py sets itself.
_FORBIDDEN_BDN_ARGS = {"--list", "--help", "-h", "-?", "--info", "--version", "--filter", "-f", "--artifacts",
                       "-a", "--resume"}

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
    """The benchmark methods (Namespace.Type.Method) of one build that match the BenchmarkDotNet glob.

    The benchmark program exits 1 after --list because it measured nothing (Program.cs), so the exit code is
    not checked here; the start-up lines and at least one listed method are.
    """
    proc = subprocess.run(
        [_dotnet(), _dll(bench_dir), "--list", "flat", "--filter", pattern],
        check=False, capture_output=True, text=True)
    if not proc.stdout.startswith(_STARTUP_PREFIXES["variant"]):
        raise RuntimeError("listing the benchmarks of %s failed (exit %d): %s%s"
                           % (bench_dir, proc.returncode, proc.stdout[-2000:], proc.stderr[-2000:]))
    name = re.compile(r"^[A-Za-z_][\w.]*\.\w+$")
    return sorted({line.strip() for line in proc.stdout.splitlines() if name.match(line.strip())})


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


def launch_policy(launches, smoke):
    """True when a run with this many launches can meet rule B4; ValueError below the minimum."""
    minimum = MIN_LAUNCHES_SMOKE if smoke else MIN_LAUNCHES_B4
    if launches < minimum:
        raise ValueError("--launches must be at least %d%s" % (minimum, " with --smoke" if smoke else
                         " for a B4 result (at least %d with --smoke, which is not B4)" % MIN_LAUNCHES_SMOKE))
    return not smoke


def table_header(b4, launches):
    """First line of the printed result table."""
    if b4:
        return "# B4 result: %d launches per side, 99.9 %% interval, 3 %% floor" % launches
    return "# smoke, not B4: %d launches per side; no verdict below is a B4 result" % launches


def check_bdn_args(bdn_args):
    """Refuses BenchmarkDotNet arguments that make a launch measure nothing or that ab.py sets itself."""
    for arg in bdn_args:
        if arg.split("=", 1)[0] in _FORBIDDEN_BDN_ARGS:
            raise ValueError("BenchmarkDotNet argument %s is not allowed after -- (it measures nothing or ab.py sets it)"
                             % arg)


def _read_variant_file(text):
    lines = text.strip().splitlines()
    fields = dict(line.split("=", 1) for line in lines[1:] if "=" in line)
    return {"line": lines[0] if lines else "", "commit": fields.get("commit"), "sdk": fields.get("sdk")}


def check_variant_files(a_text, b_text, allow_dirty):
    """Warnings about the two folders' variant.txt; ValueError for a build from uncommitted changes.

    A build from a tree with uncommitted changes is refused unless allow_dirty: its commit line does not say
    what was measured. Different commits on the two sides are legitimate (a 4.9.1 build against a 4.10 build)
    and give a warning, as does a folder without variant.txt (not built by build-variants.sh).
    """
    warnings = []
    infos = {}
    for side, text in (("A", a_text), ("B", b_text)):
        if text is None:
            warnings.append("side %s has no %s: not built by build-variants.sh, commit unknown" % (side, VARIANT_FILE))
            continue
        info = _read_variant_file(text)
        infos[side] = info
        if info["commit"] is None or DIRTY_MARK in info["commit"]:
            message = "side %s was built from a tree with uncommitted changes (%s: commit=%s)" % (
                side, VARIANT_FILE, info["commit"])
            if not allow_dirty:
                raise ValueError(message + "; build from a commit, or pass --allow-dirty for a smoke run")
            warnings.append(message)
    def base_commit(info):
        return (info["commit"] or "").replace(DIRTY_MARK, "").strip()

    if len(infos) == 2 and base_commit(infos["A"]) != base_commit(infos["B"]):
        warnings.append("A and B were built from different commits: %s and %s" % (infos["A"]["commit"], infos["B"]["commit"]))
    return warnings


def _sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def _process_launcher(bdn_args, expected, startups, hashes, run=subprocess.run, dotnet=None):
    """Launches one BenchmarkDotNet process for one method and returns the medians of its own report.

    The launch folder is emptied first, so no report of an earlier run can be read. The run stops when the
    process exits non-zero, when its start-up lines do not match the folder (check_startup) or change between
    launches, when the library DLL changes between launches (SHA-256), or when the process wrote no report.
    """
    def launch(side, bench_dir, method, artifacts_dir):
        library = os.path.join(bench_dir, LIBRARY_DLL)
        digest = _sha256(library)
        if hashes.setdefault(side, digest) != digest:
            raise RuntimeError("side %s: %s changed between launches (rebuilt during the run?)" % (side, library))
        if os.path.exists(artifacts_dir):
            shutil.rmtree(artifacts_dir)
        os.makedirs(artifacts_dir)
        log_path = os.path.join(artifacts_dir, "console.log")
        cmd = [dotnet or _dotnet(), _dll(bench_dir), "--filter", method, "--artifacts", artifacts_dir] + bdn_args
        print("[%s] %s" % (side.upper(), method), flush=True)
        with open(log_path, "w", encoding="utf-8") as log:
            code = run(cmd, cwd=artifacts_dir, stdout=log, stderr=subprocess.STDOUT, check=False).returncode
        if code != 0:
            raise RuntimeError("%s exited with %d, see %s" % (" ".join(cmd), code, log_path))
        with open(log_path, encoding="utf-8") as log:
            try:
                startup = check_startup(log.read(), bench_dir, expected[side])
            except RuntimeError as e:
                raise RuntimeError("%s (see %s)" % (e, log_path)) from None
        if startups.setdefault(side, startup) != startup:
            raise RuntimeError("side %s: start-up lines changed between launches: %s, then %s"
                               % (side, startups[side], startup))
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
    parser.add_argument("--launches", required=True, type=int,
                        help="launches per side and method (at least %d; %d with --smoke)" % (MIN_LAUNCHES_B4, MIN_LAUNCHES_SMOKE))
    parser.add_argument("--filter", required=True, help="BenchmarkDotNet --filter glob")
    parser.add_argument("--out", required=True, help="JSON file to write")
    parser.add_argument("--work", help="folder for the per-launch BenchmarkDotNet artifacts (default: <out>.runs)")
    parser.add_argument("--expect-a", help="variant line the A processes must print, for a folder not named "
                        "net10, net11-ra-on or net11-ra-off (for example 'tfm=net10.0 runtime-async=off')")
    parser.add_argument("--expect-b", help="the same for B")
    parser.add_argument("--smoke", action="store_true",
                        help="allow %d to %d launches; the result is marked as not meeting B4"
                        % (MIN_LAUNCHES_SMOKE, MIN_LAUNCHES_B4 - 1))
    parser.add_argument("--allow-dirty", action="store_true",
                        help="accept folders built from a tree with uncommitted changes (smoke runs only)")
    args = parser.parse_args(argv)

    a_dir = os.path.abspath(args.a)
    b_dir = os.path.abspath(args.b)
    try:
        b4 = launch_policy(args.launches, args.smoke)
        check_bdn_args(bdn_args)
        expected = {"a": expected_variant(a_dir, args.expect_a), "b": expected_variant(b_dir, args.expect_b)}
        variant_warnings = check_variant_files(_variant(a_dir), _variant(b_dir), args.allow_dirty)
    except ValueError as e:
        parser.error(str(e))
    for warning in variant_warnings:
        print("warning: " + warning, file=sys.stderr, flush=True)
    work_dir = os.path.abspath(args.work or args.out + ".runs")
    a_methods = list_methods(a_dir, args.filter)
    b_methods = list_methods(b_dir, args.filter)
    methods = sorted(set(a_methods) & set(b_methods))
    if not methods:
        raise RuntimeError("no benchmark matches %r in both builds" % args.filter)

    startups = {}
    hashes = {}
    results = run_ab(a_dir, b_dir, args.launches, methods,
                     _process_launcher(bdn_args, expected, startups, hashes), work_dir)
    document = {
        "b4": b4,
        "warnings": variant_warnings,
        "rule": {
            "ratio": "median(B) / median(A) of the per-launch medians; below 1.0 means B is faster",
            "confidence": CONFIDENCE,
            "resamples": RESAMPLES,
            "seed": SEED,
            "min_change": MIN_CHANGE,
        },
        "a": {"dir": a_dir, "variant": _variant(a_dir), "startup": startups.get("a"), "library_sha256": hashes.get("a")},
        "b": {"dir": b_dir, "variant": _variant(b_dir), "startup": startups.get("b"), "library_sha256": hashes.get("b")},
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

    print(table_header(b4, args.launches))
    for r in results:
        print("%-90s %.4f [%.4f, %.4f] %s" % (r["name"], r["median_ratio"], r["ci_low"], r["ci_high"], r["verdict"]))
    if document["only_in_a"] or document["only_in_b"]:
        print("not compared (in one build only): %s" % ", ".join(document["only_in_a"] + document["only_in_b"]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
