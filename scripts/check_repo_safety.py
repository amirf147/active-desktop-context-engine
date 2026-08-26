#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi
"""
Repository Safety, Secret & Path Hygiene Checker for ADCE
"""

import getpass
import os
import re
import sys

# Directory to scan (root of the repo)
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Ignore directories
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
}

# Ignore file extensions / binary files
IGNORED_EXTENSIONS = {
    ".dll",
    ".exe",
    ".pdb",
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".webp",
    ".mp4",
    ".ico",
    ".svg",
    ".pyc",
    ".pyo",
}

# Generic regex patterns for path hygiene (handles unescaped, JSON-escaped, and raw backslashes/slashes)
WINDOWS_ABS_PATH_RE = re.compile(
    r"[A-Za-z]:(?:\\{1,4}|/)+(?:Users|Documents|Program Files|AppData|Windows|Temp|repos|Projects|Desktop)(?:\\{1,4}|/)+",
    re.IGNORECASE,
)
USER_HOME_PATH_RE = re.compile(
    r"(?:[A-Za-z]:(?:\\{1,4}|/)+|/|\\{1,4})(?:Users|home|Documents and Settings)(?:\\{1,4}|/)+[A-Za-z0-9_.-]+(?:\\{1,4}|/)+",
    re.IGNORECASE,
)
UNIX_ABS_PATH_RE = re.compile(r"^/(Users|home|root|opt|var|etc)[/]", re.IGNORECASE)
ENV_VAR_PATH_RE = re.compile(r"%(?:LOCALAPPDATA|APPDATA|USERPROFILE|TEMP|TMP)%", re.IGNORECASE)
FILE_URI_RE = re.compile(r"file:///", re.IGNORECASE)

# Dynamic runtime detection for active local environment username (without hardcoding any personal name in source)
try:
    CURRENT_USER = getpass.getuser()
    if CURRENT_USER and len(CURRENT_USER) > 1 and CURRENT_USER.lower() not in {"root", "runner", "github", "administrator", "system"}:
        ACTIVE_USER_PATH_RE = re.compile(
            rf"(?:\\{{1,4}}|/)+{re.escape(CURRENT_USER)}(?:\\{{1,4}}|/)+",
            re.IGNORECASE,
        )
    else:
        ACTIVE_USER_PATH_RE = None
except Exception:
    ACTIVE_USER_PATH_RE = None

# Secret & credential token patterns
SECRET_PATTERNS = [
    (re.compile(r"-----BEGIN (?:RSA|OPENSSH|EC|DSA|PGP)?\s?PRIVATE KEY-----"), "Private Key Header"),
    (re.compile(r"\b(?:sk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}\b"), "API Key Token"),
    (re.compile(r"\bghp_[0-9a-zA-Z]{36}\b"), "GitHub Personal Access Token"),
    (re.compile(r"\beyJ[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\b"), "JWT Token"),
    (re.compile(r"\b[A-Za-z0-9._%+-]+@(?!(?:example\.com|users\.noreply\.github\.com|domain\.com)\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", re.IGNORECASE), "Plain Email Address in Content"),
]

# Required headers for source files
SPDX_CS = "// SPDX-License-Identifier: Apache-2.0"
SPDX_PY = "# SPDX-License-Identifier: Apache-2.0"


def check_file(file_path: str, rel_path: str) -> list[str]:
    violations = []
    if rel_path == "scripts/check_repo_safety.py" or rel_path.startswith("docs/reports/claim_verification_"):
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
            if USER_HOME_PATH_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: User profile directory path: {line.strip()[:100]}")
            if ACTIVE_USER_PATH_RE and ACTIVE_USER_PATH_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: Active OS user path component: {line.strip()[:100]}")
            if ENV_VAR_PATH_RE.search(line):
                violations.append(f"{rel_path}:{line_no}: Local machine environment variable: {line.strip()[:100]}")
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
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

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
