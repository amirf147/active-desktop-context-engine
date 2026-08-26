<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › **ADCE.Core Deep-Dive & Architectural Reference**

---

# ADCE.Core Deep-Dive & Architectural Reference

> **Target Library:** `ADCE.Core` (.NET 10 / C# 14)
> **Purpose:** Plain-English architectural breakdown, modular UML class diagrams, state lifecycle machines, end-to-end sequence diagrams, and file-by-file failure mode analysis.
> **Parent Context:** [`docs/CONTEXT.md`](../CONTEXT.md) | [`docs/MCP_SCHEMA_SPEC.md`](../architecture/MCP_SCHEMA_SPEC.md)
> 🔍 **Interactive Full-Screen Visualizer:** [Open Interactive UML Architecture Diagram (Zoom &amp; Pan)](../diagrams/adce_core_architecture_uml.html)

---

## 1. Architectural View 1: Structural & UML Class Diagrams

To maintain high visual legibility and vertical scrollability in markdown viewers, the `ADCE.Core` structural model is organized into three focused sub-domain views:

### 1.1 Core Snapshot & Window Topology
This view models the root aggregate snapshot (`DesktopContextSnapshot`), top-level window identity (`WindowEnvelope`), virtual desktop boundaries (`WorkspaceEnvelope`), and active input focus (`FocusedControlInfo`).

```mermaid
classDiagram
    direction TB

    class DesktopContextSnapshot {
        +DateTimeOffset Timestamp
        +WorkspaceEnvelope Workspace
        +WindowEnvelope Window
        +FocusedControlInfo Focus
        +double ExtractionDurationMs
    }

    class WorkspaceEnvelope {
        +Guid VirtualDesktopId
        +int DesktopIndex
        +string VirtualDesktopName
        +int MonitorIndex
        +BoundingRectangle MonitorBounds
    }

    class WindowEnvelope {
        +nint Hwnd
        +string Title
        +string ProcessName
        +int Pid
        +string ClassName
        +DesktopAppArchetype Archetype
        +BoundingRectangle Bounds
        +bool IsMinimized
        +bool IsMaximized
    }

    class FocusedControlInfo {
        +string ControlType
        +string ElementName
        +BoundingRectangle BoundingBox
        +string AutomationId
        +string ClassName
        +DesktopSemanticZone SemanticZone
        +string ValueSnippet
    }

    class BoundingRectangle {
        +int Left
        +int Top
        +int Width
        +int Height
        +int Right
        +int Bottom
        +bool IsEmpty
    }

    class DesktopAppArchetype {
        <<enumeration>>
        ChromiumElectron
        Gecko
        WinUI3Xaml
        ClassicWin32
        CanvasToolkit
        Unknown
    }

    class DesktopSemanticZone {
        <<enumeration>>
        EditorCodeBuffer
        IntegratedTerminal
        GitCommitBox
        SidebarExplorer
        AddressBar
        DocumentContent
        ShellItemList
        TabBar
        StatusBar
        CommandPalette
        Unknown
    }

    DesktopContextSnapshot *-- WorkspaceEnvelope : Workspace
    DesktopContextSnapshot *-- WindowEnvelope : Window
    DesktopContextSnapshot *-- FocusedControlInfo : Focus
    WorkspaceEnvelope *-- BoundingRectangle : MonitorBounds
    WindowEnvelope *-- BoundingRectangle : Bounds
    WindowEnvelope *-- DesktopAppArchetype : Archetype
    FocusedControlInfo *-- BoundingRectangle : BoundingBox
    FocusedControlInfo *-- DesktopSemanticZone : SemanticZone
```

---

### 1.2 Specialized Application Contexts & Open Tabs
This view models application-specific semantic contexts extracted by specialized zone parsers for IDEs, Web Browsers, File Explorer, and Windows Terminal.

```mermaid
classDiagram
    direction TB

    class DesktopContextSnapshot {
        +IdeContext IdeContext
        +BrowserContext BrowserContext
        +ExplorerContext ExplorerContext
        +TerminalContext TerminalContext
    }

    class TabItemInfo {
        +int Index
        +string Title
        +bool IsActive
        +bool IsPinned
        +bool IsDirty
        +string Tooltip
        +string AutomationId
    }

    class IdeContext {
        +string ActiveFilePath
        +string ActiveSidebarView
        +IReadOnlyList~TabItemInfo~ OpenEditorTabs
        +string EditBuffer
        +string GitBranch
        +IReadOnlyList~string~ Breadcrumbs
        +Equals(IdeContext other) bool
        +GetHashCode() int
    }

    class BrowserContext {
        +string ContainerType
        +int TotalCount
        +string ActiveTab
        +IReadOnlyList~TabItemInfo~ Tabs
        +string UrlAddress
        +Equals(BrowserContext other) bool
        +GetHashCode() int
    }

    class ExplorerContext {
        +string CurrentPath
        +IReadOnlyList~string~ Breadcrumbs
        +IReadOnlyList~string~ SelectedItems
        +IReadOnlyList~TabItemInfo~ Tabs
        +Equals(ExplorerContext other) bool
        +GetHashCode() int
    }

    class TerminalContext {
        +string ShellTitle
        +string ActiveBuffer
        +IReadOnlyList~TabItemInfo~ Tabs
        +Equals(TerminalContext other) bool
        +GetHashCode() int
    }

    DesktopContextSnapshot o-- IdeContext : IdeContext
    DesktopContextSnapshot o-- BrowserContext : BrowserContext
    DesktopContextSnapshot o-- ExplorerContext : ExplorerContext
    DesktopContextSnapshot o-- TerminalContext : TerminalContext

    IdeContext o-- TabItemInfo : OpenEditorTabs
    BrowserContext o-- TabItemInfo : Tabs
    ExplorerContext o-- TabItemInfo : Tabs
    TerminalContext o-- TabItemInfo : Tabs
```

---

### 1.3 OS Event Token Hierarchy & Core Engine Interfaces
This view models the lightweight, non-blocking OS event tokens enqueued from WinEvent hooks and the core engine contracts.

```mermaid
classDiagram
    direction TB

    class DesktopEventType {
        <<enumeration>>
        ForegroundChanged
        FocusChanged
        VirtualDesktopSwitched
        StructureChanged
        Heartbeat
        None
    }

    class DesktopEvent {
        <<abstract>>
        +DesktopEventType EventType
        +DateTimeOffset Timestamp
    }

    class ForegroundChangedEvent {
        +nint Hwnd
        +string ProcessName
        +string ClassName
        +int Pid
    }

    class FocusChangedEvent {
        +nint Hwnd
        +string ControlType
        +string AutomationId
        +string ElementName
    }

    class VirtualDesktopSwitchedEvent {
        +Guid NewDesktopId
        +int DesktopIndex
    }

    class StructureChangedEvent {
        +nint Hwnd
    }

    class HeartbeatEvent {
    }

    class IExtractionEngine {
        <<interface>>
        +ExtractSnapshotAsync(nint hwnd) ValueTask~DesktopContextSnapshot~
        +ExtractForegroundSnapshotAsync() ValueTask~DesktopContextSnapshot~
    }

    class IDesktopStateStore {
        <<interface>>
        +GetCurrentSnapshot() DesktopContextSnapshot
        +UpdateCurrentSnapshot(DesktopContextSnapshot snapshot) void
        +GetHistoryAsync(DateTimeOffset since, int limit) IAsyncEnumerable~DesktopContextSnapshot~
        +SearchHistoryAsync(string query, int limit) IAsyncEnumerable~DesktopContextSnapshot~
    }

    class IWorkspaceManager {
        <<interface>>
        +GetCurrentWorkspaceAsync() ValueTask~WorkspaceEnvelope~
        +GetWindowWorkspaceAsync(nint hwnd) ValueTask~WorkspaceEnvelope~
        +GetAllWorkspacesAsync() ValueTask~WorkspaceList~
    }

    class IEventHookProvider {
        <<interface>>
        +ChannelReader~DesktopEvent~ EventReader
        +Start() void
        +Stop() void
        +bool IsRunning
    }

    class IArchetypeClassifier {
        <<interface>>
        +Classify(string className, string processName, string title) DesktopAppArchetype
    }

    DesktopEvent <|-- ForegroundChangedEvent
    DesktopEvent <|-- FocusChangedEvent
    DesktopEvent <|-- VirtualDesktopSwitchedEvent
    DesktopEvent <|-- StructureChangedEvent
    DesktopEvent <|-- HeartbeatEvent
    DesktopEvent *-- DesktopEventType : EventType
```

---

## 2. Architectural View 2: State & Lifecycle Transition Diagram

This state machine tracks the end-to-end lifecycle of desktop context—from raw OS WinEvent triggers, through the 50ms debouncing window and multi-zone extraction, to **deep sequence-equality deduplication** and MCP publishing.

```mermaid
stateDiagram-v2
    [*] --> Idle : Daemon Startup

    state Idle {
        [*] --> AwaitingEvent : 0% CPU Channel.ReadAsync()
    }

    Idle --> Enqueuing : OS WinEvent Fired (Foreground / Focus / Virtual Desktop)

    state Enqueuing {
        [*] --> PushToken : WinEvent Hook Callback (< 0.01 ms)
        PushToken --> WriteChannel : Channel.Writer.TryWrite(token)
    }

    Enqueuing --> Debouncing : Token Enqueued in Channel<DesktopEvent>

    state Debouncing {
        [*] --> TrailingEdge : Window Settling Timer (50ms)
        TrailingEdge --> CancelPrevious : Rapid Focus Jitter (Reset Timer)
        TrailingEdge --> ReadyForExtraction : Stable Target Window Settled
    }

    Debouncing --> Extracting : MTA Worker Dequeues Token

    state Extracting {
        [*] --> FastWin32Gating : Query Class, Title, PID (< 0.5 ms)
        FastWin32Gating --> ArchetypeResolution : Classify Framework Archetype
        ArchetypeResolution --> UiaCacheRequest : Scoped Batch Cache (< 15 ms)
        UiaCacheRequest --> SnapshotBuilt : Construct Immutable DesktopContextSnapshot
    }

    Extracting --> Deduplicating : Snapshot Delivered to IDesktopStateStore

    state Deduplicating {
        [*] --> CompareState : (newSnapshot == currentSnapshot)
        CompareState --> SequenceEqualityCheck : Deep SequenceEqual on Tabs & Buffers
        SequenceEqualityCheck --> DropDuplicate : TRUE (Identical Context)
        SequenceEqualityCheck --> CommitUpdate : FALSE (New Active Context)
    }

    Deduplicating --> Idle : DropDuplicate (0 µs overhead, No DB writes, No MCP broadcast)

    state Publishing {
        [*] --> MemoryCacheUpdated : Atomic Reference Swap (< 1 µs)
        MemoryCacheUpdated --> AsyncDbWrite : Queue SQLite WAL Insert
        MemoryCacheUpdated --> McpBroadcast : Push SSE / Stdio MCP Update
    }

    Deduplicating --> Publishing : CommitUpdate
    Publishing --> Idle : Context Cached & Broadcasted
```

---

## 3. Architectural View 3: Execution & Sequence Dataflow

This sequence diagram details the synchronous vs. asynchronous boundaries across threads and components.

```mermaid
sequenceDiagram
    autonumber
    actor OS as Windows OS (Win32)
    participant Hook as IEventHookProvider
    participant Chan as Channel<DesktopEvent>
    participant Worker as MTA Extraction Worker
    participant Extractor as IExtractionEngine
    participant Store as IDesktopStateStore
    participant MCP as MCP JSON-RPC Server

    OS->>Hook: EVENT_SYSTEM_FOREGROUND (0x0003)
    Note over Hook: Instantly allocates ForegroundChangedEvent (< 0.01 ms)
    Hook->>Chan: WriteAsync(token)
    Note over Hook: Returns immediately (0.0% UI thread lag)

    Chan->>Worker: ReadAsync(token) (Debounce 50ms)
    Worker->>Extractor: ExtractSnapshotAsync(hwnd)
    Extractor->>Extractor: Construct DesktopContextSnapshot (Immutable Record)
    Extractor->>Store: UpdateCurrentSnapshot(newSnapshot)

    alt Snapshot Value Equality Match (newSnapshot == currentSnapshot)
        Note over Store: Sequence equality on tabs/buffers returns TRUE
        Store-->>Worker: Dropped as redundant duplicate (0 µs overhead)
    else State Actually Changed (newSnapshot != currentSnapshot)
        Store->>Store: Update in-memory live reference (< 1 µs)
        Store->>Store: Queue SQLite WAL async write
        Store->>MCP: Stream updated state via AdceJsonSerializerOptions
        Note over MCP: Serialized to snake_case JSON (0x00DB083E HWND)
    end
```

---

## 4. File-by-File Failure Mode & Responsibility Matrix

Every file in `ADCE.Core` was introduced to eliminate a specific systems failure mode.

| Directory | File | Core Responsibility | Failure Mode Prevented |
| :--- | :--- | :--- | :--- |
| **`Models/`** | [`DesktopContextSnapshot.cs`](../../src/ADCE.Core/Models/DesktopContextSnapshot.cs) | Root immutable record holding the complete desktop context state at a point in time. | **Context Fragmentation & Race Conditions:** Prevents multi-threaded consumers (MCP, storage, voice grammar) from reading partially mutated state. |
| | [`WorkspaceEnvelope.cs`](../../src/ADCE.Core/Models/WorkspaceEnvelope.cs) | Holds virtual desktop GUID, friendly name, workspace index, and monitor bounds. | **Workspace Blindness:** Prevents voice grammars and AI agents from confusing windows across different virtual desktops. |
| | [`WindowEnvelope.cs`](../../src/ADCE.Core/Models/WindowEnvelope.cs) | Encapsulates process metadata, HWND, window title, Win32 class, and archetype. | **HWND Lifetime Ambiguity:** Captures process identity and bounds upfront so subsequent queries don't fail if the window closes. |
| | [`FocusedControlInfo.cs`](../../src/ADCE.Core/Models/FocusedControlInfo.cs) | Stores focused control type, accessibility name, automation ID, and bounding rectangle. | **Target Drift:** Captures the exact active input target at the moment of focus without holding live COM proxy references. |
| | [`BoundingRectangle.cs`](../../src/ADCE.Core/Models/BoundingRectangle.cs) | Lightweight `readonly record struct` for screen coordinates (`Left`, `Top`, `Width`, `Height`). | **Heap Allocation Bloat:** Eliminates GC pressure by allocating bounding geometry on the stack instead of heap objects. |
| | [`TabItemInfo.cs`](../../src/ADCE.Core/Models/TabItemInfo.cs) | Represents open tab items with title, active state, pinned state, and dirty flag. | **Tab Parsing Inconsistency:** Provides a single unified model across IDEs, browsers, Explorer, and Terminal. |
| | [`IdeContext.cs`](../../src/ADCE.Core/Models/IdeContext.cs) | Captures active file path, sidebar view, Monaco breadcrumbs, Git branch, and open editor tabs. | **IDE State Ambiguity:** Prevents AI coding assistants from needing expensive file tree scans by providing active buffers directly. |
| | [`BrowserContext.cs`](../../src/ADCE.Core/Models/BrowserContext.cs) | Captures container type (TreeStyleTab / native), tab count, active tab title, and open tabs list. | **DOM Crawling Traps:** Replaces 6,800-node DOM crawls with isolated tab container extraction. |
| | [`ExplorerContext.cs`](../../src/ADCE.Core/Models/ExplorerContext.cs) | Captures directory path, path breadcrumbs, selected items, and Win11 TabView tabs. | **Shell Navigation Blindness:** Gives voice engines instant awareness of active folders and selected files. |
| | [`TerminalContext.cs`](../../src/ADCE.Core/Models/TerminalContext.cs) | Captures active terminal tab, shell title, and recent buffer text. | **Console Isolation:** Bridges terminal state into the unified desktop semantic graph. |
| **`Enums/`** | [`DesktopSemanticZone.cs`](../../src/ADCE.Core/Enums/DesktopSemanticZone.cs) | Identifies functional UI zones (`EditorCodeBuffer`, `AddressBar`, `GitCommitBox`, `SidebarExplorer`). | **Raw Selector Fragility:** Downstream tools check semantic intent rather than fragile UI automation paths. |
| | [`DesktopAppArchetype.cs`](../../src/ADCE.Core/Enums/DesktopAppArchetype.cs) | Categorizes apps into 5 universal desktop framework archetypes (`ChromiumElectron`, `Gecko`, `WinUI3Xaml`, `ClassicWin32`, `CanvasToolkit`). | **Hardcoded App Fragility:** Enables recipe-based dynamic discovery without hardcoding string rules per application. |
| | [`DesktopEventType.cs`](../../src/ADCE.Core/Enums/DesktopEventType.cs) | Classifies OS events (`ForegroundChanged`, `FocusChanged`, `VirtualDesktopSwitched`, `StructureChanged`, `Heartbeat`). | **Event Type Ambiguity:** Provides strongly-typed dispatching inside Channel consumers. |
| **`Events/`** | [`DesktopEvent.cs`](../../src/ADCE.Core/Events/DesktopEvent.cs) | Polymorphic hierarchy of lightweight event tokens. | **OS Message Loop Lag:** Allows WinEvent callbacks to post minimal structs to a channel and exit in $< 0.01\text{ ms}$, preventing UI stutter. |
| **`Interfaces/`**| [`IExtractionEngine.cs`](../../src/ADCE.Core/Interfaces/IExtractionEngine.cs) | Interface for extracting `DesktopContextSnapshot` from HWND or foreground. | **Tight Coupling to FlaUI:** Allows extraction implementations to be swapped or mocked in unit tests without COM runtime. |
| | [`IDesktopStateStore.cs`](../../src/ADCE.Core/Interfaces/IDesktopStateStore.cs) | Interface for in-memory cache and historical SQLite queries. | **Database Coupling:** Decouples storage persistence from MCP endpoints and daemon services. |
| | [`IWorkspaceManager.cs`](../../src/ADCE.Core/Interfaces/IWorkspaceManager.cs) | Interface for virtual desktop discovery. | **COM Virtual Desktop Deadlocks:** Wraps desktop COM calls behind an asynchronous interface. |
| | [`IEventHookProvider.cs`](../../src/ADCE.Core/Interfaces/IEventHookProvider.cs) | Interface for OS hook lifecycle and Channel exposure. | **Resource Leaks:** Ensures WinEvent hooks are cleanly disposed and unhooked on shutdown. |
| | [`IArchetypeClassifier.cs`](../../src/ADCE.Core/Interfaces/IArchetypeClassifier.cs) | Interface for classifying HWNDs into archetypes. | **Hardcoded Routing:** Enables extensible rule-based and heuristic window classification. |
| **`Serialization/`**| [`HwndJsonConverter.cs`](../../src/ADCE.Core/Serialization/HwndJsonConverter.cs) | Custom `JsonConverter<nint>` formatting HWND as hex strings (e.g. `0x00DB083E`). | **Signed Negative Signs & OverflowExceptions:** Prevents `-0x...` formatting and `Convert.ToInt64` crashes on high-bit addresses. |
| | [`AdceJsonSerializerOptions.cs`](../../src/ADCE.Core/Serialization/AdceJsonSerializerOptions.cs) | Pre-configured `JsonSerializerOptions` enforcing `snake_case` naming and null omission. | **MCP Schema Non-Compliance:** Guarantees 100% byte-level compatibility with `docs/MCP_SCHEMA_SPEC.md`. |

---

## 5. Deep-Dive: How Sequence Equality Works on Application Contexts

### The C# Record Collection Trap
By default, C# `record` types synthesize an equality operator (`Equals`) that compares each property using `EqualityComparer<T>.Default`.

For standard value types and strings (`int`, `string`, `Guid`, `enum`), this performs value comparison. However, for collection types (`IReadOnlyList<T>`, `List<T>`, arrays), `EqualityComparer<T>.Default` performs **reference equality**!

```csharp
// ❌ WITHOUT CUSTOM EQUALITY:
var contextA = new IdeContext { OpenEditorTabs = new List<TabItemInfo> { new() { Title = "a.cs", IsActive = true } } };
var contextB = new IdeContext { OpenEditorTabs = new List<TabItemInfo> { new() { Title = "a.cs", IsActive = true } } };

bool naiveEquals = (contextA == contextB); // FALSE! (Different List<T> heap references)
```

If `contextA == contextB` evaluates to `false`, every time the user clicks inside an editor, `ADCE` would falsely assume the open tabs changed, writing duplicate records to SQLite and triggering unnecessary MCP notifications.

### The Solution: Explicit `IEquatable<T>` and `SequenceEqual`
In `ADCE.Core`, all context records containing collections implement explicit `IEquatable<T>`:

```csharp
public sealed record IdeContext : IEquatable<IdeContext>
{
    public string? ActiveFilePath { get; init; }
    public string? ActiveSidebarView { get; init; }
    public IReadOnlyList<TabItemInfo> OpenEditorTabs { get; init; } = [];
    public string? EditBuffer { get; init; }
    public string? GitBranch { get; init; }
    public IReadOnlyList<string> Breadcrumbs { get; init; } = [];

    public bool Equals(IdeContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return ActiveFilePath == other.ActiveFilePath &&
               ActiveSidebarView == other.ActiveSidebarView &&
               EditBuffer == other.EditBuffer &&
               GitBranch == other.GitBranch &&
               OpenEditorTabs.SequenceEqual(other.OpenEditorTabs) &&
               Breadcrumbs.SequenceEqual(other.Breadcrumbs);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ActiveFilePath);
        hash.Add(ActiveSidebarView);
        hash.Add(EditBuffer);
        hash.Add(GitBranch);
        foreach (var tab in OpenEditorTabs) hash.Add(tab);
        foreach (var b in Breadcrumbs) hash.Add(b);
        return hash.ToHashCode();
    }
}
```

### Why This is Optimal:
1. **Element-by-Element Structural Check:** `OpenEditorTabs.SequenceEqual(...)` iterates both lists and compares each `TabItemInfo` by value.
2. **Order-Sensitive Integrity:** Tab ordering matters in desktop context (e.g. Tab 1 vs Tab 2); `SequenceEqual` guarantees order is preserved.
3. **Composite HashCode Consistency:** Combining individual item hashes with `System.HashCode` ensures that equal records always produce identical hash codes, maintaining dictionary and hash set safety.

---

## 6. Verification Harness

To empirically verify and visualize these models in action, execute the dedicated verification harness in `src/ADCE.Spikes`:

```powershell
# Run the interactive domain model verification and JSON serialization spike
dotnet run --project src/ADCE.Spikes

# Run the live FlaUI UIA3 benchmark
dotnet run --project src/ADCE.Spikes -- --flaui-benchmark
```
