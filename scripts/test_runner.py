#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi
"""
Intelligent, Change-Aware Test & Verification Runner for ADCE.
Tracks 7 independent verification domains via Merkle content hashes,
eliminating redundant test runs, full-solution rebuilds, and daemon file lock collisions.
"""

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# Ensure UTF-8 output on Windows
if sys.stdout.encoding != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

REPO_ROOT = Path(__file__).resolve().parent.parent
CACHE_FILE = REPO_ROOT / ".test_cache.json"

# Directories ignored during file scanning
IGNORED_DIRS = {
    ".git",
    ".vs",
    ".vscode",
    "bin",
    "obj",
    "node_modules",
    ".ruff_cache",
    ".idea",
    "__pycache__",
    ".pytest_cache",
    "external",
    "TestResults",
    "logs",
    "artifacts",
}


def compute_file_hash(file_path: Path) -> str:
    """Computes a line-ending normalized SHA-256 hash for a file."""
    hasher = hashlib.sha256()
    try:
        # Normalize text files to LF to avoid CRLF/LF spurious invalidation
        if file_path.suffix.lower() in {".cs", ".csproj", ".py", ".md", ".json", ".xml", ".txt", ".ps1", ".targets", ".props"}:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                content = f.read().replace("\r\n", "\n")
            hasher.update(content.encode("utf-8"))
        else:
            with open(file_path, "rb") as f:
                while chunk := f.read(65536):
                    hasher.update(chunk)
    except Exception as ex:
        hasher.update(f"ERROR:{ex}".encode("utf-8"))
    return hasher.hexdigest()


def compute_domain_fingerprint(file_paths: list[Path]) -> str:
    """Computes a deterministic Merkle fingerprint for a collection of files."""
    entries = []
    for fp in file_paths:
        if fp.is_file():
            rel_path = fp.relative_to(REPO_ROOT).as_posix()
            f_hash = compute_file_hash(fp)
            entries.append(f"{rel_path}:{f_hash}")

    entries.sort()
    combined_hasher = hashlib.sha256()
    for entry in entries:
        combined_hasher.update((entry + "\n").encode("utf-8"))
    return combined_hasher.hexdigest()


def gather_files_matching(root_dir: Path, patterns: list[str], exclude_dirs: set[str] = None) -> list[Path]:
    """Gathers all files matching specific glob patterns while skipping ignored directories."""
    matched = []
    if not root_dir.exists():
        return matched

    exclude = (exclude_dirs or set()) | IGNORED_DIRS

    for root, dirs, files in os.walk(root_dir):
        dirs[:] = [d for d in dirs if d not in exclude]
        for f in files:
            file_path = Path(root) / f
            for pat in patterns:
                if file_path.match(pat):
                    matched.append(file_path)
                    break
    return matched


# --- Domain Definitions ---

def get_documentation_files() -> list[Path]:
    archive_dir = REPO_ROOT / "docs" / "archive"
    all_md = (
        list(REPO_ROOT.glob("*.md")) +
        gather_files_matching(REPO_ROOT / "docs", ["*.md"]) +
        gather_files_matching(REPO_ROOT / ".agents", ["*.md"])
    )
    return [f for f in all_md if archive_dir not in f.resolve().parents and f.parent.resolve() != archive_dir]


def get_safety_files() -> list[Path]:
    patterns = ["*.cs", "*.py", "*.json", "*.md", "*.csproj", "*.sln", "*.props", "*.targets"]
    files = gather_files_matching(REPO_ROOT, patterns)
    return [f for f in files if f.name != ".test_cache.json"]


def get_project_files(src_dir_name: str, test_dir_name: str) -> list[Path]:
    files = []
    src_dir = REPO_ROOT / "src" / src_dir_name
    test_dir = REPO_ROOT / "tests" / test_dir_name
    if src_dir.exists():
        files.extend(gather_files_matching(src_dir, ["*.cs", "*.csproj"]))
    if test_dir.exists():
        files.extend(gather_files_matching(test_dir, ["*.cs", "*.csproj"]))
    return files


