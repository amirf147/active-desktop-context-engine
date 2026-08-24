<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

[ 🏠 ADCE Home ](../README.md) › [ 📚 Documentation Hub ](CONTEXT.md) › **MCP Schema Specification**

---

# ADCE Model Context Protocol (MCP) Schema Specification

> **Status:** Draft / Evolving Specification
> **Protocol Standard:** [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) JSON-RPC 2.0
> **Parent Context:** [`docs/CONTEXT.md`](CONTEXT.md)

---

## 1. Design Rationale & Core Envelope Philosophy

The ADCE MCP schema is designed to provide AI agents, LLM tool callers, and voice recognition grammars with high-density, token-efficient semantic desktop context.

Rather than sending raw visual screenshots or thousands of unpruned UI Automation nodes, the schema exposes a structured snapshot partitioned into four decoupled context zones:
1. **Workspace Envelope:** Virtual desktop identity and multi-monitor spatial context.
2. **Window Envelope:** Active foreground process identity, HWND, and window title.
3. **Application Semantic Context:** High-level tabs, active buffers, breadcrumbs, or navigation state.
4. **Focus & Selection Context:** Exact control type, element name, and spatial bounding box of the active input target.

---

## 2. Draft Unified Context Schema (JSON Draft)

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
        "virtual_desktop_name": { "type": "string" },
        "desktop_index": { "type": "integer" }
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
        "class_name": { "type": "string" }
      },
      "required": ["hwnd", "title", "process_name", "pid", "class_name"]
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
        "edit_buffer": { "type": "string" }
      }
    },
    "browser_context": {
      "type": "object",
      "properties": {
        "container_type": { "type": "string" },
        "total_count": { "type": "integer" },
        "active_tab": { "type": "string" },
        "tabs": {
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
    "focus": {
      "type": "object",
      "properties": {
        "control_type": { "type": "string" },
        "element_name": { "type": "string" },
        "automation_id": { "type": "string" },
        "bounding_box": {
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
      "required": ["control_type", "element_name", "bounding_box"]
    }
  },
  "required": ["timestamp", "workspace", "window", "focus"]
}
```

---

## 3. Representative Snapshot Example

```json
{
  "timestamp": "2026-08-24T06:20:00.000Z",
  "workspace": {
    "virtual_desktop_id": "3f2a1b0c-4d5e-6f7a-8b9c-0d1e2f3a4b5c",
    "virtual_desktop_name": "Development",
    "desktop_index": 1
  },
  "window": {
    "hwnd": "0x00DB083E",
    "title": "active-desktop-context-engine - Antigravity IDE",
    "process_name": "Antigravity.exe",
    "pid": 26420,
    "class_name": "Chrome_WidgetWin_1"
  },
  "ide_context": {
    "active_file_path": "C:\\Users\\Amir\\Documents\\repos\\active-desktop-context-engine\\docs\\CONTEXT.md",
    "active_sidebar_view": "Explorer (Ctrl+Shift+E)",
    "open_editor_tabs": [
      { "title": "CONTEXT.md", "is_active": true },
      { "title": "UInspect.md", "is_active": false },
      { "title": "README.md", "is_active": false }
    ],
    "edit_buffer": "CONTEXT.md"
  },
  "focus": {
    "control_type": "Edit",
    "element_name": "CONTEXT.md",
    "automation_id": "",
    "bounding_box": { "left": 400, "top": 120, "width": 1200, "height": 800 }
  }
}
```

---

## 4. MCP Tools & Resources Endpoint Specification

ADCE exposes the following initial MCP endpoints:

| Endpoint Type | URI / Tool Name | Description | Response SLA |
| :--- | :--- | :--- | :--- |
| **Resource** | `desktop://current` | Live snapshot of the current foreground desktop state | `< 1.0 ms` (cached) |
| **Resource** | `desktop://history?minutes=15` | Time-series query of recent focus transitions | `< 5.0 ms` (SQLite) |
| **Tool** | `get_desktop_context` | Explicit context pull with optional application-level filtering | `< 10.0 ms` |
| **Tool** | `search_desktop_history` | Full-text search across past window titles, tabs, and documents | `< 15.0 ms` |
