# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

import os
import re
import sys
from pathlib import Path
import urllib.parse

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def check_links():
    repo_root = Path(__file__).resolve().parent.parent
    md_files = list(repo_root.glob("*.md")) + list((repo_root / "docs").rglob("*.md")) + list((repo_root / ".agents").rglob("*.md"))

    link_pattern = re.compile(r'!?\[([^\]]*)\]\(([^)]+)\)')

    broken_links = []
    total_links = 0

    for md_file in md_files:
        try:
            content = md_file.read_text(encoding='utf-8')
        except Exception as e:
            print(f"Error reading {md_file}: {e}")
            continue

        for match in link_pattern.finditer(content):
            text, target = match.groups()
            target = target.strip()

            if target.startswith("http://") or target.startswith("https://") or target.startswith("mailto:") or target.startswith("#"):
                continue

            target_path_str = target.split("#")[0].split("?")[0]
            if not target_path_str:
                continue

            target_path_str = urllib.parse.unquote(target_path_str)

            total_links += 1

            resolved_path = (md_file.parent / target_path_str).resolve()

            if not resolved_path.exists():
                broken_links.append({
                    "source_file": md_file.relative_to(repo_root),
                    "link_text": text,
                    "target": target,
                    "resolved": resolved_path
                })

    print(f"Scanned {len(md_files)} markdown files, checked {total_links} local links.")
    if broken_links:
        print(f"\n[FAIL] Found {len(broken_links)} broken local links:")
        for b in broken_links:
            print(f"  In {b['source_file']}: '{b['link_text']}' -> '{b['target']}'")
        return False
    else:
        print("\n[SUCCESS] All local markdown links resolve successfully!")
        return True

if __name__ == "__main__":
    success = check_links()
    sys.exit(0 if success else 1)
