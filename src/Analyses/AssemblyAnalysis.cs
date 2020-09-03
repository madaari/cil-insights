// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CILAnalyzer.Reports;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CILAnalyzer
{
    /// <summary>
    /// An abstract interface for analyzing IL using a visitor pattern.
    /// </summary>
    internal abstract class AssemblyAnalysis
    {
        /// <summary>
        /// Contains data from the analysis.
        /// </summary>
        protected readonly TestProjectInfo Info;

        /// <summary>
        /// The current module being transformed.
        /// </summary>
        protected ModuleDefinition Module;

        /// <summary>
        /// The current type being transformed.
        /// </summary>
        protected TypeDefinition TypeDef;

        /// <summary>
        /// The current method being transformed.
        /// </summary>
        protected MethodDefinition Method;

        /// <summary>
        /// A helper class for editing method body.
        /// </summary>
        protected ILProcessor Processor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyAnalysis"/> class.
        /// </summary>
        protected AssemblyAnalysis(TestProjectInfo info)
        {
            this.Info = info;
        }

        /// <summary>
        /// Visits the specified <see cref="ModuleDefinition"/> inside the <see cref="AssemblyDefinition"/>
        /// that was visited by the <see cref="AssemblyAnalyzer"/>.
        /// </summary>
        /// <param name="module">The module definition to visit.</param>
        internal virtual void VisitModule(ModuleDefinition module)
        {
        }

        /// <summary>
        /// Visits the specified <see cref="TypeDefinition"/> inside the <see cref="ModuleDefinition"/>
        /// that was visited by the last <see cref="VisitModule"/>.
        /// </summary>
        /// <param name="type">The type definition to visit.</param>
        internal virtual void VisitType(TypeDefinition type)
        {
        }

        /// <summary>
        /// Visits the specified <see cref="FieldDefinition"/> inside the <see cref="TypeDefinition"/> that was visited
        /// by the last <see cref="VisitType"/>.
        /// </summary>
        /// <param name="field">The field definition to visit.</param>
        internal virtual void VisitField(FieldDefinition field)
        {
        }

        /// <summary>
        /// Visits the specified <see cref="MethodDefinition"/> inside the <see cref="TypeDefinition"/> that was visited
        /// by the last <see cref="VisitType"/>.
        /// </summary>
        /// <param name="method">The method definition to visit.</param>
        internal virtual void VisitMethod(MethodDefinition method)
        {
        }

        /// <summary>
        /// Visits the specified <see cref="VariableDefinition"/> inside the <see cref="MethodDefinition"/> that was visited
        /// by the last <see cref="VisitMethod"/>.
        /// </summary>
        /// <param name="variable">The variable definition to visit.</param>
        internal virtual void VisitVariable(VariableDefinition variable)
        {
        }

        /// <summary>
        /// Visits the specified IL <see cref="Instruction"/> inside the body of the <see cref="MethodDefinition"/>
        /// that was visited by the last <see cref="VisitMethod"/>.
        /// </summary>
        /// <param name="instruction">The instruction to visit.</param>
        internal virtual void VisitInstruction(Instruction instruction)
        {
        }
    }
}
