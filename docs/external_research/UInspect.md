<!--
SPDX-License-Identifier: Apache-2.0
Copyright (c) 2024-2026 Amir Farhadi
-->

# Codebase Deep Dive: `smourier/UInspect`

## Executive Summary

`UInspect` is a pure C# .NET implementation and replacement for the classic Windows SDK `Inspect.exe` tool. It provides direct, low-level inspection of the Windows UI Automation (UIA) tree, element properties, supported control patterns, and live structure change event dispatching.

---

## 1. Architectural Architecture & Mechanics

### 1.1 Dedicated MTA Single-Thread Task Scheduler
A critical architectural insight from `UInspect` is how it handles the COM threading model of Windows UI Automation. 

In `AutomationUtilities.cs`:
```csharp
private static readonly SingleThreadTaskScheduler _scheduler = new SingleThreadTaskScheduler(t =>
{
    t.SetApartmentState(ApartmentState.MTA);
    return true;
});
```

#### Why MTA is Mandatory:
- According to Microsoft's official UIA threading rules, UI Automation COM objects (`CUIAutomation`, `IUIAutomation`, `IUIAutomationElement`) perform cross-process COM calls.
- If invoked on an STA (Single-Threaded Apartment) thread (such as the UI thread of a WinForms or WPF application), outgoing cross-process calls pump Windows messages while waiting for replies. If the target application sends a re-entrant message or event callback back to the STA thread, **deadlocks occur**.
- `UInspect` enforces that **all** calls to `CUIAutomation` and its elements execute exclusively on a dedicated background MTA worker thread via `SingleThreadTaskScheduler.RunAutomationTask()`.

### 1.2 Structure Changed Event Dispatching
In `AutomationElement.cs`:
```csharp
public event EventHandler<StructureChangedEventArgs> StructureChanged
{
    add
    {
        _ = AutomationUtilities.RunAutomationTaskAsync(() =>
        {
            var handler = new AutomationStructureChangedEventHandler(this, value);
            AutomationUtilities.Automation.AddStructureChangedEventHandler(
                Element, 
                TreeScope.TreeScope_Subtree, 
                null, 
                handler);
            _structureChangedHandlers[value] = handler;
        });
    }
    remove
    {
        if (_structureChangedHandlers.TryRemove(value, out var handler))
        {
            AutomationUtilities.RunAutomationTask(() => 
                AutomationUtilities.Automation.RemoveStructureChangedEventHandler(Element, handler));
        }
    }
}
```

#### Event Handler Implementation:
- Implements COM interface `IUIAutomationStructureChangedEventHandler`.
- Method `HandleStructureChangedEvent(IUIAutomationElement sender, StructureChangeType changeType, Array runtimeId)` captures structural mutations (`ChildAdded`, `ChildRemoved`, `ChildrenInvalidated`, `ChildrenBulkAdded`, `ChildrenBulkRemoved`, `ChildrenReordered`).
- Automatically filters out self-events (e.g. ignoring tracing windows like `WpfTraceSpy`).

### 1.3 Property & Pattern Reflection Engine
`UInspect` discovers available properties and patterns dynamically by querying:
- Standard UIA Property IDs (`UIA_PropertyIds`).
- Standard UIA Pattern IDs (`UIA_PatternIds`), requesting interfaces like `IUIAutomationInvokePattern`, `IUIAutomationValuePattern`, `IUIAutomationTextPattern`, `IUIAutomationSelectionPattern`, etc.
- ARIA properties category (`AutomationElement.CategoryAria`).

---

## 2. Comparison: `UInspect` vs `FlaUI.UIA3`

| Feature / Dimension | `UInspect` Approach | `FlaUI.UIA3` Approach (v5.0.0+) | ADCE Strategic Recommendation |
| :--- | :--- | :--- | :--- |
| **UIA Interop Layer** | `UIAutomationClient.dll` PIA / Interop | Custom cleaned COM vtable interop (`FlaUI.UIA3`) | **FlaUI.UIA3** (Better memory footprint and cleaner C# wrappers). |
| **Threading Model** | Dedicated explicit `SingleThreadTaskScheduler` (MTA) | Caller-managed threading | **Adopt UInspect's MTA Task Scheduler pattern** inside the ADCE daemon to prevent cross-process COM deadlocks. |
| **Tree Traversal** | Raw `IUIAutomationTreeWalker` & `FindAll` | `AutomationElement.FindAll` / `FindFirst` with `CacheRequest` | **FlaUI with CacheRequest** (3-5x faster due to batched COM roundtrips). |
| **Event Handling** | Direct COM interface implementation (`IUIAutomationStructureChangedEventHandler`) | Event abstraction layer on top of native UIA COM events | **FlaUI UIA3 event sink abstractions** backed by MTA event dispatcher. |

---

## 3. Direct Actionable Value for ADCE

1. **Adopt the Dedicated MTA Task Scheduler Pattern:**
   ADCE should manage all UIA3 interactions through an isolated MTA background worker to prevent deadlocks during high-frequency telemetry harvesting.
2. **Structure Change Filtering:**
   `UInspect` demonstrates the exact pattern for subscribing to `TreeScope_Subtree` changes at the root or top-level window and mapping raw `runtimeId` arrays to managed element caches.
3. **Fallback Diagnostic Tooling:**
   `UInspect` serves as a standalone reference to verify raw UIA behavior when debugging discrepancies in FlaUI.
