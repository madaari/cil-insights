// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using CILAnalyzer.Reports;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CILAnalyzer
{
    /// <summary>
    /// Analyzes an assembly to extract insights.
    /// </summary>
    public class AssemblyAnalyzer
    {
        /// <summary>
        /// Known assembly frequencies.
        /// </summary>
        private static readonly Dictionary<int, ISet<string>> KnownAssemblyFrequencies =
            new Dictionary<int, ISet<string>>();

        /// <summary>
        /// Contains data from the analysis.
        /// </summary>
        private readonly TestProjectInfo Info;

        /// <summary>
        /// The path to the directory containing the assemblies to analyze.
        /// </summary>
        private readonly string AssemblyDir;

        /// <summary>
        /// Set of assemblies to analyze.
        /// </summary>
        private readonly HashSet<string> AssemblyPaths;

        /// <summary>
        /// Set of assemblies that are not allowed to be rewritten.
        /// </summary>
        private readonly HashSet<string> DisallowedAssemblies;

        /// <summary>
        /// List of analysis passes to apply.
        /// </summary>
        private readonly List<AssemblyAnalysis> Passes;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyAnalyzer"/> class.
        /// </summary>
        private AssemblyAnalyzer(string assemblyDir, HashSet<string> assemblyPaths)
        {
            this.Info = new TestProjectInfo();
            this.AssemblyDir = assemblyDir;
            this.AssemblyPaths = assemblyPaths;

            this.DisallowedAssemblies = new HashSet<string>()
            {
                "Microsoft.Coyote.dll",
                "Microsoft.Coyote.Test.dll",
                "System.Private.CoreLib.dll",
                "mscorlib.dll"
            };

            this.Passes = new List<AssemblyAnalysis>()
            {
                 new TestFrameworkAnalysis(this.Info),
                 new ThreadingAnalysis(this.Info)
            };
        }

        /// <summary>
        /// Runs the analyzer.
        /// </summary>
        public static void Run(string assemblyDir, HashSet<string> assemblyPaths)
        {
            var analyzer = new AssemblyAnalyzer(assemblyDir, assemblyPaths);
            analyzer.Analyze();
        }

        /// <summary>
        /// Loads the assembly frequency report, if it exists and is not already loaded.
        /// </summary>
        public static bool TryLoadAssemblyFrequencyReport(string path)
        {
            string filePath = Path.Combine(path, AssemblyFrequencies.FileName);
            if (KnownAssemblyFrequencies.Count is 0 && File.Exists(filePath))
            {
                string jsonReport = File.ReadAllText(filePath);
                var report = JsonSerializer.Deserialize<List<AssemblyFrequencies>>(jsonReport);
                foreach (var kvp in report)
                {
                    KnownAssemblyFrequencies.Add(kvp.Frequency, kvp.Assemblies);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Performs the assembly analysis.
        /// </summary>
        private void Analyze()
        {
            foreach (string assemblyPath in this.AssemblyPaths)
            {
                try
                {
                    this.VisitAssembly(assemblyPath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string reportFile = $"{Path.Combine(this.AssemblyDir, "cil.insights.json")}";
            Console.WriteLine($"... Writing the gathered insights to '{reportFile}'");
            string report = JsonSerializer.Serialize(this.Info, options);
            File.WriteAllText(reportFile, report);
        }

        /// <summary>
        /// Visits the specified assembly definition.
        /// </summary>
        private void VisitAssembly(string assemblyPath)
        {
            string assemblyName = Path.GetFileName(assemblyPath);
            this.Info.Assemblies.Add(assemblyName);

            var allowedAssemblies = new HashSet<string>();
            foreach (int freq in Enumerable.Range(1, 10))
            {
                if (KnownAssemblyFrequencies.TryGetValue(freq, out ISet<string> knownAssemblies))
                {
                    allowedAssemblies.UnionWith(knownAssemblies);
                }
            }

            if (this.DisallowedAssemblies.Contains(assemblyName) || !allowedAssemblies.Contains(assemblyName))
            {
                return;
            }

            var isSymbolFileAvailable = IsSymbolFileAvailable(assemblyPath);
            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters()
            {
                AssemblyResolver = this.GetAssemblyResolver(),
                ReadSymbols = isSymbolFileAvailable
            });

            Console.WriteLine($"... Analyzing the '{assemblyName}' assembly ({assembly.FullName})");
            this.IdentifyRuntimeVersion(assembly);

            foreach (var analysis in this.Passes)
            {
                // Traverse the assembly to apply each transformation pass.
                Debug.WriteLine($"..... Applying the '{analysis.GetType().Name}' analysis");
                foreach (var module in assembly.Modules)
                {
                    VisitModule(module, analysis);
                }
            }

            assembly.Dispose();
        }

        /// <summary>
        /// Visits the specified module definition using the specified analysis.
        /// </summary>
        private static void VisitModule(ModuleDefinition module, AssemblyAnalysis analysis)
        {
            Debug.WriteLine($"....... Module: {module.Name} ({module.FileName})");
            analysis.VisitModule(module);
            foreach (var type in module.GetTypes())
            {
                VisitType(type, analysis);
            }
        }

        /// <summary>
        /// Visits the specified type definition using the specified analysis.
        /// </summary>
        private static void VisitType(TypeDefinition type, AssemblyAnalysis analysis)
        {
            Debug.WriteLine($"......... Type: {type.FullName}");
            analysis.VisitType(type);
            foreach (var field in type.Fields)
            {
                Debug.WriteLine($"........... Field: {field.FullName}");
                analysis.VisitField(field);
            }

            foreach (var method in type.Methods)
            {
                VisitMethod(method, analysis);
            }
        }

        /// <summary>
        /// Visits the specified method definition using the specified analysis.
        /// </summary>
        private static void VisitMethod(MethodDefinition method, AssemblyAnalysis analysis)
        {
            if (method.Body == null)
            {
                return;
            }

            Debug.WriteLine($"........... Method {method.FullName}");
            analysis.VisitMethod(method);

            // Only non-abstract method bodies can be analyzed.
            if (!method.IsAbstract)
            {
                foreach (var variable in method.Body.Variables)
                {
                    analysis.VisitVariable(variable);
                }

                // Visit the method body instructions.
                Instruction instruction = method.Body.Instructions.FirstOrDefault();
                while (instruction != null)
                {
                    analysis.VisitInstruction(instruction);
                    instruction = instruction.Next;
                }
            }
        }

        /// <summary>
        /// Identifies the .NET runtime version of the specified assembly.
        /// </summary>
        private void IdentifyRuntimeVersion(AssemblyDefinition assembly)
        {
            var attribute = GetCustomAttribute(assembly, typeof(TargetFrameworkAttribute));
            if (attribute != null)
            {
                this.Info.RuntimeVersions.Add((string)attribute.ConstructorArguments[0].Value);
            }
        }

        /// <summary>
        /// Returns a new assembly resolver.
        /// </summary>
        private IAssemblyResolver GetAssemblyResolver()
        {
            // TODO: can we reuse it, or do we need a new one for each assembly?
            var assemblyResolver = new DefaultAssemblyResolver();

            // Add the assembly resolution error handler.
            assemblyResolver.ResolveFailure += this.OnResolveAssemblyFailure;
            return assemblyResolver;
        }

        /// <summary>
        /// Returns the first found custom attribute with the specified type, if such an attribute
        /// is applied to the specified assembly, else null.
        /// </summary>
        private static CustomAttribute GetCustomAttribute(AssemblyDefinition assembly, Type attributeType) =>
            assembly.CustomAttributes.FirstOrDefault(
                attr => attr.AttributeType.Namespace == attributeType.Namespace &&
                attr.AttributeType.Name == attributeType.Name);

        /// <summary>
        /// Checks if the symbol file for the specified assembly is available.
        /// </summary>
        private static bool IsSymbolFileAvailable(string assemblyPath) =>
            File.Exists(Path.ChangeExtension(assemblyPath, "pdb"));

        /// <summary>
        /// Handles an assembly resolution error.
        /// </summary>
        private AssemblyDefinition OnResolveAssemblyFailure(object sender, AssemblyNameReference reference)
        {
            Debug.WriteLine("Error resolving assembly: " + reference.FullName);
            return null;
        }
    }
}
