// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CILAnalyzer.Reports
{
    public class RewritingOptions
    {
        internal static string FileName => "rewrite.coyote.json";

        public string AssembliesPath { get; set; }

        public string OutputPath { get; set; }

        public IList<string> Assemblies { get; set; }

        public bool IsRewritingUnitTests { get; set; }

        public RewritingOptions()
        {
            this.Assemblies = new List<string>();
        }
    }
}
