// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;

namespace ADCE.Spikes.Models;

public record TargetWindow(IntPtr Hwnd, string Title, string ClassName, uint Pid);
public record TabInfo(int Index, string Title, bool IsActive);
