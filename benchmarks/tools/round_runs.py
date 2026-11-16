#!/usr/bin/env python3
"""The A/B runs of a benchmark round (T12): round.sh calls this after the builds and the checks.

Usage (round.sh passes everything; see round.sh for the folders):

    python3 -I benchmarks/tools/round_runs.py --raw <raw/round/vm> --bin <work/bin> --src <clone> \
        --filters <filters.txt> --launches N --checks-cwd <dir> [--dry] [--job NAME] [--max-iterations N] [--resume]
        [--max-rows N] [--env NAME=VALUE]... [--compare]
    python3 -I benchmarks/tools/round_runs.py --check-filters <filters.txt> [--base-commit SHA]

--max-rows N and every --env NAME=VALUE go into the environment of every process the runs start (ab.py and the
benchmark processes under it): FH_BENCH_MAX_ROWS=N (BenchRows.cs) and the named variables. The pairs base-net10 and
base-net11 need the folders of round.sh --base-commit; a filter file that names them is refused without it
("all" in the pairs column means every pair of the harness except these two).
--compare (T59) is the one-off library comparison, not an A/B: see run_compare.

For every pair and filter line it runs ab.py once (one result file per pair and line). The two lines of group
"prototype" (gates R6 and G2) run first, then every other line, pair by pair. A glob that selects nothing in one
build of a BenchmarkDotNet pair goes to not-run.txt. A failed ab.py run goes to failed.txt (pair, group, glob, log)
and the round goes on; the last line is "ROUND COMPLETE <n> result files, <m> FAILED" and the exit code is 1 when
m > 0 or n = 0. With --resume a pair and line whose result file exists already is kept and not run again, so a
second call finishes a round after a failure; a result file is kept only when it was made for the same glob, by the
same two folders, from the same binaries (SHA-256 of the library DLL or the AOT executable), and round.sh first
checks with --check-resume that the commit, the mode line and the filter file are those of the round's first call. commands.txt and timing.txt are appended; not-run.txt and failed.txt
describe the last call.

Python standard library only.
"""

import argparse
import datetime
import hashlib
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
import time

# pair, A folder, B folder, harness. Folders are relative to the round's bin folder.
PAIRS = [
    ("free-gains", "pkg-net10", "pkg-net11", "bdn"),
    ("sdk-net10", "pkg-net10", "control-net10", "bdn"),
    ("sdk-net11", "pkg-net11", "control-net11-ra-on", "bdn"),
    ("ra-control", "control-net11-ra-off", "control-net11-ra-on", "bdn"),
    ("v410-net10", "pkg-net10", "net10", "bdn"),
    ("v410-net11", "pkg-net11", "net11-ra-on", "bdn"),
    ("ra", "net11-ra-off", "net11-ra-on", "bdn"),
    ("runtime-410", "net10", "net11-ra-on", "bdn"),
    ("base-net10", "base-net10", "net10", "bdn"),
    ("base-net11", "base-net11-ra-on", "net11-ra-on", "bdn"),
    ("aot-v410-net10", "aot/pkg-net10", "aot/net10", "aot"),
    ("aot-v410-net11", "aot/pkg-net11", "aot/net11-ra-on", "aot"),
    ("aot-v410-net11-ra-off", "aot/pkg-net11", "aot/net11-ra-off", "aot"),
    ("aot-ra", "aot/net11-ra-off", "aot/net11-ra-on", "aot"),
]
PAIR_NAMES = [p[0] for p in PAIRS]
BASE_PAIRS = ("base-net10", "base-net11")      # their A folders are built from round.sh --base-commit
MAX_ROWS_VARIABLE = "FH_BENCH_MAX_ROWS"        # BenchRows.cs
COMPARE_CONFIGS = ("net10", "net11-ra-on", "net11-ra-off")
COMPARE_LAUNCHES = 3
COMPARE_LIBRARY_PREFIXES = ("FileHelpers", "ModernLibs")   # the methods that run in net11-ra-off (see run_compare)
# The BenchmarkDotNet folders ab.py does not know by name, with the variant line their processes print.
EXPECT = {
    "pkg-net10": "tfm=net10.0 runtime-async=off",
    "pkg-net11": "tfm=net10.0 runtime-async=off",
    "control-net10": "tfm=net10.0 runtime-async=off",
    "control-net11-ra-on": "tfm=net11.0 runtime-async=on",
    "control-net11-ra-off": "tfm=net11.0 runtime-async=off",
    "base-net10": "tfm=net10.0 runtime-async=off",
    "base-net11-ra-on": "tfm=net11.0 runtime-async=on",
}
AOT_GROUP = "aot"
MIN_LAUNCHES_B4 = 7          # ab.py refuses fewer without --smoke (lead ruling R20)
LIBRARY_DLL = "ModernLibs.FileHelpers.dll"
GATE_GROUP = "prototype"
# Arguments of the Native AOT harness (T47): a round reads 100,000 rows, 5 untimed and 20 timed reads per launch.
AOT_ARGS = ["--rows", "100000", "--warmup", "5", "--iterations", "20"]
AOT_ARGS_DRY = ["--rows", "10000", "--warmup", "1", "--iterations", "3"]
_METHOD_LINE = re.compile(r"^[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z0-9_]+$")


