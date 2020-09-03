// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CILInsights
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

            var testFrameworks = new Dictionary<string, int>();

            foreach (Report report in GetReports(path))
            {
                numReports++;
                numTests += report.NumberOfTests;
                if (report.NumberOfTests < minTests)
                {
                    minTests = report.NumberOfTests;
                }

                if (report.NumberOfTests > maxTests)
                {
                    maxTests = report.NumberOfTests;
                }

                // Aggregate unit testing frameworks that are used.
                foreach (var testFramework in report.TestFrameworkTypes)
                {
                    if (!testFrameworks.ContainsKey(testFramework))
                    {
                        testFrameworks.Add(testFramework, 0);
                    }

                    testFrameworks[testFramework]++;
                }
            }

            Console.WriteLine($"Statistics:");
            Console.WriteLine($" |_ Num reports: {numReports}");
            Console.WriteLine($" |_ Num tests: {numTests}");
            Console.WriteLine($" |_ Min tests: {minTests}");
            Console.WriteLine($" |_ Max tests: {maxTests}");
            Console.WriteLine($" |_ Avg tests: {numTests / numReports}");

            Console.WriteLine($"Unit testing frameworks:");
            foreach (var testFramework in testFrameworks)
            {
                Console.WriteLine($" |_ {testFramework.Key}: {testFramework.Value}");
            }
        }

        private static IEnumerable<Report> GetReports(string path)
        {
            var reportFiles = new HashSet<string>();
            GetReportFiles(path, reportFiles);

            foreach (var reportFile in reportFiles)
            {
                string jsonReport = File.ReadAllText(reportFile);
                yield return JsonSerializer.Deserialize<Report>(jsonReport);
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
