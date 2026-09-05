<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **MCP Schema Specification**

---

# ADCE Model Context Protocol (MCP) Schema Specification

> **Status:** Active / Normative Protocol Specification
> **Protocol Standard:** [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) JSON-RPC 2.0
> **Parent Context:** [`docs/CONTEXT.md`](../CONTEXT.md)
> **Implementation Target:** `src/ADCE.Mcp/` (.NET 10 / C# 14)

---

## 1. Design Rationale & Core Envelope Philosophy

The ADCE MCP schema is engineered to provide AI agents, LLM tool callers, and voice recognition grammars with high-density, token-efficient semantic desktop context.

Rather than streaming raw visual screenshots or thousands of unpruned UI Automation nodes, the schema exposes a structured aggregate snapshot partitioned into decoupled context envelopes:
1. **Workspace Envelope:** Virtual desktop identity and multi-monitor spatial coordinates.
2. **Window Envelope:** Active foreground process identity, HWND, window title, and framework archetype.
3. **Focus & Selection Context:** Exact control type, element name, 19-zone semantic typing anchor, macro pane location, and explicit container hierarchy paths.
4. **Specialized Application Context:** High-level tabs, active file paths, or shell titles for IDEs, web browsers, File Explorer, and terminals.

---

## 2. Unified Context Schema (JSON Schema Draft-07)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DesktopContextSnapshot",
  "type": "object",
  "properties": {
    "timestamp": {
      "type": "string",
      "format": "date-time",
      "description": "ISO-8601 UTC timestamp of context capture"
    },
    "workspace": {
      "type": "object",
      "properties": {
        "virtual_desktop_id": { "type": "string", "format": "guid" },
        "desktop_index": { "type": "integer" },
        "virtual_desktop_name": { "type": "string" },
        "monitor_index": { "type": "integer" },
        "monitor_bounds": {
          "type": "object",
          "properties": {
            "left": { "type": "integer" },
            "top": { "type": "integer" },
            "width": { "type": "integer" },
            "height": { "type": "integer" }
          },
          "required": ["left", "top", "width", "height"]
        }
      },
      "required": ["virtual_desktop_id", "desktop_index"]
    },
    "window": {
      "type": "object",
      "properties": {
        "hwnd": { "type": "string", "pattern": "^0x[0-9A-Fa-f]+$" },
        "title": { "type": "string" },
        "process_name": { "type": "string" },
        "pid": { "type": "integer" },
        "class_name": { "type": "string" },
        "archetype": {
          "type": "string",
          "enum": ["unknown", "chromium_electron", "gecko", "win_ui3_xaml", "classic_win32", "canvas_toolkit"]
        },
        "bounds": {
          "type": "object",
          "properties": {
            "left": { "type": "integer" },
            "top": { "type": "integer" },
            "width": { "type": "integer" },
            "height": { "type": "integer" }
          },
          "required": ["left", "top", "width", "height"]
        },
        "is_minimized": { "type": "boolean" },
        "is_maximized": { "type": "boolean" }
      },
      "required": ["hwnd", "title", "process_name", "pid", "class_name"]
    },
    "focus": {
      "type": "object",
      "properties": {
        "control_type": { "type": "string" },
        "element_name": { "type": "string" },
        "bounding_box": {
          "type": "object",
          "properties": {
            "left": { "type": "integer" },
            "top": { "type": "integer" },
            "width": { "type": "integer" },
            "height": { "type": "integer" }
          },
          "required": ["left", "top", "width", "height"]
        },
        "automation_id": { "type": "string" },
        "class_name": { "type": "string" },
        "semantic_zone": {
          "type": "string",
          "enum": [
            "unknown", "editor_buffer", "terminal", "git_commit_box", "sidebar_explorer",
            "address_bar", "web_document", "shell_item_list", "tab_bar", "status_bar",
            "command_palette", "chat_prompt", "quick_open", "system_dialog", "navigation_panel",
            "activity_bar", "timeline", "outline", "chat_conversation"
          ]
        },
        "pane_location": {
          "type": "string",
          "enum": [
            "unknown", "activity_bar", "primary_sidebar", "main_content",
            "auxiliary_sidebar", "bottom_panel", "top_bar", "status_bar", "overlay_modal"
          ]
        },
        "active_view": { "type": ["string", "null"] },
        "section_name": { "type": ["string", "null"] },
        "semantic_path": {
          "type": "array",
          "items": { "type": "string" }
        },
        "container_path": {
          "type": "array",
          "items": { "type": "string" }
        },
        "container_classes": {
          "type": "array",
          "items": { "type": "string" }
        },
        "is_overlay": { "type": "boolean" },
        "value_snippet": { "type": ["string", "null"] }
      },
      "required": ["control_type", "element_name", "bounding_box", "semantic_zone", "pane_location"]
    },
    "ide_context": {
      "type": "object",
      "properties": {
        "active_file_path": { "type": "string" },
        "active_sidebar_view": { "type": "string" },
        "open_editor_tabs": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "title": { "type": "string" },
              "is_active": { "type": "boolean" }
            },
            "required": ["title", "is_active"]
          }
        },
        "edit_buffer_snippet": { "type": "string" }
      }
    },
    "browser_context": {
      "type": "object",
      "properties": {
        "container_type": { "type": "string" },
        "total_tab_count": { "type": "integer" },
        "active_tab": { "type": "string" },
        "open_tabs": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "index": { "type": "integer" },
              "title": { "type": "string" },
              "is_active": { "type": "boolean" }
            },
            "required": ["index", "title", "is_active"]
          }
        }
      }
    },
    "explorer_context": {
      "type": "object",
      "properties": {
        "current_path": { "type": "string" },
        "selected_items": {
          "type": "array",
          "items": { "type": "string" }
        },
        "open_folder_tabs": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "terminal_context": {
      "type": "object",
      "properties": {
        "shell_title": { "type": "string" },
        "buffer_snippet": { "type": "string" },
        "active_tab": { "type": "string" }
      }
    },
    "extraction_duration_ms": {
      "type": "number",
      "description": "Pipeline extraction latency in milliseconds"
    }
  },
  "required": ["timestamp", "workspace", "window", "focus"]
}
```

---

## 3. Representative Snapshot Example

```json
{
  "timestamp": "2026-09-05T18:20:00.000Z",
  "workspace": {
    "virtual_desktop_id": "3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
    "desktop_index": 1,
    "virtual_desktop_name": "Development",
    "monitor_index": 0,
    "monitor_bounds": { "left": 0, "top": 0, "width": 1920, "height": 1080 }
  },
  "window": {
    "hwnd": "0x00DB083E",
    "title": "active-desktop-context-engine - Antigravity IDE",
    "process_name": "Antigravity.exe",
    "pid": 26420,
    "class_name": "Chrome_WidgetWin_1",
    "archetype": "chromium_electron",
    "bounds": { "left": 0, "top": 0, "width": 1920, "height": 1080 },
    "is_minimized": false,
    "is_maximized": true
  },
  "focus": {
    "control_type": "Edit",
    "element_name": "CORE_DOMAIN_MODEL.md",
    "bounding_box": { "left": 400, "top": 120, "width": 1200, "height": 800 },
    "automation_id": "workbench.parts.editor",
    "class_name": "native-edit-context",
    "semantic_zone": "editor_buffer",
    "pane_location": "main_content",
    "active_view": "workbench.parts.editor",
    "section_name": null,
    "semantic_path": ["main_content", "editor"],
    "container_path": ["workbench.parts.editor", "editor-container"],
    "container_classes": ["monaco-editor", "overflow-guard"],
    "is_overlay": false,
    "value_snippet": null
  },
  "ide_context": {
    "active_file_path": "docs/architecture/CORE_DOMAIN_MODEL.md",
    "active_sidebar_view": "Explorer",
    "open_editor_tabs": [
      { "title": "CORE_DOMAIN_MODEL.md", "is_active": true },
      { "title": "EXTRACTION_PIPELINE.md", "is_active": false },
      { "title": "README.md", "is_active": false }
    ],
    "edit_buffer_snippet": "# ADCE Core Domain Model Specification"
  },
  "extraction_duration_ms": 3.82
}
```

---

## 4. MCP Tools & Resources Endpoint Specification

ADCE exposes the following MCP JSON-RPC 2.0 endpoints:

| Endpoint Type | URI / Tool Name | Description | SLA |
| :--- | :--- | :--- | :--- |
| **Resource** | `desktop://current` | Live snapshot of current foreground desktop state from L1 atomic cache. | `< 1.0 µs` |
| **Resource** | `desktop://history` | Time-series query of recent focus transitions (supports `?minutes=15` and `?limit=50`). | `< 5.0 ms` |
| **Tool** | `get_desktop_context` | Explicit context pull with optional process filter and projection (`full`, `compact`, `ide`, `terminal`). | `< 1.0 ms` |
| **Tool** | `get_active_context` | Alias for `get_desktop_context`. | `< 1.0 ms` |
| **Tool** | `search_desktop_history` | Full-text keyword search across past window titles, tabs, and documents in SQLite WAL storage. | `< 15.0 ms` |
| **Tool** | `tag_active_control` | Adds or updates a dynamic rule in `%LOCALAPPDATA%\ADCE\semantic_rules.json` and updates live context immediately. | `< 10.0 ms` |
