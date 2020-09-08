// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            (type.Namespace.StartsWith(KnownNamespaces.Threading) ||
            type.Namespace.StartsWith(KnownNamespaces.CompilerServices));

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