DOMAINS = {
    "documentation": {
        "description": "Markdown Documentation Links & Structure",
        "gather_files": get_documentation_files,
        "runner": lambda: run_command(["python", str(REPO_ROOT / "scripts" / "verify_markdown_links.py")]),
    },
    "safety_hygiene": {
        "description": "Repository Safety, Secret Leaks & Path Hygiene",
        "gather_files": get_safety_files,
        "runner": lambda: run_command(["python", str(REPO_ROOT / "scripts" / "check_repo_safety.py")]),
    },
    "ADCE.Core": {
        "description": "Core Domain Models & Contracts (.NET 10)",
        "gather_files": lambda: get_project_files("ADCE.Core", "ADCE.Core.Tests"),
        "runner": lambda: run_command(["dotnet", "test", str(REPO_ROOT / "tests" / "ADCE.Core.Tests" / "ADCE.Core.Tests.csproj"), "--no-restore"]),
    },
    "ADCE.Extraction": {
        "description": "UI Automation Extraction & Archetype Resolvers (.NET 10)",
        "gather_files": lambda: get_project_files("ADCE.Extraction", "ADCE.Extraction.Tests") + get_project_files("ADCE.Core", "ADCE.Core.Tests"),
        "runner": lambda: run_command(["dotnet", "test", str(REPO_ROOT / "tests" / "ADCE.Extraction.Tests" / "ADCE.Extraction.Tests.csproj"), "--no-restore"]),
    },
    "ADCE.Storage": {
        "description": "Dual-Tier Memory & SQLite WAL Storage Engine (.NET 10)",
        "gather_files": lambda: get_project_files("ADCE.Storage", "ADCE.Storage.Tests") + get_project_files("ADCE.Core", "ADCE.Core.Tests"),
        "runner": lambda: run_command(["dotnet", "test", str(REPO_ROOT / "tests" / "ADCE.Storage.Tests" / "ADCE.Storage.Tests.csproj"), "--no-restore"]),
    },
    "ADCE.Mcp": {
        "description": "Model Context Protocol JSON-RPC Server (.NET 10)",
        "gather_files": lambda: get_project_files("ADCE.Mcp", "ADCE.Mcp.Tests") + get_project_files("ADCE.Core", "ADCE.Core.Tests") + get_project_files("ADCE.Storage", "ADCE.Storage.Tests"),
        "runner": lambda: run_command(["dotnet", "test", str(REPO_ROOT / "tests" / "ADCE.Mcp.Tests" / "ADCE.Mcp.Tests.csproj"), "--no-restore"]),
    },
    "ADCE.Daemon": {
        "description": "Windows Tray Host & HUD Overlay Engine (.NET 10)",
        "gather_files": lambda: (
            get_project_files("ADCE.Daemon", "ADCE.Daemon.Tests") +
            get_project_files("ADCE.Core", "ADCE.Core.Tests") +
            get_project_files("ADCE.Extraction", "ADCE.Extraction.Tests") +
            get_project_files("ADCE.Storage", "ADCE.Storage.Tests") +
            get_project_files("ADCE.Mcp", "ADCE.Mcp.Tests")
        ),
        "runner": lambda: run_command(["dotnet", "test", str(REPO_ROOT / "tests" / "ADCE.Daemon.Tests" / "ADCE.Daemon.Tests.csproj"), "--no-restore", "/p:BuildProjectReferences=false"]),
    },
}