class FilterLine:
    def __init__(self, lineno, group, pairs, glob):
        self.lineno = lineno
        self.group = group
        self.groups = group.split(",")
        self.pairs = pairs
        self.glob = glob

    @property
    def harness(self):
        return "aot" if AOT_GROUP in self.groups else "bdn"


def read_filters(text):
    """[FilterLine]; ValueError naming the line for a malformed one."""
    lines = []
    for lineno, raw in enumerate(text.splitlines(), start=1):
        stripped = raw.strip()
        if not stripped or stripped.startswith("#"):
            continue
        parts = stripped.split()
        if len(parts) != 3:
            raise ValueError("line %d: expected '<group> <pairs> <glob>'" % lineno)
        group, pair_list, glob = parts
        line = FilterLine(lineno, group, [], glob)
        if AOT_GROUP in line.groups and len(line.groups) > 1:
            raise ValueError("line %d: group aot cannot be combined with another group" % lineno)
        if pair_list == "all":
            pairs = [p for p, _, _, h in PAIRS if h == line.harness and p not in BASE_PAIRS]
        else:
            pairs = pair_list.split(",")
        for pair in pairs:
            if pair not in PAIR_NAMES:
                raise ValueError("line %d: unknown pair '%s'" % (lineno, pair))
            harness = dict((p, h) for p, _, _, h in PAIRS)[pair]
            if harness != line.harness:
                raise ValueError("line %d: group '%s' cannot run in pair '%s' (group aot runs in the aot-* pairs only)"
                                 % (lineno, group, pair))
        line.pairs = pairs
        lines.append(line)
    if not lines:
        raise ValueError("no filter line")
    return lines


def uses_base_pairs(lines):
    """The base pairs the filter lines name (sorted)."""
    return sorted({pair for line in lines for pair in line.pairs if pair in BASE_PAIRS})


def parse_env(items):
    """{NAME: VALUE} of --env NAME=VALUE items; ValueError for a malformed or repeated one."""
    env = {}
    for item in items or []:
        name, sep, value = item.partition("=")
        if not sep or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            raise ValueError("--env %r is not NAME=VALUE" % item)
        if name in env or name == MAX_ROWS_VARIABLE:
            raise ValueError("--env %s is set twice (or is the variable --max-rows sets)" % name)
        env[name] = value
    return env


def child_env(max_rows, env):
    """The variables added to the environment of every process of a round: --max-rows and --env."""
    added = dict(env or {})
    if max_rows:
        added[MAX_ROWS_VARIABLE] = str(max_rows)
    return added


def plan(lines):
    """[(pair, a, b, harness, line)] in run order: the gate lines first, then pair by pair, line by line."""
    def runs(selected):
        out = []
        for pair, a, b, harness in PAIRS:
            for line in selected:
                if pair in line.pairs:
                    out.append((pair, a, b, harness, line))
        return out
    gates = [line for line in lines if GATE_GROUP in line.groups]
    rest = [line for line in lines if GATE_GROUP not in line.groups]
    return runs(gates) + runs(rest)


