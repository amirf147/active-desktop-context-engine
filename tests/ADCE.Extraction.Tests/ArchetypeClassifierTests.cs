// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

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
    [InlineData("Chrome_WidgetWin_1", "cursor", "Cursor", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("Chrome_WidgetWin_1", "arbitrary_custom_app", "My Custom App", DesktopAppArchetype.ChromiumElectron)]
    [InlineData("MozillaWindowClass", "waterfox", "Waterfox", DesktopAppArchetype.Gecko)]
    [InlineData("MozillaWindowClass", "firefox", "Mozilla Firefox", DesktopAppArchetype.Gecko)]
    [InlineData("MozillaWindowClass", "zen", "Zen Browser", DesktopAppArchetype.Gecko)]
    [InlineData("MozillaWindowClass", "custom_gecko_fork", "Custom Gecko", DesktopAppArchetype.Gecko)]
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

    [Fact]
    public void Classify_DoesNotClassifyBasedOnProcessNameAlone()
    {
        // When window class is unknown, having a process name like 'code' or 'firefox' must NOT classify
        var resultChromium = _classifier.Classify("UnknownClass", "code", "Some Window");
        Assert.Equal(DesktopAppArchetype.Unknown, resultChromium);

        var resultGecko = _classifier.Classify("UnknownClass", "firefox", "Some Window");
        Assert.Equal(DesktopAppArchetype.Unknown, resultGecko);

        var resultWaterfox = _classifier.Classify("UnknownClass", "waterfox", "Some Window");
        Assert.Equal(DesktopAppArchetype.Unknown, resultWaterfox);

        var resultSlack = _classifier.Classify("UnknownClass", "slack", "Some Window");
        Assert.Equal(DesktopAppArchetype.Unknown, resultSlack);
    }
}
