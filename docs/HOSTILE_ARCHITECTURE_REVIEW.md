<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2024-2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../README.md) › [ 📚 Documentation Hub ](CONTEXT.md) › **Hostile Architecture & Systems Review**

---

# Hostile Architectural & Systems Review (Gate 2 Adversarial Audit)

> **Document Status:** Active / Master Epistemic Audit
> **Target Solution:** `ADCE.Core`, `ADCE.Extraction`, `ADCE.Storage`, `ADCE.Mcp`, `ADCE.Daemon`
> **Runtime:** .NET 10 (`net10.0-windows`) / C# 14 / `FlaUI.UIA3 5.0.0`
> **Role:** Principal Systems Architect & Windows Internals Specialist
> **Goal:** Expose fatal failure domains, race conditions, COM apartment deadlocks, and memory churn before production code implementation.

---

## 1. Concurrency & Threading Deadlocks

* ### Flaw 1.1: STA/MTA Apartment Mismatch & Hook Message Pump Starvation
  * **Failure Mechanism:** The design specifies installing `SetWinEventHook` and passing tokens to an MTA worker queue, while hosting a system tray icon (`NotifyIcon`) in `ADCE.Daemon`. In Win32, out-of-context WinEvent hooks (`WINEVENT_OUTOFCONTEXT`) deliver events via a hidden window message hook (`WH_GETMESSAGE`), requiring a running Win32 message pump (`GetMessage`/`DispatchMessage`) on the thread that called `SetWinEventHook`. If the hook is initialized on a background MTA worker or a thread pool worker without a message pump, event delivery silently stops. Conversely, if `SetWinEventHook` is registered on the WinForms/WPF STA UI thread, any synchronous COM RCW cleanup, blocking channel write, or cross-apartment COM marshaling will freeze the Windows shell event dispatch pipeline, causing desktop-wide input lag.
  * **Mandated Engineering Fix:** Isolate the WinEvent pump onto a dedicated, long-running STA thread executing a continuous Win32 message loop (`MsgWaitForMultipleObjectsEx`). Decouple hook ingress from extraction by writing into an unbounded-drop channel with zero COM interaction on the pump thread.

  ```csharp
  public sealed class WinEventHookThread : IDisposable
  {
      private readonly Thread _pumpThread;
      private readonly ChannelWriter<DesktopEventToken> _channelWriter;
      private nint _hHook;
      private uint _pumpThreadId;

      public WinEventHookThread(ChannelWriter<DesktopEventToken> writer)
      {
          _channelWriter = writer;
          _pumpThread = new Thread(PumpThreadMain)
          {
              IsBackground = true,
              Name = "ADCE.WinEventPump.STA"
          };
          _pumpThread.SetApartmentState(ApartmentState.STA);
          _pumpThread.Start();
      }

      private void PumpThreadMain()
      {
          _pumpThreadId = NativeMethods.GetCurrentThreadId();
          NativeMethods.WinEventProc proc = HookCallback;
          _hHook = NativeMethods.SetWinEventHook(
              0x0003, // EVENT_SYSTEM_FOREGROUND
              0x8005, // EVENT_OBJECT_FOCUS
              nint.Zero,
              proc,
              0, 0,
              0x0000 | 0x0002 // WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
          );

          while (NativeMethods.GetMessage(out var msg, nint.Zero, 0, 0) > 0)
          {
              NativeMethods.TranslateMessage(ref msg);
              NativeMethods.DispatchMessage(ref msg);
          }

          if (_hHook != nint.Zero)
              NativeMethods.UnhookWinEvent(_hHook);
      }

      private void HookCallback(nint hHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
      {
          // Minimal work on hook callback: zero COM, zero heap allocations, direct value-struct write
          _channelWriter.TryWrite(new DesktopEventToken((ushort)eventType, hwnd, dwmsEventTime));
      }

      public void Dispose()
      {
          if (_pumpThreadId != 0)
              NativeMethods.PostThreadMessage(_pumpThreadId, 0x0012 /* WM_QUIT */, nint.Zero, nint.Zero);
      }
  }
  ```

---

