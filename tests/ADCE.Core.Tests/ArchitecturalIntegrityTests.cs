// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 Amir Farhadi

using System;
using System.Linq;
using System.Reflection;
using ADCE.Core.Models;
using Xunit;

namespace ADCE.Core.Tests;

public class ArchitecturalIntegrityTests
{
    [Fact]
    public void AdceCore_HasZeroNonBclDependencies()
    {
        var coreAssembly = typeof(DesktopContextSnapshot).Assembly;
        var referencedAssemblies = coreAssembly.GetReferencedAssemblies();

        foreach (var assemblyName in referencedAssemblies)
        {
            string name = assemblyName.Name ?? "";
            bool isBcl = name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                         name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);

            Assert.True(isBcl, $"ADCE.Core must not reference non-BCL assembly: '{name}'.");
        }
    }

    [Fact]
    public void AdceCore_Models_DoNotHaveMutablePublicSetters()
    {
        var coreAssembly = typeof(DesktopContextSnapshot).Assembly;
        var modelTypes = coreAssembly.GetTypes()
            .Where(t => t.Namespace == "ADCE.Core.Models" && t.IsClass && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(modelTypes);

        foreach (var type in modelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var setMethod = prop.GetSetMethod();
                if (setMethod != null)
                {
                    // In C#, init-only setters have a return parameter with modreq IsExternalInit
                    bool isInitOnly = setMethod.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Any(mod => mod.FullName == "System.Runtime.CompilerServices.IsExternalInit");

                    Assert.True(isInitOnly,
                        $"Model property {type.Name}.{prop.Name} has a mutable public setter. All model properties must be init-only.");
                }
            }
        }
    }
}
