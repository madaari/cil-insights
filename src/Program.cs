// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CILAnalyzer
{
    /// <summary>
    /// Entry point to the Coyote tool.
    /// </summary>
    internal class Program
    {
        private static int Main(string[] args)
        {
            ProcessArguments(args, out string cmd, out string path, out bool isAssemblyFile);

            switch (cmd.ToLower())
            {
                case "analyze":
                    Analyze(path, isAssemblyFile);
                    break;
                case "analyze-many":
                    AnalyzeMany(path);
                    break;
                case "stats":
                    GatherStats(path);
                    break;
            }

            return 0;
        }

        private static void Analyze(string path, bool isAssemblyFile)
        {
            string assemblyDir = null;
            var assemblyPaths = new HashSet<string>();
            if (isAssemblyFile)
            {
                Console.WriteLine($". Analyzing the '{path}' assembly");
                assemblyPaths.Add(path);
                assemblyDir = Path.GetDirectoryName(path);
            }
            else
            {
                Console.WriteLine($". Analyzing assemblies in '{path}'");
                assemblyPaths.UnionWith(Directory.GetFiles(path, "*.dll"));
                assemblyPaths.UnionWith(Directory.GetFiles(path, "*.exe"));
                assemblyDir = path;
            }

            AssemblyAnalyzer.TryLoadAssemblyFrequencyReport(path);
            AssemblyAnalyzer.Run(assemblyDir, assemblyPaths);

            Console.WriteLine($". Done analyzing");
        }

        private static void AnalyzeMany(string path)
        {
            AssemblyAnalyzer.TryLoadAssemblyFrequencyReport(path);
            foreach (var directory in Directory.GetDirectories(path))
            {
                if (!Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".dll") || file.EndsWith(".exe")))
                {
                    AnalyzeMany(directory);
                    continue;
                }

                Analyze(directory, false);
            }
        }

        private static void GatherStats(string path)
        {
            Console.WriteLine($". Analyzing reports in '{path}'");
            InsightsEngine.Run(path);
            Console.WriteLine($". Done analyzing");
        }

        private static void ProcessArguments(string[] args, out string cmd, out string path, out bool isAssemblyFile)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Error: please give tool command [analyze, analyze-many, stats] and a directory path.");
                Environment.Exit(1);
            }

            cmd = args[0];
            if (!(cmd is "analyze" || cmd is "analyze-many" || cmd is "stats"))
            {
                Console.Error.WriteLine("Error: unknown tool command, please use [analyze, analyze-many, stats].");
                Environment.Exit(1);
            }

            path = args[1];
            if (cmd is "analyze")
            {
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
            }
            else
            {
                if (!Directory.Exists(path))
                {
                    Console.Error.WriteLine($"Error: '{path}' is not an existing directory.");
                    Environment.Exit(1);
                }

                isAssemblyFile = false;
            }

            Debug.IsEnabled = args.Length > 2 && args[2] is "debug";
        }
    }
}