* ### Flaw 1.2: Cross-Process COM RPC Hangs via UIPI and Unresponsive Targets
  * **Failure Mechanism:** Calls to `FlaUI.UIA3` (`automation.FromHandle(hwnd)`, `FindFirstDescendant`, `CacheRequest.Activate()`) translate to synchronous cross-process ALPC/RPC requests (`SendMessage(WM_GETOBJECT)` and `IUIAutomation::ElementFromHandle`). If the target application is:
    1. Performing a Gen 2 Garbage Collection pause (e.g., heavy Electron app).
    2. Paused under a debugger or frozen in an infinite JavaScript/native loop.
    3. Running at a higher integrity level (Elevated/Admin) while ADCE runs at Medium Integrity (UIPI block returning `E_ACCESSDENIED` `0x80070005`).
    The calling MTA thread blocks indefinitely or waits for the 20-second default OLE/RPC timeout (`RPC_E_TIMEOUT`), deadlocking ADCE's extraction queue.
  * **Mandated Engineering Fix:** Configure `IUIAutomation2::put_TransactionTimeout` to a strict 50ms threshold upon initializing FlaUI, pre-screen target HWND integrity levels using native token inspection before invoking UIA, and wrap extractions in a hard watchdog timeout.

  ```csharp
  public static class UiaTransactionConfigurator
  {
      public static void EnforceStrictTimeouts(UIA3Automation automation, uint timeoutMs = 50)
      {
          var nativeAutomation = automation.NativeAutomation;
          if (nativeAutomation is Interop.UIAutomationClient.IUIAutomation2 native2)
          {
              // Abort any cross-process COM query that stalls for > 50ms
              native2.TransactionTimeout = timeoutMs;
              native2.ConnectionTimeout = timeoutMs;
              native2.AutoSetFocus = 0; // Prevent UIA from sending WM_SETFOCUS to target
          }
      }
  }

  public static class IntegrityGating
  {
      [DllImport("user32.dll")]
      private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

      public static bool CanAccessProcess(nint hwnd)
      {
          GetWindowThreadProcessId(hwnd, out uint pid);
          using var processHandle = NativeMethods.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
          if (processHandle.IsInvalid) return false;

          // If target is elevated and ADCE is standard user, abort before entering COM
          return NativeMethods.GetProcessIntegrityLevel(processHandle) <= NativeMethods.GetCurrentProcessIntegrityLevel();
      }
  }
  ```

---

* ### Flaw 1.3: Channel Head-of-Line Blocking on Consumer Stall
  * **Failure Mechanism:** The architecture processes OS events sequentially through `Channel<DesktopEvent>`. If the consumer thread stalls for 500ms (e.g., executing SQLite disk I/O or waiting on a slow window), hundreds of rapid mouse moves and focus hops accumulate in the queue. When the consumer recovers, it processes obsolete historical states sequentially instead of jumping to the live active window.
  * **Mandated Engineering Fix:** Configure the channel as a bounded single-item conflation buffer (`BoundedChannelFullMode.DropOldest`, `Capacity = 1`). Desktop context is inherently state-based (latest state wins), not event-stream-based.

  ```csharp
  var eventChannel = Channel.CreateBounded<DesktopEventToken>(new BoundedChannelOptions(1)
  {
      FullMode = BoundedChannelFullMode.DropOldest,
      SingleWriter = true,
      SingleReader = true
  });
  ```

---

## 2. Memory & Allocation Churn

* ### Flaw 2.1: `IReadOnlyList<T>` LINQ Enumerator & Sequence Equality Heap Churn
  * **Failure Mechanism:** In `IdeContext.cs`, `BrowserContext.cs`, and `ExplorerContext.cs`, `Equals()` invokes `OpenEditorTabs.SequenceEqual(other.OpenEditorTabs)`. Because `OpenEditorTabs` is typed as `IReadOnlyList<TabItemInfo>`, the compiler calls `Enumerable.SequenceEqual<T>(IEnumerable<T>, IEnumerable<T>)`. This performs two heap allocations for `IEnumerator<T>` on **every single comparison**. During 50ms focus debouncing or mouse jitter, thousands of enumerator objects are allocated per second, forcing high-frequency Gen 0/Gen 1 GC pauses.
  * **Mandated Engineering Fix:** Replace all `IReadOnlyList<T>` interface properties with `ImmutableArray<T>` or concrete sealed collections, and implement indexer-based comparison loops that perform zero heap allocations.

  ```csharp
  public sealed record IdeContext : IEquatable<IdeContext>
  {
      public string? ActiveFilePath { get; init; }
      public string? ActiveSidebarView { get; init; }
      public ImmutableArray<TabItemInfo> OpenEditorTabs { get; init; } = ImmutableArray<TabItemInfo>.Empty;
      public string? EditBuffer { get; init; }
      public string? GitBranch { get; init; }
      public ImmutableArray<string> Breadcrumbs { get; init; } = ImmutableArray<string>.Empty;

      public bool Equals(IdeContext? other)
      {
          if (other is null) return false;
          if (ReferenceEquals(this, other)) return true;

          return ActiveFilePath == other.ActiveFilePath &&
                 ActiveSidebarView == other.ActiveSidebarView &&
                 EditBuffer == other.EditBuffer &&
                 GitBranch == other.GitBranch &&
                 FastArrayEquals(OpenEditorTabs, other.OpenEditorTabs) &&
                 FastArrayEquals(Breadcrumbs, other.Breadcrumbs);
      }

      private static bool FastArrayEquals<T>(ImmutableArray<T> a, ImmutableArray<T> b) where T : IEquatable<T>
      {
          if (a.Length != b.Length) return false;
          for (int i = 0; i < a.Length; i++)
          {
              if (!a[i].Equals(b[i])) return false;
          }
          return true; // Zero enumerators, zero boxing, pure register-indexed comparison
      }
  }
  ```