def run_command(cmd: list[str]) -> tuple[bool, str, str]:
    """Executes a command and returns (success, summary, output)."""
    try:
        proc = subprocess.run(cmd, cwd=str(REPO_ROOT), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
        output = proc.stdout or ""
        success = proc.returncode == 0

        # Extract summary info
        summary = ""
        for line in output.splitlines():
            line_str = line.strip()
            if line_str.startswith("Passed!") or line_str.startswith("Failed!"):
                summary = line_str
                break
            elif line_str.startswith("[PASSED]") or line_str.startswith("[SUCCESS]") or line_str.startswith("[FAILED]") or line_str.startswith("[FAIL]"):
                summary = line_str
                break

        if not summary:
            summary = "Passed" if success else f"Failed (exit code {proc.returncode})"

        return success, summary, output
    except Exception as ex:
        return False, f"Execution Error: {ex}", str(ex)


def load_cache() -> dict:
    if CACHE_FILE.exists():
        try:
            with open(CACHE_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return {}
    return {}


def save_cache(cache: dict):
    try:
        with open(CACHE_FILE, "w", encoding="utf-8") as f:
            json.dump(cache, f, indent=2)
    except Exception as ex:
        print(f"[WARN] Failed to write cache: {ex}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Intelligent Change-Aware Test & Verification Runner for ADCE")
    parser.add_argument("--check", action="store_true", help="Dry-run audit of cached vs dirty domains without executing tests")
    parser.add_argument("--force", action="store_true", help="Bypass cache and force-run all verification checks and test suites")
    parser.add_argument("--domain", type=str, choices=list(DOMAINS.keys()), help="Run checks for a specific domain only")
    args = parser.parse_args()

    cache = load_cache()
    cached_domains = cache.get("domains", {})

    target_domains = [args.domain] if args.domain else list(DOMAINS.keys())

    print("================================================================================")
    print("  ADCE Change-Aware Verification & Test Engine")
    print("================================================================================")

    current_fingerprints = {}
    dirty_domains = []

    for name in target_domains:
        config = DOMAINS[name]
        files = config["gather_files"]()
        fp = compute_domain_fingerprint(files)
        current_fingerprints[name] = fp

        cached_entry = cached_domains.get(name, {})
        cached_fp = cached_entry.get("fingerprint")
        cached_status = cached_entry.get("status")

        is_dirty = args.force or (fp != cached_fp) or (cached_status != "PASSED")
        if is_dirty:
            dirty_domains.append(name)

    if args.check:
        print("\nDomain Verification Status (Audit Mode):")
        for name in target_domains:
            fp = current_fingerprints[name]
            cached_entry = cached_domains.get(name, {})
            cached_fp = cached_entry.get("fingerprint")
            cached_status = cached_entry.get("status", "UNVERIFIED")
            cached_time = cached_entry.get("timestamp_utc", "never")

            if fp == cached_fp and cached_status == "PASSED":
                print(f"  [CACHE HIT]  {name:<16} - {DOMAINS[name]['description']} ({cached_entry.get('summary', 'PASSED')})")
            else:
                print(f"  [DIRTY/STALE] {name:<16} - Changes detected since {cached_time}")

        print(f"\nAudit complete: {len(target_domains) - len(dirty_domains)} cached, {len(dirty_domains)} pending execution.")
        return 0 if len(dirty_domains) == 0 else 1

    if not dirty_domains:
        print("\n[ALL CACHE HITS] All verification domains and test suites are already verified!")
        for name in target_domains:
            entry = cached_domains.get(name, {})
            print(f"  ✓ {name:<16} : {entry.get('summary', 'PASSED')} (verified at {entry.get('timestamp_utc', 'recently')})")
        print("\nZero redundant tests or compilations executed.")
        return 0

    print(f"\nExecuting verification for {len(dirty_domains)} dirty/stale domain(s) ({len(target_domains) - len(dirty_domains)} cached):\n")

    overall_success = True
    new_cached_domains = dict(cached_domains)

    for name in target_domains:
        config = DOMAINS[name]
        fp = current_fingerprints[name]

        if name not in dirty_domains:
            entry = cached_domains.get(name, {})
            print(f"  [CACHE HIT]  {name:<16} : {entry.get('summary', 'PASSED')}")
            continue

        print(f"  [RUNNING]    {name:<16} : {config['description']}...")
        start_t = time.perf_counter()
        success, summary, output = config["runner"]()
        elapsed = time.perf_counter() - start_t

        now_utc = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

        if success:
            print(f"  [PASSED]     {name:<16} : {summary} ({elapsed:.2f}s)")
            new_cached_domains[name] = {
                "fingerprint": fp,
                "status": "PASSED",
                "timestamp_utc": now_utc,
                "duration_sec": round(elapsed, 2),
                "summary": summary,
            }
        else:
            print(f"  [FAILED]     {name:<16} : {summary} ({elapsed:.2f}s)")
            print("\n------------------------- Failure Output -------------------------")
            print(output.strip())
            print("------------------------------------------------------------------\n")
            new_cached_domains[name] = {
                "fingerprint": fp,
                "status": "FAILED",
                "timestamp_utc": now_utc,
                "duration_sec": round(elapsed, 2),
                "summary": summary,
            }
            overall_success = False

    cache["domains"] = new_cached_domains
    cache["version"] = 1
    cache["last_run_utc"] = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    save_cache(cache)

    print("\n================================================================================")
    if overall_success:
        print(f"  SUCCESS: All {len(target_domains)} domains verified and cached.")
        print("================================================================================")
        return 0
    else:
        print("  FAILURE: One or more verification checks or test suites failed.")
        print("================================================================================")
        return 1


if __name__ == "__main__":
    sys.exit(main())
