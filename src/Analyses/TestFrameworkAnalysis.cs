// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using CILAnalyzer.Reports;
using Mono.Cecil;

namespace CILAnalyzer
{
    internal class TestFrameworkAnalysis : AssemblyAnalysis
    {
        /// <summary>
        /// Known attributes declaring a unit test.
        /// </summary>
        private readonly Dictionary<string, string> KnownUnitTestFrameworks;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFrameworkAnalysis"/> class.
        /// </summary>
        internal TestFrameworkAnalysis(TestProjectInfo info)
            : base(info)
        {
            this.KnownUnitTestFrameworks = new Dictionary<string, string>();
            this.KnownUnitTestFrameworks.Add("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute", "MSTest");
            this.KnownUnitTestFrameworks.Add("Xunit.FactAttribute", "Xunit");
            this.KnownUnitTestFrameworks.Add("Xunit.TheoryAttribute", "Xunit");
            this.KnownUnitTestFrameworks.Add("NUnit.Framework.TestAttribute", "NUnit");
            this.KnownUnitTestFrameworks.Add("NUnit.Framework.TheoryAttribute", "NUnit");
        }

        /// <inheritdoc/>
        internal override void VisitModule(ModuleDefinition module)
        {
            this.Module = module;
        }

        /// <inheritdoc/>
        internal override void VisitType(TypeDefinition type)
        {
            this.TypeDef = type;
            this.Method = null;
            this.Processor = null;
        }

        /// <inheritdoc/>
        internal override void VisitMethod(MethodDefinition method)
        {
            this.Method = null;

            // Only non-abstract method bodies can be analyzed.
            if (!method.IsAbstract)
            {
                this.Method = method;
                this.Processor = method.Body.GetILProcessor();
            }

            if (method.CustomAttributes.Count > 0)
            {
                // Search for a method with a unit testing framework attribute.
                foreach (var attr in method.CustomAttributes)
                {
                    if (IsTestFrameworkAttribute(attr))
                    {
                        string name = attr.AttributeType.FullName;
                        if (this.KnownUnitTestFrameworks.TryGetValue(name, out string framework))
                        {
                            Debug.WriteLine($"............. [{framework}] '{method.Name}'");
                            this.Info.TestFrameworkTypes.Add(framework);
                            this.Info.TestAssemblies.Add(Path.GetFileName(this.Module.FileName));
                            this.Info.NumberOfTests++;
                        }

                        if (!this.Info.TestFrameworkAPIs.ContainsKey(name))
                        {
                            this.Info.TestFrameworkAPIs.Add(name, 0);
                        }

                        this.Info.TestFrameworkAPIs[name]++;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the specified type is a threading type.
        /// </summary>
        private static bool IsTestFrameworkAttribute(CustomAttribute attr) => attr != null &&
            (attr.AttributeType.FullName.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting") ||
            attr.AttributeType.FullName.StartsWith("Xunit") ||
            attr.AttributeType.FullName.StartsWith("NUnit.Framework"));
    }
}
