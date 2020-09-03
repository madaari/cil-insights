// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CILAnalyzer.Reports;

namespace CILAnalyzer
{
    /// <summary>
    /// Aggregates, analyzes and reports insights.
    /// </summary>
    public class InsightsEngine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InsightsEngine"/> class.
        /// </summary>
        private InsightsEngine()
        {
        }

        /// <summary>
        /// Runs the engine.
        /// </summary>
        public static void Run(string path)
        {
            var engine = new InsightsEngine();
            engine.Analyze(path);
        }

        /// <summary>
        /// Performs the insights analysis.
        /// </summary>
        private void Analyze(string path)
        {
            int numReports = 0;
            int numTests = 0;
            int minTests = int.MaxValue;
            int maxTests = 0;

            var runtimeVersions = new Dictionary<string, int>();
            var testFrameworks = new Dictionary<string, int>();
            var assemblies = new Dictionary<string, int>();
            var threadingAPIs = new Dictionary<string, int>();

            foreach (TestProjectInfo info in GetTestProjectInfos(path))
            {
                numReports++;
                numTests += info.NumberOfTests;
                if (info.NumberOfTests < minTests)
                {
                    minTests = info.NumberOfTests;
                }

                if (info.NumberOfTests > maxTests)
                {
                    maxTests = info.NumberOfTests;
                }

                // Aggregate .NET runtime versions that are used.
                foreach (var runtimeVersion in info.RuntimeVersions)
                {
                    if (!runtimeVersions.ContainsKey(runtimeVersion))
                    {
                        runtimeVersions.Add(runtimeVersion, 0);
                    }

                    runtimeVersions[runtimeVersion]++;
                }

                // Aggregate unit testing frameworks that are used.
                foreach (var testFramework in info.TestFrameworkTypes)
                {
                    if (!testFrameworks.ContainsKey(testFramework))
                    {
                        testFrameworks.Add(testFramework, 0);
                    }

                    testFrameworks[testFramework]++;
                }

                // Aggregate assemblies that are used.
                foreach (var assembly in info.Assemblies)
                {
                    if (!assemblies.ContainsKey(assembly))
                    {
                        assemblies.Add(assembly, 0);
                    }

                    assemblies[assembly]++;
                }

                // Aggregate threading APIs that are used.
                foreach (var kvp in info.ThreadingAPIs)
                {
                    if (!threadingAPIs.ContainsKey(kvp.Key))
                    {
                        threadingAPIs.Add(kvp.Key, 0);
                    }

                    threadingAPIs[kvp.Key] += kvp.Value;
                }
            }

            Console.WriteLine($"General statistics:");
            Console.WriteLine($" |_ Num reports: {numReports}");
            Console.WriteLine($" |_ Num tests: {numTests}");
            Console.WriteLine($"    |_ Min: {minTests}");
            Console.WriteLine($"    |_ Max: {maxTests}");
            Console.WriteLine($"    |_ Avg: {numTests / numReports}");

            Console.WriteLine($"Version of the .NET runtime:");
            foreach (var runtimeVersion in runtimeVersions)
            {
                Console.WriteLine($" |_ {runtimeVersion.Key}: {runtimeVersion.Value}");
            }

            Console.WriteLine($"Unit test frameworks:");
            foreach (var testFramework in testFrameworks)
            {
                Console.WriteLine($" |_ {testFramework.Key}: {testFramework.Value}");
            }

            ReportAssemblyFrequencies(assemblies, path);
            ReportThreadingAPIFrequencies(threadingAPIs, path);
        }

        /// <summary>
        /// Write the assembly frequencies in a JSON file.
        /// </summary>
        private static void ReportAssemblyFrequencies(Dictionary<string, int> assemblies, string path)
        {
            var assemblyFrequencies = new SortedDictionary<int, HashSet<string>>();
            foreach (var kvp in assemblies)
            {
                if (!assemblyFrequencies.ContainsKey(kvp.Value))
                {
                    assemblyFrequencies.Add(kvp.Value, new HashSet<string>());
                }

                assemblyFrequencies[kvp.Value].Add(kvp.Key);
            }

            string reportFile = $"{Path.Combine(path, TestProjectInfo.AssemblyInsightsFileName)}";
            Console.WriteLine($"... Writing assembly frequencies to '{reportFile}'");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string report = JsonSerializer.Serialize(AssemblyFrequencies.FromDictionary(assemblyFrequencies), options);
            File.WriteAllText(reportFile, report);
        }

        /// <summary>
        /// Write the threading frequencies in a JSON file.
        /// </summary>
        private static void ReportThreadingAPIFrequencies(Dictionary<string, int> threadingAPIs, string path)
        {
            var threadingAPIFrequencies = new SortedDictionary<int, HashSet<string>>();
            foreach (var kvp in threadingAPIs)
            {
                if (!threadingAPIFrequencies.ContainsKey(kvp.Value))
                {
                    threadingAPIFrequencies.Add(kvp.Value, new HashSet<string>());
                }

                threadingAPIFrequencies[kvp.Value].Add(kvp.Key);
            }

            string reportFile = $"{Path.Combine(path, TestProjectInfo.ThreadingAPIInsightsFileName)}";
            Console.WriteLine($"... Writing threading API frequencies to '{reportFile}'");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string report = JsonSerializer.Serialize(AssemblyFrequencies.FromDictionary(threadingAPIFrequencies), options);
            File.WriteAllText(reportFile, report);
        }

        private static IEnumerable<TestProjectInfo> GetTestProjectInfos(string path)
        {
            var reportFiles = new HashSet<string>();
            GetReportFiles(path, reportFiles);

            foreach (var reportFile in reportFiles)
            {
                string jsonReport = File.ReadAllText(reportFile);
                yield return JsonSerializer.Deserialize<TestProjectInfo>(jsonReport);
            }
        }

        private static void GetReportFiles(string path, HashSet<string> files)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                string[] reports = Directory.GetFiles(path, "cil.insights.json");
                if (reports.Length is 0)
                {
                    GetReportFiles(directory, files);
                    continue;
                }

                files.Add(reports[0]);
            }
        }
    }
}
