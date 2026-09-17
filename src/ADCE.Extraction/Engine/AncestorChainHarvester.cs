// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using ADCE.Extraction.Models;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ADCE.Extraction.Engine;

/// <summary>
/// Harvester responsible exclusively for climbing the Windows UI Automation tree
/// via a single-roundtrip CacheRequest and collecting parent metadata into an AncestorChain.
/// </summary>
public static class AncestorChainHarvester
{
    public static AncestorChain Harvest(
        UIA3Automation automation,
        AutomationElement focusedElement,
        nint rootWindowHwnd,
        int expectedPid,
        string expectedProcessName,
        int maxDepth = 8)
    {
        var pathBuilder = ImmutableArray.CreateBuilder<string>(maxDepth);
        var classBuilder = ImmutableArray.CreateBuilder<string>(maxDepth);
        var nodeBuilder = ImmutableArray.CreateBuilder<AncestorNode>(maxDepth);

        try
        {
            var nativeAutomation = (Interop.UIAutomationClient.IUIAutomation)automation.NativeAutomation;
            var nativeWalker = nativeAutomation.RawViewWalker;

            var cacheRequest = nativeAutomation.CreateCacheRequest();
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.AutomationId.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ClassName.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ControlType.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.Name.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.ProcessId.Id);
            cacheRequest.AddProperty(automation.PropertyLibrary.Element.NativeWindowHandle.Id);
            cacheRequest.TreeScope = Interop.UIAutomationClient.TreeScope.TreeScope_Element;

            var currentNative = ((UIA3FrameworkAutomationElement)focusedElement.FrameworkAutomationElement).NativeElement;

            for (int depth = 0; depth < maxDepth; depth++)
            {
                Interop.UIAutomationClient.IUIAutomationElement? parentNative = null;
                try
                {
                    parentNative = nativeWalker.GetParentElementBuildCache(currentNative, cacheRequest);
                }
                catch (COMException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (parentNative == null) break;

                int parentPid = 0;
                nint parentHwnd = nint.Zero;
                try
                {
                    parentPid = (int)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ProcessId.Id);
                    var rawHwnd = parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.NativeWindowHandle.Id);
                    parentHwnd = rawHwnd switch
                    {
                        int hInt => (nint)(uint)hInt,
                        uint hUint => (nint)hUint,
                        long hLong => (nint)hLong,
                        nint hNint => hNint,
                        _ => nint.Zero
                    };
                }
                catch { }

                if (!UiaExtractionEngine.IsSameOrChildProcess(parentPid, expectedPid, expectedProcessName) ||
                    (rootWindowHwnd != nint.Zero && parentHwnd == rootWindowHwnd))
                {
                    break;
                }

                string autoId = string.Empty;
                string cls = string.Empty;
                string name = string.Empty;
                int cTypeId = 0;
                try
                {
                    autoId = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.AutomationId.Id) ?? string.Empty;
                    cls = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ClassName.Id) ?? string.Empty;
                    name = (string)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.Name.Id) ?? string.Empty;
                    cTypeId = (int)parentNative.GetCachedPropertyValue(automation.PropertyLibrary.Element.ControlType.Id);
                }
                catch { }

                string controlTypeName = GetControlTypeName(cTypeId);

                if (!string.IsNullOrWhiteSpace(cls) && !IsNoiseWrapperClass(cls))
                {
                    classBuilder.Add(cls);
                }

                if (!string.IsNullOrWhiteSpace(autoId))
                {
                    pathBuilder.Add(autoId);
                }

                nodeBuilder.Add(new AncestorNode(
                    Depth: depth,
                    ControlTypeId: cTypeId,
                    ControlType: controlTypeName,
                    Name: name,
                    AutomationId: autoId,
                    ClassName: cls,
                    ProcessId: parentPid,
                    NativeWindowHandle: parentHwnd));

                currentNative = parentNative;
            }
        }
        catch { }

        return new AncestorChain(
            pathBuilder.ToImmutable(),
            classBuilder.ToImmutable(),
            nodeBuilder.ToImmutable());
    }

    public static bool IsNoiseWrapperClass(string cls)
    {
        return cls.Contains("view-lines", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("overflow-guard", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("monaco-scrollable-element", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("split-view-view", StringComparison.OrdinalIgnoreCase) ||
               cls.Contains("split-view-container", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetControlTypeName(int controlTypeId)
    {
        return controlTypeId switch
        {
            50000 => "Button",
            50003 => "ComboBox",
            50004 => "Edit",
            50008 => "ListItem",
            50009 => "Menu",
            50011 => "MenuItem",
            50018 => "Tab",
            50019 => "TabItem",
            50023 => "Tree",
            50024 => "TreeItem",
            50026 => "Group",
            50030 => "Document",
            50032 => "Window",
            50033 => "Pane",
            _ => "Custom"
        };
    }
}