---

* ### Flaw 2.2: Event Token Heap Allocation in WinEvent Callback
  * **Failure Mechanism:** In `DesktopEvent.cs`, `ForegroundChangedEvent`, `FocusChangedEvent`, and `StructureChangedEvent` are `abstract`/`sealed record` reference types. Every time the user moves focus across list items, inputs, or tabs, a new heap object is instantiated inside the OS callback path.
  * **Mandated Engineering Fix:** Refactor OS event ingestion to a 16-byte unmanaged `readonly struct` value token. Defer polymorphic record construction until after the 50ms debouncing window confirms the target window is settled.

  ```csharp
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public readonly record struct DesktopEventToken(
      ushort EventType,
      nint Hwnd,
      uint TimestampMs
  );
  ```

---

* ### Flaw 2.3: `StringBuilder` & P/Invoke Heap Churn in Win32 Gating
  * **Failure Mechanism:** In `Program.cs`, `GetWindowText` and `GetClassName` instantiate `new StringBuilder(512)` and `new StringBuilder(256)` inside enumeration loops. This allocates managed string buffers and intermediate strings on every discovery cycle.
  * **Mandated Engineering Fix:** Use `stackalloc char` buffers and `Span<char>` with `GetWindowTextW` / `GetClassNameW` P/Invokes, performing zero heap allocations.

  ```csharp
  public static unsafe (string Title, string ClassName) GetWindowIdentityFast(nint hwnd)
  {
      Span<char> titleBuffer = stackalloc char[256];
      Span<char> classBuffer = stackalloc char[128];

      int titleLen = NativeMethods.GetWindowTextW(hwnd, ref MemoryMarshal.GetReference(titleBuffer), titleBuffer.Length);
      int classLen = NativeMethods.GetClassNameW(hwnd, ref MemoryMarshal.GetReference(classBuffer), classBuffer.Length);

      string title = titleLen > 0 ? new string(titleBuffer[..titleLen]) : string.Empty;
      string className = classLen > 0 ? new string(classBuffer[..classLen]) : string.Empty;

      return (title, className);
  }
  ```

---

## 3. Boundary & Interop Friction

* ### Flaw 3.1: NativeAOT Incompatibility with `FlaUI.UIA3` & COM RCWs
  * **Failure Mechanism:** The project targets .NET 10 with NativeAOT plans while using `FlaUI.UIA3 5.0.0`. `FlaUI.UIA3` relies on built-in COM interop (`[ComImport]`, `Marshal.GetObjectForIUnknown`, and runtime RCW activation via `Activator.CreateInstance(Type.GetTypeFromCLSID(...))`). Under NativeAOT (`<PublishAot>true</PublishAot>`), dynamic COM RCW generation and non-source-generated `[ComImport]` interfaces are unsupported and trimmed by the IL compiler, throwing `PlatformNotSupportedException` or failing with `COR_E_NOTSUPPORTED` at runtime.
  * **Mandated Engineering Fix:** Configure explicit COM interop preservation in project properties, or migrate low-level UIA3 bindings to C# 12+ Source-Generated COM wrappers using `[GeneratedComInterface]` and `StrategyBasedComWrappers`.

  ```xml
  <!-- In ADCE.Extraction.csproj and ADCE.Daemon.csproj -->
  <PropertyGroup>
      <PublishAot>true</PublishAot>
      <!-- Preserve COM interop marshalling metadata during AOT compilation -->
      <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
      <IlcTrimMetadata>false</IlcTrimMetadata>
  </PropertyGroup>
  ```

---

