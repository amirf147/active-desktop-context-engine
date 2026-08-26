// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ADCE.Daemon.UI;

/// <summary>
/// Thread-safe clipboard helper ensuring all Windows OLE clipboard operations execute on an STA thread.
/// Guards against <see cref="ThreadStateException"/> when invoked from asynchronous or ThreadPool MTA threads,
/// and includes an exponential/backoff retry loop for transient clipboard lock contention (e.g. CLIPBRD_E_CANT_OPEN).
/// </summary>
public static class StaClipboardHelper
{
    private const int DefaultMaxRetries = 3;
    private const int DefaultRetryDelayMs = 50;

    /// <summary>
    /// Sets text to the Windows clipboard on an STA thread, retrying on clipboard lock contention.
    /// </summary>
    /// <param name="text">The text to place on the clipboard.</param>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="retryDelayMs">Delay between retries in milliseconds.</param>
    /// <returns>True if the clipboard was set successfully; otherwise false.</returns>
    public static bool SetText(string text, int maxRetries = DefaultMaxRetries, int retryDelayMs = DefaultRetryDelayMs)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // If currently running on an STA thread, execute directly
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return TrySetClipboardText(text, maxRetries, retryDelayMs);
        }

        // Otherwise, marshal execution to a dedicated STA worker thread
        bool success = false;
        Exception? exception = null;

        var staThread = new Thread(() =>
        {
            try
            {
                success = TrySetClipboardText(text, maxRetries, retryDelayMs);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        })
        {
            IsBackground = true,
            Name = "ADCE.StaClipboardWorker"
        };

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join(TimeSpan.FromSeconds(3));

        if (exception != null && !success)
        {
            System.Diagnostics.Debug.WriteLine($"[StaClipboardHelper] Warning: Failed to set clipboard: {exception.Message}");
        }

        return success;
    }

    private static bool TrySetClipboardText(string text, int maxRetries, int retryDelayMs)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true, retryTimes: 3, retryDelay: 50);
                return true;
            }
            catch (ExternalException)
            {
                // Clipboard locked by another process (e.g. Windows Clipboard History Win+V, Ditto)
                if (i < maxRetries - 1)
                {
                    Thread.Sleep(retryDelayMs * (i + 1));
                }
            }
            catch (ThreadStateException)
            {
                // Propagate thread state exception if encountered on caller thread
                throw;
            }
            catch (Exception)
            {
                if (i == maxRetries - 1)
                {
                    return false;
                }
                Thread.Sleep(retryDelayMs);
            }
        }

        return false;
    }
}
