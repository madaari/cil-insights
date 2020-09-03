// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CILAnalyzer.Reports;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CILAnalyzer
{
    internal class ThreadingAnalysis : AssemblyAnalysis
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadingAnalysis"/> class.
        /// </summary>
        internal ThreadingAnalysis(TestProjectInfo Info)
            : base(Info)
        {
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
        internal override void VisitField(FieldDefinition field)
        {
            Debug.WriteLine($"............. [!] field '{field}'");
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

            Debug.WriteLine($"............. [!] return type '{method.ReturnType}'");
        }

        /// <inheritdoc/>
        internal override void VisitVariable(VariableDefinition variable)
        {
            if (this.Method is null)
            {
                return;
            }

            Debug.WriteLine($"............. [!] variable '{variable.VariableType}'");
        }

        /// <inheritdoc/>
        internal override void VisitInstruction(Instruction instruction)
        {
            if (this.Method is null)
            {
                return;
            }

            // Note that the C# compiler is not generating `OpCodes.Calli` instructions:
            // https://docs.microsoft.com/en-us/archive/blogs/shawnfa/calli-is-not-verifiable.
            if (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldflda)
            {
                Debug.WriteLine($"............. [!] {instruction}");
            }
            else if (instruction.OpCode == OpCodes.Initobj)
            {
                this.VisitInitobjInstruction(instruction);
            }
            else if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                instruction.Operand is MethodReference methodReference)
            {
                this.VisitCallInstruction(instruction, methodReference);
            }

            return;
        }

        /// <summary>
        /// Transforms the specified <see cref="OpCodes.Initobj"/> instruction.
        /// </summary>
        /// <returns>The unmodified instruction, or the newly replaced instruction.</returns>
        private void VisitInitobjInstruction(Instruction instruction)
        {
            Debug.WriteLine($"............. [!] {instruction}");
        }

        /// <summary>
        /// Transforms the specified non-generic <see cref="OpCodes.Call"/> or <see cref="OpCodes.Callvirt"/> instruction.
        /// </summary>
        /// <returns>The unmodified instruction, or the newly replaced instruction.</returns>
        private void VisitCallInstruction(Instruction instruction, MethodReference method)
        {
            Debug.WriteLine($"............. [!] {instruction}");

            try
            {
                TypeDefinition resolvedDeclaringType = method.DeclaringType.Resolve();
                if (IsThreadingType(resolvedDeclaringType))
                {
                    string name = null;
                    if (method.DeclaringType is GenericInstanceType genericType)
                    {
                        name = $"{genericType.ElementType.FullName.Split('`')[0]}.{method.Name}";
                    }
                    else
                    {
                        name = $"{resolvedDeclaringType.FullName}.{method.Name}";
                    }

                    if (!this.Info.ThreadingAPIs.ContainsKey(name))
                    {
                        this.Info.ThreadingAPIs.Add(name, 0);
                    }

                    this.Info.ThreadingAPIs[name]++;
                }
            }
            catch (AssemblyResolutionException)
            {
                // Skip this method, we are only interested in methods that can be resolved.
            }
        }

        /// <summary>
        /// Checks if the specified type is a threading type.
        /// </summary>
        private static bool IsThreadingType(TypeDefinition type) => type != null &&
            (type.Module.Name is "System.Private.CoreLib.dll" || type.Module.Name is "mscorlib.dll") &&
            (type.Namespace.StartsWith(typeof(Thread).Namespace));

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
            internal static string TasksName { get; } = typeof(Task).Namespace;
            internal static string ThreadingName { get; } = typeof(ThreadPool).Namespace;
        }
    }
}
