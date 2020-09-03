// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
                Debug.WriteLine($"............. [!] '{method.Name}'");
                foreach (var attr in method.CustomAttributes)
                {
                    Debug.WriteLine($"............... attr: '{attr.AttributeType.FullName}'");
                    if (this.KnownUnitTestFrameworks.TryGetValue(attr.AttributeType.FullName, out string framework))
                    {
                        Debug.WriteLine($"............. [{framework}] '{method.Name}'");
                        this.Info.TestFrameworkTypes.Add(framework);
                        this.Info.TestAssemblies.Add(this.Module.FileName);
                        this.Info.NumberOfTests++;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the first found custom attribute with the specified type, if such an attribute
        /// is applied to the specified assembly, else null.
        /// </summary>
        private static CustomAttribute GetCustomAttribute(MethodDefinition method, Type attributeType) =>
            method.CustomAttributes.FirstOrDefault(
                attr => attr.AttributeType.Namespace == attributeType.Namespace &&
                attr.AttributeType.Name == attributeType.Name);

        /// <summary>
        /// Checks if the specified type is the <see cref="Task"/> type.
        /// </summary>
        private static bool IsSystemTaskType(TypeReference type) => type.Namespace == KnownNamespaces.SystemTasksName &&
            (type.Name == typeof(Task).Name || type.Name.StartsWith("Task`"));

        /// <summary>
        /// Checks if the <see cref="Task"/> method with the specified name is supported.
        /// </summary>
        private static bool IsSupportedTaskMethod(string name) =>
            name == "get_Factory" ||
            name == "get_Result" ||
            name == nameof(Task.Run) ||
            name == nameof(Task.Delay) ||
            name == nameof(Task.WhenAll) ||
            name == nameof(Task.WhenAny) ||
            name == nameof(Task.WaitAll) ||
            name == nameof(Task.WaitAny) ||
            name == nameof(Task.Wait) ||
            name == nameof(Task.Yield) ||
            name == nameof(Task.GetAwaiter);

        /// <summary>
        /// Cache of known <see cref="SystemCompiler"/> type names.
        /// </summary>
        private static class KnownSystemTypes
        {
            internal static string TaskFullName { get; } = typeof(Task).FullName;
            internal static string GenericTaskFullName { get; } = typeof(Task<>).FullName;
            internal static string AsyncTaskMethodBuilderFullName { get; } = typeof(AsyncTaskMethodBuilder).FullName;
            internal static string GenericAsyncTaskMethodBuilderName { get; } = typeof(AsyncTaskMethodBuilder<>).Name;
            internal static string GenericAsyncTaskMethodBuilderFullName { get; } = typeof(AsyncTaskMethodBuilder<>).FullName;
            internal static string TaskAwaiterFullName { get; } = typeof(TaskAwaiter).FullName;
            internal static string GenericTaskAwaiterName { get; } = typeof(TaskAwaiter<>).Name;
            internal static string GenericTaskAwaiterFullName { get; } = typeof(TaskAwaiter<>).FullName;
            internal static string YieldAwaitableFullName { get; } = typeof(YieldAwaitable).FullName;
            internal static string YieldAwaiterFullName { get; } = typeof(YieldAwaitable).FullName + "/YieldAwaiter";
            internal static string TaskExtensionsFullName { get; } = typeof(TaskExtensions).FullName;
            internal static string TaskFactoryFullName { get; } = typeof(TaskFactory).FullName;
            internal static string ThreadPoolFullName { get; } = typeof(ThreadPool).FullName;
        }

        /// <summary>
        /// Cache of known namespace names.
        /// </summary>
        private static class KnownNamespaces
        {
            internal static string SystemTasksName { get; } = typeof(Task).Namespace;
        }
    }
}