def slug(glob):
    return re.sub(r"[^A-Za-z0-9_.-]", "_", glob).strip("_")


def result_path(raw, pair, line):
    return os.path.join(raw, pair, "%03d-%s-%s.json" % (line.lineno, line.group.replace(",", "+"), slug(line.glob)))


def bdn_args(job, max_iterations):
    args = []
    if job:
        args += ["--job", job]
    if max_iterations:
        args += ["--maxIterationCount", str(max_iterations)]
    return args


def command(src, bin_dir, pair, a, b, harness, line, out, launches, dry, job, max_iterations):
    ab = os.path.join(src, "benchmarks", "tools", "ab.py")
    cmd = ["python3", "-I", ab]
    if harness == "aot":
        # ab.py --aot knows the five AOT folder names (T47 fix round 1): no --expect-a/--expect-b.
        cmd += ["--aot", "--a", os.path.join(bin_dir, a), "--b", os.path.join(bin_dir, b), "--launches", str(launches)]
        if dry:
            cmd.append("--smoke")
        cmd += ["--filter", line.glob, "--out", out, "--"] + (AOT_ARGS_DRY if dry else AOT_ARGS)
        return cmd
    cmd += ["--a", os.path.join(bin_dir, a), "--b", os.path.join(bin_dir, b), "--launches", str(launches)]
    if dry:
        cmd.append("--smoke")
    cmd += ["--filter", line.glob, "--out", out]
    if a in EXPECT:
        cmd += ["--expect-a", EXPECT[a]]
    if b in EXPECT:
        cmd += ["--expect-b", EXPECT[b]]
    extra = bdn_args(job, max_iterations)
    if extra:
        cmd += ["--"] + extra
    return cmd


def complete_result(path):
    """True when path is an ab.py result file (a run that finished)."""
    try:
        with open(path, encoding="utf-8") as f:
            document = json.load(f)
    except (OSError, ValueError):
        return False
    return isinstance(document, dict) and "benchmarks" in document and "a" in document


def file_sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def folder_hash(bin_dir, folder, harness):
    """SHA-256 of what ab.py hashes for a folder: the library DLL, or the AOT executable; None when it is missing."""
    path = os.path.join(bin_dir, folder, AOT_EXECUTABLE if harness == "aot" else LIBRARY_DLL)
    return file_sha256(path) if os.path.isfile(path) else None


def reusable_result(path, glob, a_dir, b_dir, hashes):
    """(True, None) when the result file at path was made for this glob by these two folders from these binaries
    (hashes: {'a': sha, 'b': sha}); otherwise (False, reason). Used by --resume (rereview-T12 Important 1)."""
    try:
        with open(path, encoding="utf-8") as f:
            document = json.load(f)
    except (OSError, ValueError):
        return False, "missing or damaged result file"
    if not (isinstance(document, dict) and "benchmarks" in document and "a" in document and "b" in document):
        return False, "not an ab.py result file"
    if document.get("filter") != glob:
        return False, "made for the glob %r, not %r" % (document.get("filter"), glob)
    key = "executable_sha256" if document.get("harness") == "aot" else "library_sha256"
    for side, folder in (("a", a_dir), ("b", b_dir)):
        made_by = os.path.normpath(document[side].get("dir") or "")
        if made_by != os.path.normpath(folder):
            return False, "side %s was %s, not %s" % (side.upper(), made_by, folder)
        if document[side].get(key) != hashes.get(side):
            return False, "side %s was measured from another binary (%s %s, now %s)" % (
                side.upper(), key, document[side].get(key), hashes.get(side))
    return True, None


def _first_block_value(machine_text, prefix):
    for text in machine_text.splitlines():
        if text.startswith("== "):
            break                      # the first block ends where the machine records start
        if text.startswith(prefix):
            return text[len(prefix):].strip()
    return None


