# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi

import os
import re
import sys
from pathlib import Path
import urllib.parse

repo_root = Path(__file__).resolve().parent.parent

DOC_MOVES = {
    "ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md": "docs/architecture/ARCHITECTURE_AND_MODULAR_IMPLEMENTATION_PLAN.md",
    "REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md": "docs/architecture/REQUIREMENTS_AND_DYNAMIC_DISCOVERY_SPEC.md",
    "MCP_SCHEMA_SPEC.md": "docs/architecture/MCP_SCHEMA_SPEC.md",
    "UI_AUTOMATION_STRUCTURES_REFERENCE.md": "docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md",
    "HOSTILE_ARCHITECTURE_REVIEW.md": "docs/architecture/HOSTILE_ARCHITECTURE_REVIEW.md",

    "ADCE_CORE_DEEP_DIVE.md": "docs/deep_dives/ADCE_CORE_DEEP_DIVE.md",
    "ADCE_EXTRACTION_DEEP_DIVE.md": "docs/deep_dives/ADCE_EXTRACTION_DEEP_DIVE.md",
    "ADCE_EVENT_PIPELINE_DEEP_DIVE.md": "docs/deep_dives/ADCE_EVENT_PIPELINE_DEEP_DIVE.md",
    "ADCE_STORAGE_DEEP_DIVE.md": "docs/deep_dives/ADCE_STORAGE_DEEP_DIVE.md",
    "ADCE_MCP_DEEP_DIVE.md": "docs/deep_dives/ADCE_MCP_DEEP_DIVE.md",
    "ADCE_DAEMON_DEEP_DIVE.md": "docs/deep_dives/ADCE_DAEMON_DEEP_DIVE.md",

    "EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md": "docs/guides/EDUCATIONAL_GUIDE_AND_ARCHITECTURE_REFRESHER.md",
    "ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md": "docs/guides/ADCE_FOCUS_AND_ZONE_DETECTION_EXPLAINED.md",
    "EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md": "docs/guides/EDUCATIONAL_GUIDE_TEST_HARNESS_AND_CLAIM_VERIFICATION.md",

    "EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md": "docs/testing/EMPIRICAL_TEST_HARNESS_AND_CLAIM_VERIFICATION_SPEC.md",
    "REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md": "docs/testing/REVIEWER_OBSERVATIONS_AND_HARDENING_ROADMAP.md",

    "LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md": "docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_2.md",
    "LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md": "docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4.md",
    "LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md": "docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_4_5.md",
    "LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md": "docs/postmortems/LESSONS_LEARNED_AND_SPIKE_POSTMORTEM_MILESTONE_6.md",
}

SUBDIRS = ["architecture", "deep_dives", "guides", "testing", "postmortems"]

def fix_file_links(file_path: Path):
    content = file_path.read_text(encoding='utf-8')
    orig_content = content
    rel_file = file_path.relative_to(repo_root).as_posix()

    in_docs_subdir = False
    current_sd = ""
    for sd in SUBDIRS:
        if rel_file.startswith(f"docs/{sd}/"):
            in_docs_subdir = True
            current_sd = sd
            break

    link_regex = re.compile(r'(!?\[[^\]]*\]\()([^)]+)(\))')

    def replace_link(match):
        prefix = match.group(1)
        target = match.group(2).strip()
        suffix = match.group(3)

        if target.startswith("http://") or target.startswith("https://") or target.startswith("mailto:") or target.startswith("#"):
            return match.group(0)

        anchor = ""
        target_no_anchor = target
        if "#" in target:
            parts = target.split("#", 1)
            target_no_anchor = parts[0]
            anchor = "#" + parts[1]

        target_clean = urllib.parse.unquote(target_no_anchor)

        # 1. If inside docs/<subdir>/
        if in_docs_subdir:
            # Breadcrumbs & home links
            if target_clean == "../README.md":
                return f"{prefix}../../README.md{anchor}{suffix}"
            if target_clean == "CONTEXT.md" or target_clean == "./CONTEXT.md":
                return f"{prefix}../CONTEXT.md{anchor}{suffix}"
            if target_clean == "docs/CONTEXT.md":
                return f"{prefix}../CONTEXT.md{anchor}{suffix}"

            # Links to other top-level folders from docs root
            for folder in ["benchmarks", "diagrams", "external_research", "media", "reports"]:
                if target_clean.startswith(f"{folder}/") or target_clean.startswith(f"./{folder}/"):
                    clean = target_clean.lstrip("./")
                    return f"{prefix}../{clean}{anchor}{suffix}"
                if target_clean == folder or target_clean == f"{folder}/":
                    return f"{prefix}../{folder}/{anchor}{suffix}"

            # Links to src/
            if target_clean.startswith("../src/"):
                return f"{prefix}../../src/{target_clean[7:]}{anchor}{suffix}"

            # Links to tests/
            if target_clean.startswith("../tests/"):
                return f"{prefix}../../tests/{target_clean[9:]}{anchor}{suffix}"

            # Links to moved docs
            target_basename = Path(target_clean).name
            if target_basename in DOC_MOVES:
                dest_rel = DOC_MOVES[target_basename] # e.g. docs/architecture/FILE.md
                dest_sd = dest_rel.split("/")[1]
                if dest_sd == current_sd:
                    return f"{prefix}{target_basename}{anchor}{suffix}"
                else:
                    return f"{prefix}../{dest_sd}/{target_basename}{anchor}{suffix}"

        # 2. If in docs/reports/
        elif rel_file.startswith("docs/reports/"):
            target_basename = Path(target_clean).name
            if target_basename in DOC_MOVES:
                dest_rel = DOC_MOVES[target_basename]
                dest_sd = dest_rel.split("/")[1]
                return f"{prefix}../{dest_sd}/{target_basename}{anchor}{suffix}"

        # 3. If in docs/external_research/
        elif rel_file.startswith("docs/external_research/"):
            target_basename = Path(target_clean).name
            if target_basename in DOC_MOVES:
                dest_rel = DOC_MOVES[target_basename]
                dest_sd = dest_rel.split("/")[1]
                return f"{prefix}../{dest_sd}/{target_basename}{anchor}{suffix}"

        # 4. If in .agents/
        elif rel_file.startswith(".agents/"):
            target_basename = Path(target_clean).name
            if target_basename in DOC_MOVES:
                dest_rel = DOC_MOVES[target_basename]
                return f"{prefix}../{dest_rel}{anchor}{suffix}"

        # 5. If in root or docs/CONTEXT.md
        elif rel_file == "README.md" or rel_file == "docs/CONTEXT.md":
            target_basename = Path(target_clean).name
            if target_basename in DOC_MOVES:
                dest_rel = DOC_MOVES[target_basename]
                if rel_file == "docs/CONTEXT.md":
                    return f"{prefix}{dest_rel[5:]}{anchor}{suffix}"
                else:
                    return f"{prefix}{dest_rel}{anchor}{suffix}"

        return match.group(0)

    new_content = link_regex.sub(replace_link, content)
    if new_content != orig_content:
        file_path.write_text(new_content, encoding='utf-8')
        print(f"Updated links in {rel_file}")

def main():
    md_files = list(repo_root.glob("*.md")) + list((repo_root / "docs").rglob("*.md")) + list((repo_root / ".agents").rglob("*.md"))
    for f in md_files:
        fix_file_links(f)

if __name__ == "__main__":
    main()
