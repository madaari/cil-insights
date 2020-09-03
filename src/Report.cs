// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CILInsights
{
    public class Report
    {
        public int NumberOfTests { get; set; }
        public List<string> TestAssemblies { get; set; }
        public List<string> TestFrameworkTypes { get; set; }

        public Report()
        {
            this.TestAssemblies = new List<string>();
            this.TestFrameworkTypes = new List<string>();
        }
    }
}
