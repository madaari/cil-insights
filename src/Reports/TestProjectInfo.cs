// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CILAnalyzer.Reports
{
    public class TestProjectInfo
    {
        internal static string AssemblyInsightsFileName => "assemblyfreq.insights.json";
        internal static string ThreadingAPIInsightsFileName => "threading.insights.json";

        public int NumberOfTests { get; set; }
        public ISet<string> RuntimeVersions { get; set; }
        public ISet<string> TestFrameworkTypes { get; set; }
        public ISet<string> TestAssemblies { get; set; }
        public ISet<string> Assemblies { get; set; }
        public IDictionary<string, int> ThreadingAPIs { get; set; }

        public TestProjectInfo()
        {
            this.RuntimeVersions = new SortedSet<string>();
            this.TestFrameworkTypes = new SortedSet<string>();
            this.TestAssemblies = new SortedSet<string>();
            this.Assemblies = new SortedSet<string>();
            this.ThreadingAPIs = new SortedDictionary<string, int>();
        }
    }
}
