---
description: Generates a copy-paste ready commit message for Caster User Directory repository updates.
---

# Instructions

1. **Analyze**: Review `git diff --cached` to understand the staged changes.
2. **Title Formatting (Conventional Commits)**:
   - Format: `type(scope): imperative title`
   - Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`.
   - *Multi-file changes*: Use the highest-impact type (e.g., `feat` overrides `docs`). If unrelated, omit scope (e.g., `chore: update various files`).
3. **Body Formatting**:
   - 1-2 sentence paragraph explaining *why* the change was needed.
   - Bulleted list of specific changes.
4. **Exclusions**:
   - No diff metadata, line numbers, or section headers (e.g., "Summary:"). Just empty lines between title, body, and list.

# Execution
- Output the final message as raw plain text in a single code block (Subject line, blank line, then bulleted body).
- Do NOT wrap in `git commit -m "..."` CLI syntax or quote arguments. The output must be directly pasteable into the IDE Source Control commit message box.
- Do NOT run `git commit` autonomously.