* ### Flaw 3.2: 32-bit / 64-bit Architecture Mismatch for Python Clients (Dragonfly / Caster)
  * **Failure Mechanism:** The specification plans to use Simon Mourier's `RegfreeNetComServer` (SxS in-process/out-of-process COM). In accessibility environments, Dragon NaturallySpeaking (`natspeak.dll`) forces Python (Caster/Dragonfly) to run as a **32-bit (x86)** process (`python.exe`). A 64-bit NativeAOT .NET 10 in-process COM DLL (`.dll`) cannot be loaded into a 32-bit address space, throwing `OSError: [WinError 193] %1 is not a valid Win32 application`.
  * **Mandated Engineering Fix:** Decouple the IPC transport entirely from COM DLL bitness. Use a local Win32 Named Pipe (`\\.\pipe\adce_context`) delivering framing-delimited JSON-RPC 2.0. Named Pipes are bitness-agnostic (x86 Python ↔ x64 .NET 10) and support async I/O without COM apartment initialization in Python.

  ```csharp
  public sealed class NamedPipeServerHost
  {
      public static async Task StartAsync(IDesktopStateStore store, CancellationToken ct)
      {
          var pipeSecurity = new PipeSecurity();
          pipeSecurity.AddAccessRule(new PipeAccessRule(
              WindowsIdentity.GetCurrent().User!,
              PipeAccessRights.ReadWrite,
              AccessControlType.Allow));

          while (!ct.IsCancellationRequested)
          {
              await using var pipeServer = NamedPipeServerStreamAot.Create(
                  "adce_context",
                  PipeDirection.InOut,
                  10,
                  PipeTransmissionMode.Byte,
                  PipeOptions.Asynchronous,
                  pipeSecurity);

              await pipeServer.WaitForConnectionAsync(ct);
              _ = ProcessClientRpcAsync(pipeServer, store, ct);
          }
      }
  }
  ```

---

## 4. Privacy & Data Leaks

* ### Flaw 4.1: Plaintext Exfiltration of OAuth Tokens, Passwords, and Secrets
  * **Failure Mechanism:**
    1. `BrowserContext.UrlAddress`: Captures raw address bar strings, including authentication parameters (e.g., `https://auth.local/callback?access_token=ey...`, password reset tokens, and internal intranet paths).
    2. `FocusedControlInfo.ValueSnippet`: When focus enters a password field, terminal sudo prompt, or `.env` file editor, `ValueSnippet` reads and stores plaintext credentials.
    3. `TerminalContext.ActiveBuffer`: Captures terminal output containing database connection strings, API keys, and environment variables.
  * **Mandated Engineering Fix:** Implement an explicit redaction firewall at extraction time. Query `IsPasswordProperty` on focused elements, strip query strings from URLs, and enforce an ignore-list on sensitive file extensions (`.env`, `.pem`, `.key`, `id_rsa`).

  ```csharp
  public static class ContextPrivacySanitizer
  {
      private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
      {
          ".env", ".pem", ".key", ".pfx", "id_rsa", "secrets.json", "credentials"
      };

      public static string SanitizeUrl(string? rawUrl)
      {
          if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;
          if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
          {
              // Strip all query strings and fragment identifiers containing credentials
              return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
          }
          return "[REDACTED_INVALID_URL]";
      }

      public static string? SanitizeBuffer(string? buffer, string? fileName, bool isPasswordControl)
      {
          if (isPasswordControl) return "[REDACTED_PASSWORD]";
          if (fileName != null && SensitiveExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
          {
              return "[REDACTED_SENSITIVE_FILE_BUFFER]";
          }
          return buffer;
      }
  }
  ```

---

* ### Flaw 4.2: Unauthenticated Local MCP HTTP/SSE Listener Exposure
  * **Failure Mechanism:** The specification defines exposing MCP endpoints (`desktop://current`, `get_desktop_context`) over HTTP/SSE on `localhost`. Any unprivileged process, browser-based CSRF attack, or malicious script running on `127.0.0.1` can query the HTTP server and stream the user's active editor code, open tabs, and window titles in real time without authentication.
  * **Mandated Engineering Fix:** Bind MCP strictly to Standard I/O (`stdio`) or Named Pipes protected by Windows DACLs. If HTTP/SSE transport is enabled, generate a cryptographically random authorization bearer token at startup stored in a memory-mapped file restricted to the current user's SID.

---

## 5. State Machine & Lifecycle Edge Cases

