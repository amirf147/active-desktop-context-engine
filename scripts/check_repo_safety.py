#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2024-2026 Amir Farhadi
"""
Repository Safety, Secret & Absolute Path Hygiene Checker for ADCE
"""

import os
import re
import sys

# Directory to scan (root of the repo)
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Ignore directories
IGNORED_DIRS = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    ".ruff_cache",
    ".idea",
}

# Ignore file extensions / files
IGNORED_EXTENSIONS = {
    ".dll",
    ".exe",
    ".pdb",
    ".png",
    ".jpg",
    ".jpeg",
    ".ico",
    ".svg",
}

# Regex patterns for safety violations
WINDOWS_ABS_PATH_RE = re.compile(
    r"[A-Za-z]:[\\/](Users|Documents|Program Files|AppData|Windows|Temp|repos)[\\/]",
    re.IGNORECASE,
)
UNIX_ABS_PATH_RE = re.compile(r"^/(Users|home|root|opt|var|etc)[/]", re.IGNORECASE)
LOCALAPPDATA_RE = re.compile(r"%LOCALAPPDATA%", re.IGNORECASE)
FILE_URI_RE = re.compile(r"file:///", re.IGNORECASE)

# Secret token patterns
SECRET_PATTERNS = [
    (re.compile(r"-----BEGIN (?:RSA|OPENSSH|EC|DSA|PGP)?\s?PRIVATE KEY-----"), "Private Key Header"),
    (re.compile(r"\b(?:sk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}\b"), "API Key Token"),
    (re.compile(r"\bghp_[0-9a-zA-Z]{36}\b"), "GitHub Personal Access Token"),
    (re.compile(r"\beyJ[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\b"), "JWT Token"),
    (re.compile(r"\bamirf147@gmail\.com\b", re.IGNORECASE), "Personal Email Address in Content"),
]

# Required headers for source files
SPDX_CS = "// SPDX-License-Identifier: Apache-2.0"
SPDX_PY = "# SPDX-License-Identifier: Apache-2.0"


def check_file(file_path: str, rel_path: str) -> list[str]:
    violations = []
    if rel_path == "scripts/check_repo_safety.py":
        return violations

    try:
        with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
            lines = content.splitlines()

        # Check SPDX headers for source code
        if file_path.endswith(".cs") and SPDX_CS not in content:
            violations.append(f"{rel_path}: Missing SPDX Apache-2.0 header ('{SPDX_CS}')")
        elif file_path.endswith(".py") and SPDX_PY not in content:
            violations.append(f"{rel_path}: Missing SPDX Apache-2.0 header ('{SPDX_PY}')")

        for line_no, line in enumerate(lines, start=1):
            # Check for hardcoded machine paths
            if WINDOWS_ABS_PATH_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: Hardcoded Windows absolute path: {line.strip()[:100]}")
            if LOCALAPPDATA_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: Local machine path variable (%LOCALAPPDATA%): {line.strip()[:100]}")
            if FILE_URI_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: Local file URI (file:///): {line.strip()[:100]}")

            # Check secrets
            for pattern, desc in SECRET_PATTERNS:
                if pattern.search(line):
                    violations.append(f"{rel_path}:{line_no}: Potential secret/credential ({desc}): {line.strip()[:60]}...")

    except Exception as ex:
        violations.append(f"{rel_path}: Failed to read file ({ex})")

    return violations


def main() -> int:
    print(f"Scanning repository for safety, secrets, and path hygiene: {REPO_ROOT}")
    all_violations = []

    for root, dirs, files in os.walk(REPO_ROOT):
        dirs[:] = [d for d in dirs if d not in IGNORED_DIRS]

        for file in files:
            ext = os.path.splitext(file)[1].lower()
            if ext in IGNORED_EXTENSIONS:
                continue

            file_path = os.path.join(root, file)
            rel_path = os.path.relpath(file_path, REPO_ROOT).replace("\\", "/")

            violations = check_file(file_path, rel_path)
            all_violations.extend(violations)

    if all_violations:
        print(f"\n[FAILED] Found {len(all_violations)} hygiene violation(s):\n")
        for v in all_violations:
            print(f"  - {v}")
        print("\nPlease resolve the above issues before committing.")
        return 1

    print("\n[PASSED] Zero hardcoded absolute paths, secrets, or header issues detected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
