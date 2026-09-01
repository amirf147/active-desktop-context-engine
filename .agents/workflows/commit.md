---
description: Generates a copy-paste ready conventional commit message from staged changes after running automated safety and path checks.
---
<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

# Commit Workflow

Follow this deterministic 5-step sequence whenever generating commit messages:

## Step 1: Pre-Flight Safety & Path Validation
Run the automated repository validation checks:
1. Execute repository safety, secret detection, and path hygiene check:
   ```pwsh
   python scripts/check_repo_safety.py
   ```
2. Execute markdown link validation:
   ```pwsh
   python scripts/verify_markdown_links.py
   ```
3. Execute unit tests (.NET 10):
   ```pwsh
   dotnet test --configuration Release
   ```
4. **Gate**: If any check fails or reports absolute user path leaks or broken links, STOP immediately. Fix the violations and re-run until all checks pass with exit code 0.

## Step 2: Stage Verified Changes
Stage only verified repository files:
```pwsh
git add <modified_files>
```

## Step 3: Inspect Staged Diff
Inspect the staged changes to verify completeness:
```pwsh
git status
git diff --cached --stat
```

## Step 4: Format Conventional Commit Message
Construct a conventional commit message following this format:
- **Title Line**: `type(scope): imperative title`
  - Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `ci`, `perf`.
  - Multi-file changes: Use the highest-impact type (e.g., `feat` overrides `docs`).
  - Formatting: All lowercase imperative mood without trailing period.
- **Body Paragraph**: 1 to 2 sentences explaining why the change was needed and the architectural context.
- **Bulleted Changes**: An itemized list of concrete changes.
- **Exclusions**: No diff metadata, line numbers, or section labels (e.g., "Summary:").

## Step 5: Output Copy-Paste Ready Message
- **NEVER execute `git commit` or `git push` autonomously.**
- Output the final formatted message in a single markdown code block so it can be pasted directly into the IDE Source Control commit box.
