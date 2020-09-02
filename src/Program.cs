// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

namespace CILInsights
{
    /// <summary>
    /// Entry point to the Coyote tool.
    /// </summary>
    internal class Program
    {
        private static int Main(string[] args)
        {
            // Get assembly file or path to assemblies.
            string path = ProcessInputPath(args, out bool isAssemblyFile);

            var assemblyPaths = new HashSet<string>();
            if (isAssemblyFile)
            {
                Console.WriteLine($". Analyzing the '{path}' assembly");
                assemblyPaths.Add(path);
            }
            else
            {
                Console.WriteLine($". Analyzing assemblies in '{path}'");
                assemblyPaths.UnionWith(Directory.GetFiles(path, "*.dll"));
                assemblyPaths.UnionWith(Directory.GetFiles(path, "*.exe"));
            }

            AssemblyAnalyzer.Run(assemblyPaths);

            Console.WriteLine($". Done analyzing");
            return 0;
        }

        private static string ProcessInputPath(string[] args, out bool isAssemblyFile)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine($"Error: please give path to directory containing assemblies to analyze.");
                Environment.Exit(1);
            }

            string path = args[0];
            isAssemblyFile = File.Exists(path);
            if (!(isAssemblyFile || Directory.Exists(path)))
            {
                Console.Error.WriteLine($"Error: '{path}' is not an existing file or directory.");
                Environment.Exit(1);
            }

            string extension = Path.GetExtension(path);
            if (isAssemblyFile && !(extension is ".dll" || extension is ".exe"))
            {
                Console.Error.WriteLine($"Error: '{path}' is not a dll or exe.");
                Environment.Exit(1);
            }

            return path;
        }
    }
}