* ### Flaw 5.1: Chromium Lazy Accessibility Tree Initialization Blind Spot
  * **Failure Mechanism:** Chromium and Electron applications (VS Code, Antigravity, Slack, Chrome) do not maintain an active UI Automation tree by default; they initialize `BrowserAccessibilityManager` lazily on their background render threads upon receiving the first `WM_GETOBJECT` request. This takes **150–300ms**. When ADCE fires extraction immediately upon `EVENT_SYSTEM_FOREGROUND`, `FindFirstDescendant("tabs-container")` returns `null` or an empty container. Because ADCE's deduplication logic checks sequence equality, subsequent empty states match, and ADCE **permanently caches an empty context**, blinding downstream AI agents until another window transition occurs.
  * **Mandated Engineering Fix:** Implement state-aware retry scheduling with a degradation flag. When an archetype extraction returns zero tabs for a known tabbed host, schedule an asynchronous trailing re-probe at 150ms and 300ms without blocking the main event queue.

  ```csharp
  public async ValueTask<DesktopContextSnapshot> ExtractWithLazyTreeRecoveryAsync(nint hwnd, DesktopAppArchetype archetype)
  {
      var snapshot = await ExtractDirectAsync(hwnd);

      if (archetype == DesktopAppArchetype.ChromiumElectron &&
          snapshot.IdeContext != null &&
          snapshot.IdeContext.OpenEditorTabs.IsEmpty)
      {
          _ = Task.Run(async () =>
          {
              await Task.Delay(200);
              var recoveredSnapshot = await ExtractDirectAsync(hwnd);
              if (recoveredSnapshot.IdeContext?.OpenEditorTabs.Length > 0)
              {
                  _stateStore.UpdateCurrentSnapshot(recoveredSnapshot);
              }
          });
      }

      return snapshot;
  }
  ```

---

* ### Flaw 5.2: HWND Destruction Race Condition (1ms Window Tear-Down)
  * **Failure Mechanism:** A transient window (popup, tooltip, dialog) opens, fires `EVENT_SYSTEM_FOREGROUND`, and is destroyed 1ms later while ADCE's 50ms debounce timer is running. When the worker invokes `automation.FromHandle(hwnd)`, the window handle is already invalid (`!IsWindow(hwnd)`). The underlying COM call to `IUIAutomation::ElementFromHandle` throws `COMException` with `UIA_E_ELEMENTNOTAVAILABLE` (`0x80040201`), `ERROR_INVALID_WINDOW_HANDLE` (`0x80070578`), or `E_FAIL` (`0x80004005`), crashing the extraction pipeline if unhandled.
  * **Mandated Engineering Fix:** Pre-validate handle liveness via `IsWindow`, encapsulate all COM calls in a resilience wrapper targeting specific UIA HRESULTs, and provide an atomic fallback to the desktop root or previous known-good state.

  ```csharp
  public static AutomationElement? SafeBindWindow(UIA3Automation automation, nint hwnd)
  {
      if (!NativeMethods.IsWindow(hwnd)) return null;

      try
      {
          return automation.FromHandle(hwnd);
      }
      catch (COMException ex) when (ex.HResult is unchecked((int)0x80040201) /* UIA_E_ELEMENTNOTAVAILABLE */ or
                                    unchecked((int)0x80070578) /* ERROR_INVALID_WINDOW_HANDLE */ or
                                    unchecked((int)0x80004005) /* E_FAIL */)
      {
          return null;
      }
  }
  ```

---

## 6. Hostile Review Summary & Required Actions

| Domain | Critical Flaw | Mandated Action | Affected Milestone |
| :--- | :--- | :--- | :--- |
| **Concurrency** | `SetWinEventHook` pump starvation & UIPI cross-process COM hangs. | Implement dedicated STA message pump; set `TransactionTimeout = 50ms`. | **Milestone 2 & 3** |
| **Memory** | `IReadOnlyList<T>.SequenceEqual` heap allocations on every tick. | Replace with `ImmutableArray<T>` and custom indexer-based comparison. | **Milestone 1 Refinement** |
| **Interop** | NativeAOT trimming breaks COM RCWs; x64 DLL incompatible with x86 Python. | Configure AOT COM preservation; use DACL-restricted Named Pipes. | **Milestone 5 & 6** |
| **Privacy** | Address bar OAuth tokens & IDE secret buffers broadcasted in plaintext. | Implement URL parameter sanitizer & password/sensitive file redaction. | **Milestone 2 & 5** |
| **Lifecycle** | Chromium lazy accessibility tree causes permanent empty-state cache. | Implement 200ms non-blocking deferred probe on degraded snapshots. | **Milestone 2 & 3** |