def check_resume(machine_text, commit, mode, filter_sha):
    """Reasons why a --resume call does not continue the round recorded in machine.txt (empty: it does): another
    commit, another mode line (launches, job, iteration cap, --dry), or another filter file (SHA-256)."""
    reasons = []
    first_commit = (_first_block_value(machine_text, "commit: ") or "").split(" ")[0]
    if first_commit != commit:
        reasons.append("measured at %s, not %s; results of two commits never mix" % (first_commit or "?", commit))
    first_mode = _first_block_value(machine_text, "mode: ")
    if first_mode != mode:
        reasons.append("measured as '%s', this call is '%s'" % (first_mode, mode))
    filter_line = _first_block_value(machine_text, "filter file: ") or ""
    first_filter = filter_line.rsplit("sha256 ", 1)[-1].strip() if "sha256 " in filter_line else None
    if first_filter != filter_sha:
        reasons.append("measured with the filter file sha256 %s, this call has %s" % (first_filter, filter_sha))
    return reasons


def _utc():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def run_round(raw, bin_dir, src, lines, launches, dry, job, max_iterations, resume, lister, runner,
              out=None, clock=time.time, utc=_utc, hasher=None, env=None):
    """Runs the plan. lister(folder, glob) -> bool tells whether a BenchmarkDotNet build has a method matching glob;
    runner(cmd, log_path, env) -> exit code runs ab.py with the variables of env added to its environment (env: see
    child_env); hasher(folder, harness) -> SHA-256 of the binary ab.py hashes (default: folder_hash).
    Returns (results, failed)."""
    out = out or sys.stdout
    if hasher is None:
        def hasher(folder, harness):
            return folder_hash(bin_dir, folder, harness)
    for name in ("not-run.txt", "failed.txt"):
        open(os.path.join(raw, name), "w", encoding="utf-8").close()
    with open(os.path.join(raw, "commands.txt"), "a", encoding="utf-8") as f:
        f.write("# %s %s\n" % ("resumed" if resume else "started", utc()))
    results = failed = kept = 0
    for pair, a, b, harness, line in plan(lines):
        result = result_path(raw, pair, line)
        if resume and os.path.exists(result):
            ok, why = reusable_result(result, line.glob, os.path.join(bin_dir, a), os.path.join(bin_dir, b),
                                      {"a": hasher(a, harness), "b": hasher(b, harness)})
            if ok:
                kept += 1
                results += 1
                print("kept (resume): %s %s" % (pair, line.glob), file=out, flush=True)
                continue
            print("run again (resume): %s %s: %s" % (pair, line.glob, why), file=out, flush=True)
        if harness == "bdn":
            missing = [folder for folder in (a, b) if not lister(folder, line.glob)]
            if missing:
                with open(os.path.join(raw, "not-run.txt"), "a", encoding="utf-8") as f:
                    f.write("%s\t%s\t%s\tno benchmark matches in %s\n" % (pair, line.group, line.glob,
                                                                         " and ".join(missing)))
                print("not run: %s %s (no benchmark matches in %s)" % (pair, line.glob, " and ".join(missing)),
                      file=out, flush=True)
                continue
        os.makedirs(os.path.dirname(result), exist_ok=True)
        cmd = command(src, bin_dir, pair, a, b, harness, line, result, launches, dry, job, max_iterations)
        with open(os.path.join(raw, "commands.txt"), "a", encoding="utf-8") as f:
            f.write(" ".join(shlex.quote(c) for c in cmd) + "\n")
        log = result[:-len(".json")] + ".log"
        print("== %s (%s -> %s): %s" % (pair, a, b, line.glob), file=out, flush=True)
        started, t0 = utc(), clock()
        code = runner(cmd, log, env or {})
        with open(os.path.join(raw, "timing.txt"), "a", encoding="utf-8") as f:
            f.write("%s\t%s\t%s\t%s\t%ds\t%s\n" % (pair, line.glob, started, utc(), round(clock() - t0),
                                                   "ok" if code == 0 else "FAILED exit %d" % code))
        if code != 0 or not complete_result(result):
            failed += 1
            with open(os.path.join(raw, "failed.txt"), "a", encoding="utf-8") as f:
                f.write("%s\t%s\t%s\t%s\n" % (pair, line.group, line.glob, os.path.relpath(log, raw)))
            print("FAILED: %s %s (exit %d, see %s)" % (pair, line.glob, code, log), file=out, flush=True)
            _print_tail(log, out)
            continue
        results += 1
        _print_table(log, out)
    if kept:
        print("resume: %d result files kept from an earlier call" % kept, file=out)
    return results, failed


