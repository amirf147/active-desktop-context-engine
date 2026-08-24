// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using ADCE.Core.Enums;
using ADCE.Extraction.Classifiers;
using Xunit;

namespace ADCE.Extraction.Tests;

public class ArchetypeClassifierTests
{
    private readonly ArchetypeClassifier _classifier = ArchetypeClassifier.Default;

    [Theory]
    [InlineData("Chrome_WidgetWin_1", "Antigravity", "Antigravity IDE", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("Chrome_WidgetWin_1", "Code", "Visual Studio Code", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("Chrome_WidgetWin_1", "chrome", "Google Chrome", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("Chrome_WidgetWin_1", "slack", "Slack", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("MozillaWindowClass", "waterfox", "Waterfox", DesktopAppArchetype.Gecko)]
    [InlineData("MozillaWindowClass", "firefox", "Mozilla Firefox", DesktopAppArchetype.Gecko)]
    [InlineData("CabinetWClass", "explorer", "File Explorer", DesktopAppArchetype.WinUI3Xaml)]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS", "WindowsTerminal", "Windows Terminal", DesktopAppArchetype.WinUI3Xaml)]
    [InlineData("SunAwtFrame", "idea64", "IntelliJ IDEA", DesktopAppArchetype.CanvasToolkit)]
    [InlineData("Qt5QWindowIcon", "obs64", "OBS Studio", DesktopAppArchetype.CanvasToolkit)]
    [InlineData("ConsoleWindowClass", "cmd", "Command Prompt", DesktopAppArchetype.ClassicWin32)]
    [InlineData("Notepad", "notepad", "Untitled - Notepad", DesktopAppArchetype.ClassicWin32)]
    [InlineData("RandomCustomClass", "unknown", "Unknown Title", DesktopAppArchetype.Unknown)]
    public void Classify_CategorizesCorrectly(string className, string processName, string title, DesktopAppArchetype expected)
    {
        var result = _classifier.Classify(className, processName, title);
        Assert.Equal(expected, result);
    }
}
