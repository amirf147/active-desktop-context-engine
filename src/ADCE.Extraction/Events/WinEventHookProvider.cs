// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using ADCE.Core.Events;
using ADCE.Core.Interfaces;
using ADCE.Extraction.Win32;

namespace ADCE.Extraction.Events;

/// <summary>
/// Production-grade WinEvent hook provider capturing Windows OS foreground and focus transitions.
/// Features a dedicated STA thread message pump, ManualResetEventSlim initialization barrier,
/// unmanaged token noise filtering, and a zero-allocation bounded channel.
/// </summary>
public sealed class WinEventHookProvider : IEventHookProvider
{
    private readonly Channel<DesktopEventToken> _channel;
    private readonly ManualResetEventSlim _initBarrier = new(false);
    private readonly NativeMethods.WinEventProc _winEventProc;

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _foregroundHookHandle;
    private nint _focusHookHandle;
    private nint _selectionHookHandle;
    private nint _nameChangeHookHandle;
    private int _isRunning;
    private bool _disposed;

    public WinEventHookProvider(int channelCapacity = 128)
    {
        _channel = Channel.CreateBounded<DesktopEventToken>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        });

        // Retain delegate instance as private field to prevent unmanaged GC collection
        _winEventProc = OnWinEvent;
    }

    public ChannelReader<DesktopEventToken> EventReader => _channel.Reader;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return; // Already running
        }

        _initBarrier.Reset();

        _hookThread = new Thread(HookThreadLoop)
        {
            IsBackground = true,
            Name = "ADCE.WinEventHook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        if (!_initBarrier.Wait(TimeSpan.FromSeconds(3)))
        {
            Stop();
            throw new TimeoutException("Failed to initialize Windows WinEvent hook thread message queue within timeout.");
        }
    }

    public void Stop()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 0, 1) != 1)
        {
            return; // Already stopped
        }

        uint threadId = Volatile.Read(ref _hookThreadId);
        if (threadId != 0)
        {
            NativeMethods.PostThreadMessageW(threadId, NativeMethods.WM_QUIT, 0, 0);
        }

        if (_hookThread != null && _hookThread.IsAlive)
        {
            _hookThread.Join(TimeSpan.FromSeconds(2));
            _hookThread = null;
        }

        _hookThreadId = 0;
    }

    private void HookThreadLoop()
    {
        try
        {
            Volatile.Write(ref _hookThreadId, NativeMethods.GetCurrentThreadId());

            // 1. Force Win32 message queue creation on this thread before signaling caller
            NativeMethods.PeekMessageW(out _, nint.Zero, 0, 0, NativeMethods.PM_NOREMOVE);

            // 2. Install targeted Out-of-Context WinEvent Hooks:
            // Hook 1: Foreground transitions only (0x0003)
            _foregroundHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                nint.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // Hook 2: Focus transitions only (0x8005)
            _focusHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_FOCUS,
                NativeMethods.EVENT_OBJECT_FOCUS,
                nint.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // Hook 3: Selection transitions only (0x8006 - e.g. browser/IDE tab switches)
            _selectionHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_SELECTION,
                NativeMethods.EVENT_OBJECT_SELECTION,
                nint.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // Hook 4: Name change transitions only (0x800C - e.g. dynamic document title/URL changes)
            _nameChangeHookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_NAMECHANGE,
                NativeMethods.EVENT_OBJECT_NAMECHANGE,
                nint.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // 3. Signal initialization barrier so Start() can unblock safely
            _initBarrier.Set();

            if (_foregroundHookHandle == nint.Zero && _focusHookHandle == nint.Zero &&
                _selectionHookHandle == nint.Zero && _nameChangeHookHandle == nint.Zero)
            {
                return;
            }

            // 4. Standard Win32 Message Pump (Sleeps in kernel mode with 0.0% CPU when no messages)
            while (NativeMethods.GetMessageW(out var msg, nint.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessageW(ref msg);
            }
        }
        finally
        {
            if (_foregroundHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHookHandle);
                _foregroundHookHandle = nint.Zero;
            }

            if (_focusHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(_focusHookHandle);
                _focusHookHandle = nint.Zero;
            }

            if (_selectionHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(_selectionHookHandle);
                _selectionHookHandle = nint.Zero;
            }

            if (_nameChangeHookHandle != nint.Zero)
            {
                NativeMethods.UnhookWinEvent(_nameChangeHookHandle);
                _nameChangeHookHandle = nint.Zero;
            }

            _initBarrier.Set(); // Guard against hangs if exception occurred before set
        }
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        // Trap 2: WinEvent Noise Filtering
        // Only accept explicit Foreground (0x0003), Focus (0x8005), Selection (0x8006), and NameChange (0x800C) events
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND &&
            eventType != NativeMethods.EVENT_OBJECT_FOCUS &&
            eventType != NativeMethods.EVENT_OBJECT_SELECTION &&
            eventType != NativeMethods.EVENT_OBJECT_NAMECHANGE)
        {
            return;
        }

        // Guard 1: Drop background EVENT_OBJECT_NAMECHANGE storms unless originating from active foreground window
        if (eventType == NativeMethods.EVENT_OBJECT_NAMECHANGE)
        {
            var foregroundHwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != foregroundHwnd && NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER) != foregroundHwnd)
            {
                return; // Ignore background name change storms
            }
        }

        // Accept OBJID_WINDOW (0) and OBJID_CLIENT (-4)
        if (idObject != NativeMethods.OBJID_CLIENT && idObject != NativeMethods.OBJID_WINDOW)
        {
            return;
        }

        if (hwnd == nint.Zero)
        {
            return;
        }

        // Guard 3: Root HWND Normalization for OBJID_CLIENT & child sub-surface events
        // Normalizes child rendering HWNDs (e.g. Electron/Gecko sub-windows) to top-level window handle
        nint rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOTOWNER);
        if (rootHwnd != nint.Zero && NativeMethods.IsWindow(rootHwnd))
        {
            hwnd = rootHwnd;
        }

        // Trap 3 & 5: End-to-End zero-allocation struct enqueueing into bounded DropOldest channel
        _channel.Writer.TryWrite(new DesktopEventToken((ushort)eventType, hwnd, dwmsEventTime));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _channel.Writer.TryComplete();
            _initBarrier.Dispose();
            _disposed = true;
        }
    }
}