def _print_table(log, out):
    """ab.py's result table from its log (the lines that are not per-launch progress)."""
    try:
        with open(log, encoding="utf-8", errors="replace") as f:
            for text in f:
                if not text.startswith("[A]") and not text.startswith("[B]"):
                    out.write(text)
    except OSError:
        pass
    out.flush()


def _print_tail(log, out, lines=5):
    try:
        with open(log, encoding="utf-8", errors="replace") as f:
            for text in f.readlines()[-lines:]:
                out.write("  " + text)
    except OSError:
        pass
    out.flush()


def process_lister(bin_dir, cwd):
    def lister(folder, glob):
        # The listing process exits 1 by design (it measured nothing, Program.cs): only its output counts.
        proc = subprocess.run(["dotnet", os.path.join(bin_dir, folder, "ModernLibs.FileHelpers.Benchmarks.dll"),
                               "--list", "flat", "--filter", glob], cwd=cwd, capture_output=True, text=True,
                              check=False)
        return any(_METHOD_LINE.match(text.strip()) for text in proc.stdout.splitlines())
    return lister


def process_runner(cmd, log, env=None):
    with open(log, "w", encoding="utf-8") as f:
        return subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT, check=False, env=_environment(env)).returncode


def _environment(added):
    """The environment of a child process: this one's, plus the variables of a round (--max-rows, --env)."""
    environment = dict(os.environ)
    environment.update(added or {})
    return environment


def process_method_lister(bin_dir, cwd):
    """lister(folder, glob) -> the benchmark methods (Namespace.Type.Method) of that build matching the glob."""
    def methods(folder, glob):
        proc = subprocess.run(["dotnet", os.path.join(bin_dir, folder, "ModernLibs.FileHelpers.Benchmarks.dll"),
                               "--list", "flat", "--filter", glob], cwd=cwd, capture_output=True, text=True,
                              check=False)
        return sorted({text.strip() for text in proc.stdout.splitlines() if _METHOD_LINE.match(text.strip())})
    return methods


def compare_launch_dir(raw, config, line, launch):
    return os.path.join(raw, "compare", config, "%03d-%s-%s" % (line.lineno, line.group.replace(",", "+"),
                                                                  slug(line.glob)), "launch-%d" % launch)


