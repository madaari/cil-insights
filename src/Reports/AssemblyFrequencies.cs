// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CILAnalyzer.Reports
{
    public class AssemblyFrequencies
    {
        public int Frequency { get; set; }
        public ISet<string> Assemblies { get; set; }

        public AssemblyFrequencies()
        {
            this.Assemblies = new SortedSet<string>();
        }

        internal static List<AssemblyFrequencies> FromDictionary(SortedDictionary<int, HashSet<string>> data)
        {
            var result = new List<AssemblyFrequencies>();
            foreach (var entry in data)
            {
                result.Add(new AssemblyFrequencies()
                {
                    Frequency = entry.Key,
                    Assemblies = entry.Value
                });
            }

            return result;
        }
    }
}
