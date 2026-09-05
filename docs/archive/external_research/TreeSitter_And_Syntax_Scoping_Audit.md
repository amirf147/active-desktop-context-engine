<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🔬 External Research ](README.md) › **Tree-sitter & Syntax Scoping Audit**

---

# External Research: Tree-sitter, Incremental Parsing & Syntactic Voice Scoping

> **Document Status:** Historical Research Archive / Syntactic Scoping Audit
> **Epistemic Authority:** Tier 6 (External Research & Upstream Lineage — Non-Normative Background Context)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Scope:** Technical audit of [Tree-sitter](https://github.com/tree-sitter/tree-sitter) (C / Rust incremental parsing system) and its architectural applicability to the Active Desktop Context Engine (ADCE) and Caster / Dragonfly voice grammar systems.
> **Key Premise:** Moving beyond top-level window envelopes and sub-window UI zones (`DesktopSemanticZone`) into **syntactic and semantic document micro-contexts** (AST nodes, function boundaries, docstrings, and expression scopes) to enable dynamic, context-aware voice grammars.

---

## 1. Executive Summary

Traditional desktop accessibility engines and voice recognition frameworks evaluate context at two macroscopic tiers:
1. **Window-Level Scope (Win32):** Process executable (`Code.exe`), window title, and top-level `HWND`.
2. **Sub-Window Zone-Level Scope (ADCE / UIA):** Semantic containers like `IntegratedTerminal`, `EditorCodeBuffer`, `SidebarExplorer`, or `ChatAssistant`.

While ADCE's zone-level discovery successfully differentiates the VS Code terminal from the Monaco code buffer, modern software development workflows demand **intra-document syntactic awareness**. When a user speaks a voice command inside an editor, the desired grammar often depends strictly on the **syntactic construct enclosing the text caret**:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        HIERARCHICAL CONTEXT RESOLUTION TIERS                           │
├────────────────────────────────┬───────────────────────────────────────────────────────┤
│ Context Tier                   │ Governing Engine & Granularity                        │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ Tier 1: Application Window     │ Win32 API (`GetForegroundWindow`, `GetWindowText`)   │
│                                │ e.g. "User is in Visual Studio Code (Code.exe)"       │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ Tier 2: Sub-Window Zone        │ ADCE UIA3 Extractor (`DesktopSemanticZone`)           │
│                                │ e.g. "User is in the Monaco Code Editor Buffer"       │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ Tier 3: Syntactic Micro-Scope  │ Tree-sitter Incremental AST (`ts_tree_root_node`)     │
│                                │ e.g. "User is inside a Python Docstring / Comment"    │
│                                │ ──► Activate Dictation & Natural Language Grammars    │
│                                │ e.g. "User is inside a Function Parameter List"       │
│                                │ ──► Activate Type Hint & Parameter Voice Macros       │
│                                │ e.g. "User is at Module Top-Level"                   │
│                                │ ──► Activate Class & Function Declaration Grammars    │
└────────────────────────────────┴───────────────────────────────────────────────────────┘
```

[Tree-sitter](https://github.com/tree-sitter/tree-sitter) provides an incremental, error-tolerant Generalized LR (GLR) parser that produces concrete syntax trees (CSTs) in sub-millisecond times, making it an ideal companion to ADCE for fine-grained editor voice grammar activation.

---

## 2. Tree-sitter Architecture & Mechanics

### 2.1 Core Capabilities & Performance Characteristics

Tree-sitter was engineered specifically for interactive text editors (originally Atom, now standard in Neovim, Zed, Helix, and GitHub):

* **Incremental Re-parsing:** When the user types or dictates changes into a document, Tree-sitter does not re-parse the entire buffer. It mutates the existing syntax tree in $O(\log N)$ time, typically completing within **0.05 ms to 0.5 ms** even for multi-thousand-line files.
* **Robust Error Recovery:** Unlike conventional compiler frontends that abort on syntax errors, Tree-sitter generates valid, queryable trees even while code is actively being typed or partially broken (`ERROR` nodes are localized to the smallest possible subtree).
* **Deterministic C API:** The core library is written in pure C99 with zero external dependencies, offering zero-allocation query execution and bindings across C#, Rust, Python, and WebAssembly.
* **Concrete Syntax Trees (CST):** Every token, whitespace, delimiter, and identifier is retained with precise byte offsets (`start_byte`, `end_byte`) and point coordinates (`row`, `column`).

```
                              Document Source Buffer
                                       │
                                       ▼
                       ┌───────────────────────────────┐
                       │  Tree-sitter Parser (C Core)  │
                       └───────────────┬───────────────┘
                                       │
                                       ▼
                        Incremental Concrete Syntax Tree
                                       │
                        ┌──────────────┴──────────────┐
                        ▼                             ▼
              [function_definition]             [class_definition]
                ├── name: identifier              ├── name: identifier
                ├── parameters: (parameter_list)  └── body: (block)
                └── body: (block)
                      ├── (expression_statement)
                      └── (comment) ◄── Caret Position (Line 42, Col 8)
```

### 2.2 Tree-sitter S-Expression Query Language

Tree-sitter provides a Lisp-like pattern matching DSL (Tree Queries) that allows extracting semantic patterns across AST nodes:

```scheme
; Match function definitions and capture their names and parameters
(function_definition
  name: (identifier) @func.name
  parameters: (parameters) @func.params
  body: (block) @func.body)

; Match docstrings and comments for dictation routing
(comment) @context.comment
(string (string_content) @context.docstring)

; Match import blocks
(import_from_statement) @context.imports
```

---

## 3. Integrating Tree-sitter with ADCE & Voice Interfaces

### 3.1 The Caret-to-AST Resolution Pipeline

To enable syntactic voice scoping in Caster / Dragonfly, ADCE coordinates with the editor buffer to map the physical screen caret to a Tree-sitter AST node:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        INTRA-EDITOR VOICE SCOPING WORKFLOW                             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. ADCE UIA Extractor:                                                                 │
│    • Detects active zone: `DesktopSemanticZone.EditorCodeBuffer`                       │
│    • Reads active file path / language ID via Monaco breadcrumbs                       │
│    • (Optional Milestone 8) Reads caret line & column via UIA `TextPattern`            │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 2. Tree-sitter Context Bridge:                                                         │
│    • Retrieves active AST for the current document buffer                             │
│    • Queries AST at point: `ts_node_descendant_for_point_range(root, point, point)`    │
│    • Identifies node type hierarchy: `['module', 'class_def', 'function_def', 'comment']│
├────────────────────────────────────────────────────────────────────────────────────────┤
│ 3. Caster / Dragonfly Grammar Evaluation:                                              │
│    • Rule: `PythonDocstringRule` ──► `FuncContext(is_in_docstring_or_comment)`         │
│    • Rule: `PythonSignatureRule` ──► `FuncContext(is_in_parameter_list)`               │
│    • Rule: `PythonStatementRule` ──► `FuncContext(is_in_function_body)`                │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Concrete Caster Voice Use Cases

| Syntactic Context | AST Node Type | Active Voice Grammar in Caster | User Experience / Voice Command Example |
| :--- | :--- | :--- | :--- |
| **Docstring / Comment** | `comment`, `string_literal` | **Natural Language & Dictation** | Speaking `"explain edge case handling"` types fluent natural English without code capitalization or programming acronym expansion. |
| **Function Parameters** | `parameters`, `parameter_list` | **Type Hint & Argument Macros** | Speaking `"optional string name"` expands to `name: Optional[str] = None`. |
| **Class Definition** | `class_definition` | **OOP & Inheritance Macros** | Speaking `"inherit base model"` generates `(BaseModel):` with docstring template. |
| **Import Block** | `import_statement`, `import_from` | **Module Import Grammar** | Speaking `"import typing list dict"` expands to `from typing import List, Dict`. |
| **Conditionals** | `if_statement`, `match_statement` | **Branching & Comparison Macros** | Speaking `"is none return false"` expands to `if value is None: return False`. |

---

## 4. Architectural Feasibility & Tradeoff Matrix

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          TREE-SITTER ADCE INTEGRATION MATRIX                           │
├──────────────────────────────┬──────────────────────────────┬──────────────────────────┤
│ Dimension                    │ Tree-sitter Direct in ADCE   │ Editor Extension Plugin  │
├──────────────────────────────┼──────────────────────────────┼──────────────────────────┤
│ **Execution Locus**          │ Out-of-process (ADCE Daemon) │ In-process (VS Code Ext) │
│ **Buffer Synchronization**   │ Disk file read + LSP / UIA   │ Native Editor Document API│
│ **Re-parse Latency**         │ `< 1 ms` (C / NativeAOT)     │ `< 0.2 ms` (Node/V8)     │
│ **Caret Resolution**         │ Via UIA `TextPattern` offset │ Direct editor cursor API │
│ **Cross-Editor Scope**       │ Any editor (VS Code, Zed)    │ Single editor ecosystem  │
│ **Memory Overhead**          │ Minimal (~2 MB per language) │ Part of editor memory    │
└──────────────────────────────┴──────────────────────────────┴──────────────────────────┘
```

### Strategic Evaluation:
* **Recommended Phase:** Milestone 8 (Advanced Context Primitives) & Milestone 9 (Voice Bindings).
* **Architecture:** Tree-sitter bindings in C# (.NET 10 via `P/Invoke` to `tree-sitter.dll` or the official C# bindings) can ingest document buffers exposed via ADCE's planned `get_document_text` endpoint, enriching the live MCP snapshot with an `ast_node_path` (e.g. `["class:ContextManager", "func:__enter__", "comment"]`).
* **Verdict:** 🟢 **High Value.** Tree-sitter is the gold standard for AST-aware code navigation and provides the exact semantic bridge needed for context-dependent voice programming.