def run_compare(raw, bin_dir, lines, launches, dry, job, max_iterations, resume, methods_of, runner,
                out=None, clock=time.time, utc=_utc, env=None, hasher=None):
    """The one-off library comparison (round.sh --compare): not an A/B, no verdict, no ab.py.

    For each of net10, net11-ra-on and net11-ra-off, and each filter line (its pairs are ignored; group aot lines
    are skipped), the benchmark program of that folder runs `launches` times with the glob, BenchmarkDotNet's default
    job (or --job, capped by --max-iterations). In net11-ra-off only the methods whose name starts with FileHelpers
    or ModernLibs run: the other libraries do not change with the runtime-async switch, so their cells stay empty.
    Output: <raw>/compare/<config>/<line>/launch-<k>/ (the BenchmarkDotNet artifacts and console.log); with resume a
    launch folder that holds `done` for the same library DLL is kept. methods_of(folder, glob) -> the matching
    methods; runner(cmd, log, env) -> exit code. Returns (results, failed): a result is one config and line."""
    out = out or sys.stdout
    if hasher is None:
        def hasher(folder, harness):
            return folder_hash(bin_dir, folder, harness)
    for name in ("not-run.txt", "failed.txt"):
        open(os.path.join(raw, name), "w", encoding="utf-8").close()
    with open(os.path.join(raw, "commands.txt"), "a", encoding="utf-8") as f:
        f.write("# %s %s (comparison)\n" % ("resumed" if resume else "started", utc()))
    results = failed = 0
    for config in COMPARE_CONFIGS:
        library_hash = hasher(config, "bdn")
        for line in lines:
            if line.harness != "bdn":
                continue
            methods = methods_of(config, line.glob)
            if config == "net11-ra-off":
                methods = [m for m in methods if m.rsplit(".", 1)[-1].startswith(COMPARE_LIBRARY_PREFIXES)]
            if not methods:
                with open(os.path.join(raw, "not-run.txt"), "a", encoding="utf-8") as f:
                    f.write("compare/%s\t%s\t%s\tno benchmark matches%s\n" % (
                        config, line.group, line.glob,
                        " (net11-ra-off runs only FileHelpers* and ModernLibs* methods)" if config == "net11-ra-off"
                        else ""))
                print("not run: %s %s (no benchmark matches)" % (config, line.glob), file=out, flush=True)
                continue
            # net11-ra-off names its methods: a glob cannot say "starts with FileHelpers" (BenchmarkDotNet globs OR).
            globs = methods if config == "net11-ra-off" else [line.glob]
            print("== compare %s: %s (%d methods, %d launches)" % (config, line.glob, len(methods), launches),
                  file=out, flush=True)
            line_failed = False
            for launch in range(1, launches + 1):
                folder = compare_launch_dir(raw, config, line, launch)
                done = os.path.join(folder, "done")
                if resume and os.path.isfile(done):
                    with open(done, encoding="utf-8") as f:
                        if f.read().strip() == str(library_hash):
                            print("kept (resume): %s %s launch %d" % (config, line.glob, launch), file=out, flush=True)
                            continue
                if os.path.exists(folder):
                    shutil.rmtree(folder)
                os.makedirs(folder)
                cmd = ["dotnet", os.path.join(bin_dir, config, "ModernLibs.FileHelpers.Benchmarks.dll"), "--filter"]
                cmd += globs + ["--artifacts", folder] + bdn_args(job, max_iterations)
                with open(os.path.join(raw, "commands.txt"), "a", encoding="utf-8") as f:
                    f.write(" ".join(shlex.quote(c) for c in cmd) + "\n")
                log = os.path.join(folder, "console.log")
                started, t0 = utc(), clock()
                code = runner(cmd, log, env or {})
                reports = glob_reports(folder)
                with open(os.path.join(raw, "timing.txt"), "a", encoding="utf-8") as f:
                    f.write("compare/%s\t%s\t%s\t%s\t%ds\t%s\n" % (
                        config, line.glob, started, utc(), round(clock() - t0),
                        "ok" if code == 0 and reports else "FAILED exit %d" % code))
                if code != 0 or not reports:
                    line_failed = True
                    with open(os.path.join(raw, "failed.txt"), "a", encoding="utf-8") as f:
                        f.write("compare/%s\t%s\t%s\t%s\n" % (config, line.group, line.glob,
                                                               os.path.relpath(log, raw)))
                    print("FAILED: %s %s launch %d (exit %d, see %s)" % (config, line.glob, launch, code, log),
                          file=out, flush=True)
                    _print_tail(log, out)
                    break
                with open(done, "w", encoding="utf-8") as f:
                    f.write(str(library_hash) + "\n")
            if line_failed:
                failed += 1
            else:
                results += 1
    return results, failed


def glob_reports(folder):
    """The BenchmarkDotNet *-report-full.json files of one launch folder."""
    results = os.path.join(folder, "results")
    if not os.path.isdir(results):
        return []
    return sorted(os.path.join(results, name) for name in os.listdir(results) if name.endswith("-report-full.json"))


