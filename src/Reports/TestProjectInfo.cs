// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CILAnalyzer.Reports
{
    public class TestProjectInfo
    {
        internal static string ThreadingAPIInsightsFileName => "threading.insights.json";
        internal static string TestFrameworkAPIInsightsFileName => "testframework.insights.json";

        public int NumberOfTests { get; set; }
        public ISet<string> RuntimeVersions { get; set; }
        public ISet<string> TestAssemblies { get; set; }
        public ISet<string> Assemblies { get; set; }
        public ISet<string> ThreadingAssemblies { get; set; }
        public ISet<string> UnsupportedAssemblies { get; set; }
        public IDictionary<string, int> ThreadingAPIs { get; set; }
        public ISet<string> TestFrameworkTypes { get; set; }
        public IDictionary<string, int> TestFrameworkAPIs { get; set; }

        public TestProjectInfo()
        {
            this.RuntimeVersions = new SortedSet<string>();
            this.TestAssemblies = new SortedSet<string>();
            this.Assemblies = new SortedSet<string>();
            this.ThreadingAssemblies = new SortedSet<string>();
            this.UnsupportedAssemblies = new SortedSet<string>();
            this.ThreadingAPIs = new SortedDictionary<string, int>();
            this.TestFrameworkTypes = new SortedSet<string>();
            this.TestFrameworkAPIs = new SortedDictionary<string, int>();
        }
    }
}
