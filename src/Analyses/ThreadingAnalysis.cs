// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
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
        internal override void VisitMethod(MethodDefinition method)
        {
            this.Method = null;

            // Only non-abstract method bodies can be analyzed.
            if (!method.IsAbstract)
            {
                this.Method = method;
                this.Processor = method.Body.GetILProcessor();
            }
        }

        /// <inheritdoc/>
        internal override void VisitInstruction(Instruction instruction)
        {
            if (this.Method is null)
            {
                return;
            }

            if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                instruction.Operand is MethodReference methodReference)
            {
                this.VisitCallInstruction(instruction, methodReference);
            }

            return;
        }

        /// <summary>
        /// Transforms the specified non-generic <see cref="OpCodes.Call"/> or <see cref="OpCodes.Callvirt"/> instruction.
        /// </summary>
        /// <returns>The unmodified instruction, or the newly replaced instruction.</returns>
        private void VisitCallInstruction(Instruction instruction, MethodReference method)
        {
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
                    this.Info.ThreadingAssemblies.Add(Path.GetFileName(this.Module.FileName));

                    if (!(resolvedDeclaringType.Namespace.StartsWith("System.Threading.Tasks") ||
                        resolvedDeclaringType.Namespace.StartsWith("System.Runtime.CompilerServices") ||
                        resolvedDeclaringType.FullName.StartsWith("System.Threading.Monitor") ||
                        resolvedDeclaringType.FullName.StartsWith("System.Threading.CancellationTokenSource")))
                    {
                        this.Info.UnsupportedAssemblies.Add(Path.GetFileName(this.Module.FileName));
                    }
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
        private static bool IsThreadingType(TypeDefinition type)
        {
            if (type is null)
            {
                return false;
            }

            string module = Path.GetFileName(type.Module.FileName);
            return (module is "System.Private.CoreLib.dll" || module is "mscorlib.dll") &&
                (type.Namespace.StartsWith(KnownNamespaces.Threading) ||
                type.Namespace.StartsWith(KnownNamespaces.CompilerServices));
        }

        /// <summary>
        /// Cache of known namespace names.
        /// </summary>
        private static class KnownNamespaces
        {
            internal static string CompilerServices { get; } = typeof(System.Runtime.CompilerServices.TaskAwaiter).Namespace;
            internal static string Tasks{ get; } = typeof(System.Threading.Tasks.Task).Namespace;
            internal static string Threading { get; } = typeof(System.Threading.Thread).Namespace;
        }
    }
}