# The Native AOT folders of a round (build-aot-variants.sh, T47): name, variant line, .NET major version.
AOT_FOLDERS = [
    ("net10", "tfm=net10.0 runtime-async=off", "10"),
    ("net11-ra-on", "tfm=net11.0 runtime-async=on", "11"),
    ("net11-ra-off", "tfm=net11.0 runtime-async=off", "11"),
    ("pkg-net10", "tfm=net10.0 runtime-async=off", "10"),
    ("pkg-net11", "tfm=net10.0 runtime-async=off", "11"),
]
AOT_EXECUTABLE = "ModernLibs.FileHelpers.Benchmarks.Aot"


def check_aot_folder(folder, name, line, major, cwd=None, run=subprocess.run):
    """The variants.txt block of one AOT folder; ValueError with the reason when the folder does not match its name.

    build-aot-variants.sh checked each publish; this checks the folder that will be measured: the executable exists,
    variant.txt starts with '<name>: <line>', and the executable started with --info prints that variant,
    'native-aot=yes' and the .NET major version.
    """
    exe = os.path.join(folder, AOT_EXECUTABLE)
    if not (os.path.isfile(exe) and os.access(exe, os.X_OK)):
        raise ValueError("no AOT harness executable in the folder")
    try:
        with open(os.path.join(folder, "variant.txt"), encoding="utf-8") as f:
            variant = f.read()
    except OSError:
        variant = ""
    if variant.splitlines()[:1] != ["%s: %s" % (name, line)]:
        raise ValueError("variant.txt does not start with '%s: %s'" % (name, line))
    try:
        proc = run([exe, "--info"], cwd=cwd, capture_output=True, text=True, check=False)
    except OSError as e:
        raise ValueError("cannot start %s: %s" % (exe, e)) from None
    startup = [text for text in (proc.stdout + proc.stderr).splitlines() if text.startswith("// ")]

    def value(prefix):
        for text in startup:
            if text.startswith(prefix):
                return text[len(prefix):].strip()
        return ""
    printed = value("// ModernLibs.FileHelpers variant: ")
    build = value("// Build: ")
    runtime = value("// Runtime: ")
    if printed != line:
        raise ValueError("AOT process printed variant '%s', expected '%s'" % (printed, line))
    if not build.startswith("native-aot=yes "):
        raise ValueError("AOT process printed build '%s', not native-aot=yes" % build)
    if not runtime.startswith(".NET %s." % major):
        raise ValueError("AOT process ran on '%s', expected .NET %s" % (runtime, major))
    return "== aot/%s\n%s\n%s\n\n" % (name, variant.rstrip("\n"), "\n".join(startup))


def check_aot(aot_dir, cwd=None, out=sys.stdout, err=sys.stderr, run=subprocess.run):
    """Checks the five AOT folders; writes their variants.txt blocks to out. 0, or 1 after 'VARIANT MISMATCH <dir>'."""
    for name, line, major in AOT_FOLDERS:
        folder = os.path.join(aot_dir, name)
        try:
            out.write(check_aot_folder(folder, name, line, major, cwd=cwd, run=run))
        except ValueError as e:
            print("VARIANT MISMATCH %s\n  %s" % (folder, e), file=err)
            return 1
    return 0


def main(argv, lister=None, runner=None, methods_lister=None):
    parser = argparse.ArgumentParser(description="The A/B runs of a benchmark round (called by round.sh).")
    parser.add_argument("--check-filters", help="only validate this filter file and exit")
    parser.add_argument("--check-aot", help="only check the five AOT folders under this folder (round.sh step 4), "
                        "print their variants.txt blocks and exit")
    parser.add_argument("--check-resume", metavar="MACHINE_TXT", help="only check that a --resume call (--commit, "
                        "--mode, --filter-sha) continues the round recorded in this machine.txt, and exit")
    parser.add_argument("--commit")
    parser.add_argument("--mode")
    parser.add_argument("--filter-sha")
    parser.add_argument("--raw")
    parser.add_argument("--bin")
    parser.add_argument("--src")
    parser.add_argument("--filters")
    parser.add_argument("--launches", type=int)
    parser.add_argument("--checks-cwd")
    parser.add_argument("--dry", action="store_true")
    parser.add_argument("--job")
    parser.add_argument("--max-iterations", type=int)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--max-rows", type=int, help="FH_BENCH_MAX_ROWS for every process (BenchRows.cs)")
    parser.add_argument("--env", action="append", default=[], metavar="NAME=VALUE",
                        help="an environment variable for every process; repeatable")
    parser.add_argument("--compare", action="store_true", help="the one-off library comparison (run_compare)")
    parser.add_argument("--base-commit", help="with --check-filters: the commit round.sh builds into the base folders")
    args = parser.parse_args(argv)

    if args.check_aot:
        return check_aot(args.check_aot, cwd=args.checks_cwd)
    if args.check_resume:
        try:
            with open(args.check_resume, encoding="utf-8") as f:
                machine_text = f.read()
        except OSError as e:
            print("error: --resume: %s; without the first call's machine.txt nothing can be compared" % e,
                  file=sys.stderr)
            return 1
        if not machine_text.strip():
            print("error: --resume: %s is empty; without the first call's record nothing can be compared"
                  % args.check_resume, file=sys.stderr)
            return 1
        reasons = check_resume(machine_text, args.commit, args.mode, args.filter_sha)
        for reason in reasons:
            print("error: --resume: %s" % reason, file=sys.stderr)
        return 1 if reasons else 0
    path = args.check_filters or args.filters
    if not path:
        parser.error("--filters or --check-filters is required")
    try:
        with open(path, encoding="utf-8") as f:
            lines = read_filters(f.read())
    except (OSError, ValueError) as e:
        print("error: filter file %s: %s" % (path, e), file=sys.stderr)
        return 2
    try:
        env = parse_env(args.env)
    except ValueError as e:
        print("error: %s" % e, file=sys.stderr)
        return 2
    if args.max_rows is not None and args.max_rows < 1:
        print("error: --max-rows needs a positive integer", file=sys.stderr)
        return 2
    base_pairs = uses_base_pairs(lines)
    if base_pairs and not args.base_commit and not args.compare:
        print("error: filter file %s names the pair %s, which needs --base-commit (round.sh --base-commit SHA)"
              % (path, " and ".join(base_pairs)), file=sys.stderr)
        return 2
    if args.check_filters:
        print("filter file %s: %d lines, %d ab.py runs" % (path, len(lines), len(plan(lines))))
        return 0
    for name in ("raw", "bin", "src", "launches", "checks_cwd"):
        if getattr(args, name) is None:
            parser.error("--%s is required" % name.replace("_", "-"))
    if args.compare:
        if args.launches != COMPARE_LAUNCHES:
            print("error: --compare runs %d launches" % COMPARE_LAUNCHES, file=sys.stderr)
            return 2
        compared, compare_failed = run_compare(
            args.raw, args.bin, lines, args.launches, args.dry, args.job, args.max_iterations, args.resume,
            methods_lister or process_method_lister(args.bin, args.checks_cwd), runner or process_runner,
            env=child_env(args.max_rows, env))
        if compared == 0 and compare_failed == 0:
            print("ROUND FAILED: no comparison ran", file=sys.stderr)
            return 1
        print("ROUND COMPLETE %d result files, %d FAILED" % (compared, compare_failed))
        return 1 if compare_failed or compared == 0 else 0
    if not args.dry and args.launches < MIN_LAUNCHES_B4:
        print("error: --launches %d is below %d, the B4 minimum; fewer launches only with --dry"
              % (args.launches, MIN_LAUNCHES_B4), file=sys.stderr)
        return 2

    results, failed = run_round(args.raw, args.bin, args.src, lines, args.launches, args.dry, args.job,
                                args.max_iterations, args.resume,
                                lister or process_lister(args.bin, args.checks_cwd), runner or process_runner,
                                env=child_env(args.max_rows, env))
    if results == 0 and failed == 0:
        print("ROUND FAILED: no result files", file=sys.stderr)
        return 1
    print("ROUND COMPLETE %d result files, %d FAILED" % (results, failed))
    return 1 if failed or results == 0 else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
