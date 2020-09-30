// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CILAnalyzer
{
    /// <summary>
    /// Entry point to the Coyote tool.
    /// </summary>
    internal class Program
    {
        private static string coyote_bin_path;
        private static string vstest_exe_path;

        private static int num_diff = 0;
        public static int total_test_failed = 0;
        private static int total_test_failed_w_rewrite = 0;
        private static int total_test_skipped = 0;
        private static int total_test_skipped_w_rewrite = 0;

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
                case "rewrite":
                    Rewrite(path);
                    break;
                case "rewrite-many":
                    RewriteMany(path);
                    break;
                case "test":
                    RunTestSuite(path);
                    break;
                case "test-many":
                    RunTestSuiteMany(path);
                    break;
                case "test-diff":
                    GenerateDiff(path);
                    break;
            }

            return 0;
        }

        private static void Analyze(string path, bool isAssemblyFile)
        {
            string assemblyDir;
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

            AssemblyAnalyzer.Run(assemblyDir, assemblyPaths);

            Console.WriteLine($". Done analyzing");
        }

        private static void AnalyzeMany(string path)
        {
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


        private static void GenerateDiff(string path)
        {

            path = Path.GetFullPath(path);

            foreach (var testdir in Directory.EnumerateDirectories(path))
            {
                // If this file exits then only call GenerateTestDiff
                if(File.Exists(testdir + "\\cil.insights.json"))
                {
                    GenerateTestDiff(testdir);
                }
            }

            Console.WriteLine($"Directories visited: {num_diff}. Total Test failed without rewrite: {total_test_failed}");
            Console.WriteLine($"Total test failed with rewrite: {total_test_failed_w_rewrite}. Total test skipped: {total_test_skipped}." +
                $"Total test skipped with rewrite: {total_test_skipped_w_rewrite}");
        }

        private static string GetVStestLogFile(string path)
        {
            if (!Directory.Exists(path)) return null;

            if (!File.Exists(path + "\\log.vstest.txt")) return null;

            string retval = File.ReadAllText(path + "\\log.vstest.txt");

            return retval;
        }

        private static int GetFailedTests(string logfile)
        {
            int numfailed = 0;

            foreach (string line in logfile.Split('\n'))
            {
                
                if (line.ToLower().Contains("failed:"))
                {
                    numfailed = Int32.Parse(line.ToLower().Split(':')[1]);
                }
            }

            return numfailed;
        }

        private static int GetSkippedTests(string logfile)
        {
            int numskipped = 0;

            foreach (string line in logfile.Split('\n'))
            {
                if (line.ToLower().Contains("skipped:"))
                {
                    numskipped = Int32.Parse(line.ToLower().Split(':')[1]);
                }
            }

            return numskipped;
        }

        private static void GenerateTestDiff(string testdir)
        {
            bool has_rewritten = false;
            foreach (var dir in Directory.EnumerateDirectories(testdir))
            {
                if (dir.ToLower().Contains("rewritten"))
                {
                    has_rewritten = true;
                    break;
                }
            }

            if (has_rewritten == false) return;

            string logfile_orignal = GetVStestLogFile(testdir);
            string logfile_rewrite = GetVStestLogFile(testdir + "\\rewritten");

            if (logfile_orignal == null || logfile_rewrite == null) return;

            int num_failed_test = GetFailedTests(logfile_orignal);
            int num_failed_test_rewrite = GetFailedTests(logfile_rewrite);

            int num_skipped_test = GetSkippedTests(logfile_orignal);
            int num_skipped_test_rewrite = GetSkippedTests(logfile_rewrite);

            if (num_failed_test != num_failed_test_rewrite || num_skipped_test != num_skipped_test_rewrite)
            {
                Debug.WriteError($"Mismatch found! in dir: {testdir}");
            }
            else
            {
                System.Console.WriteLine($"Verified testdir: {testdir}");
            }

            num_diff++;
            total_test_failed += num_failed_test;
            total_test_failed_w_rewrite += num_failed_test_rewrite;
            total_test_skipped += num_skipped_test;
            total_test_skipped_w_rewrite += num_skipped_test_rewrite;
        }

        private static void RunTestSuiteMany(string path)
        {
            path = Path.GetFullPath(path);

            foreach(var directory in Directory.EnumerateDirectories(path))
            {
                if(!File.Exists(directory + "\\cil.insights.json"))
                {
                    RunTestSuiteMany(directory);
                    continue;
                }

                // This should be called on both the test case directory and the Rewritten sub-directory with it
                RunTestSuite(directory);

                RunTestSuiteMany(directory);
            }
        }

        /// <summary>
        ///  Run testsuite given in the path. The path should contain the test DLL
        /// </summary>
        /// <param name="path"></param>
        private static void RunTestSuite(string path)
        {
            Console.WriteLine($"Running test case in dir: {path}");

            // Replace the relative path with absolute path
            path = Path.GetFullPath(path);

            if (vstest_exe_path == null)
            {
                Debug.WriteError("Please give the path to mstest.exe file using mstest-path=<> option");
                Environment.Exit(1);
            }

            if (!File.Exists(path + "\\cil.insights.json"))
            {
                Debug.WriteError($"This directory: {path} does not contain cil.insights.json file!");
                Environment.Exit(1);
            }

            string testframework = null;
            string TestDllName = null;

            // Get the cil.insights.json file and deserialize it
            foreach (var (info, file) in InsightsEngine.GetTestProjectInfos(path))
            {
                foreach (var testFramework in info.TestFrameworkTypes)
                {
                    if (testFramework.Contains("MSTest"))
                    {
                        testframework = "MSTest";
                        break;
                    } else
                    {
                        Debug.WriteError($"Unsupported test framework: {testFramework}");
                        return;
                    }
                }

                foreach (var testAssembly in info.TestAssemblies)
                {
                    TestDllName = testAssembly;
                }

                if (testframework != null && TestDllName != null) break;
            }

            if (TestDllName == null || testframework == null) return;

            // Check if the Test DLL exists or not
            if(!File.Exists(path + "\\" + TestDllName))
            {
                Debug.WriteError($"Can not find the following file: {path + "\\" + TestDllName}");
                Environment.Exit(1);
            }

            TestDllName = path + "\\" + TestDllName;

            // Used for logging
            string command_issued = "";

            // We are now ready to spawn a new process
            try
            {
                System.Diagnostics.Process process = new Process();

                if (testframework == "MSTest")
                {
                    process.StartInfo.FileName = vstest_exe_path+ " ";
                    process.StartInfo.Arguments = "/platform:x64 " + TestDllName;
                    command_issued = process.StartInfo.FileName + process.StartInfo.Arguments;
                } else
                {
                    // If it is not MSTest, leave it
                    return;
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                string op = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                int ex_code = process.ExitCode;

                // MSTest will create this directory. Make sure to remove/change this in case of other
                // Test framework
                //System.Diagnostics.Debug.Assert(Directory.Exists(path + "\\TestResults"));

                File.WriteAllText(path + "\\log.vstest.txt", op);

                if (err.Length > 1)
                {
                    Debug.WriteError(err);
                }

                if (ex_code != 0)
                {
                    Debug.WriteError(op);
                    Debug.WriteError($"Error: something went wrong while running the test framework: Perhaps some test failed. Exit code = {ex_code} Check: {path + "\\TestResults"}");
                    Debug.WriteError("Error: Issued the following command: " +
                                        $"{command_issued}");
                }

            }
            catch (Exception e)
            {
                Debug.WriteError(e.ToString());
                Debug.WriteError("Error: Unable to start a new process. Trying to issue the following command: " +
                                        $"{command_issued}");

                // Should we terminate the program? Perhaps, no. Let it finish rewriting other directories.
                return;
            }

            Console.WriteLine($"...Successfully ran test suite in the directory: {path}");
        }

        /// <summary>
        /// Recursively calls 'coyote rewrite' on all the directories that consists of rewrite.coyote.json file.
        /// </summary>
        /// <param name="path"></param>

        private static void RewriteMany(string path)
        {
            Debug.WriteLine($"In rewrite many: {path}");

            AssemblyAnalyzer.TryLoadAssemblyFrequencyReport(path);
            foreach (var directory in Directory.GetDirectories(path))
            {
                // Presence of log.rewrite.coyote.txt file indicates that this is 'rewritten' directory and we have to
                // delete it.
                if (directory.Contains("rewritten"))
                {
                    Directory.Delete(directory);
                    Debug.WriteLine($"Deleting directory: {directory}");

                    continue;
                }
                
                if (!File.Exists(directory + "\\rewrite.coyote.json"))
                {
                    RewriteMany(directory);
                    continue;
                }

                Rewrite(directory);
            }
        }

        /// <summary>
        /// Calls 'coyote-rewrite' on the directory passed as the paramerter. This directory should contain
        /// rewrite.coyote.json file.
        /// </summary>
        /// <param name="directory"></param>

        private static void Rewrite(string directory)
        {

            if (directory.Contains("rewritten")) return;

            Debug.WriteLine($"Rewrting the following directory: {directory}");

            if (coyote_bin_path == null)
            {
                Debug.WriteError("Please give path of the coyote installation, bin directory. Use coyote-path=<> option");
                Environment.Exit(1);
            }

            // This directory should contain the rewrite.coyote.json file!
            System.Diagnostics.Debug.Assert(File.Exists(directory+"\\rewrite.coyote.json"));

            string netruntime = "";
            string version = "";

            // Try open this file! and load the netcore version it use!

            foreach (var (info, file) in InsightsEngine.GetTestProjectInfos(directory))
            {
                // Aggregate .NET runtime versions that are used.
                foreach (var runtimeVersion in info.RuntimeVersions)
                {
                    string[] ar = runtimeVersion.Split(',');
                    netruntime = ar[0];
                    version = ar[1];
                    break;
                }
            }

            string coyote_build_version = "";

            if (netruntime.ToLower().Contains(".netframework"))
            {
                coyote_build_version = "net48";
            }

            if (netruntime.ToLower().Contains("netcoreapp"))
            {
                coyote_build_version = "netcoreapp3.1";
            }

            if (netruntime.ToLower().Contains("netstandard"))
            {
                coyote_build_version = "netstandard2.0";
            }

            System.Diagnostics.Debug.Assert(coyote_build_version != "");

            string coyote_exe_path = coyote_bin_path + "\\" + coyote_build_version + "\\coyote.exe";

            if(!File.Exists(coyote_exe_path))
            {
                Debug.WriteError($"coyote.exe not found in the given path: {coyote_exe_path}");
                Environment.Exit(1);
            }

            try
            {
                System.Diagnostics.Process process = new Process();
                process.StartInfo.FileName = coyote_exe_path;
                process.StartInfo.Arguments = "rewrite " + directory + "\\rewrite.coyote.json";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                string op = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                int ex_code = process.ExitCode;

                // Wait for the process to finish and read its output. Store the output in a file
                // named log.rewrite.coyote.txt.
                System.Diagnostics.Debug.Assert(Directory.Exists(directory + "\\rewritten"));
                File.WriteAllText(directory + "\\rewritten\\log.rewrite.coyote.txt", op+"\n"+err);

                if(err.Length > 1)
                {
                    Debug.WriteError(err);
                }

                if(ex_code != 0)
                {
                    Debug.WriteError($"Error: something went wrong in the child process: Exit code = {ex_code} Check: {directory + "\\rewritten\\log.rewrite.coyote.txt"}");
                }

            }
            catch(Exception e)
            {
                Debug.WriteError(e.ToString());
                Debug.WriteError("Error: Unable to start a new process. Trying to issue the following command: "+
                                        $" {coyote_exe_path} rewrite {directory}\\rewrite.coyote.json");
                
                // Should we terminate the program? Perhaps, no. Let it finish rewriting other directories.
                return;
            }
        }

        private static void ProcessArguments(string[] args, out string cmd, out string path, out bool isAssemblyFile)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Error: please give tool command [analyze, analyze-many, stats, rewrite-many, test, test-many, rewrite] and a directory path.");
                Environment.Exit(1);
            }

            cmd = args[0];
            if (!(cmd is "analyze" || cmd is "analyze-many" || cmd is "stats" || cmd is "rewrite-many" || cmd is "rewrite" || cmd is "test" || cmd is "test-many" || cmd is "test-diff"))
            {
                Console.Error.WriteLine("Error: unknown tool command, please use [analyze, analyze-many, stats, rewrite, rewrite-many, test, test-many].");
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

            if(args.Length > 2)
            {
                for(int i=2; i < args.Length; i++)
                {
                    if(args[i] is "debug")
                    {
                        Debug.IsEnabled = true;
                        continue;
                    }

                    if (args[i].Contains("vstest-path"))
                    {
                        string vstest_path = args[i].Split('=')[1];

                        // Make sure that this path exists
                        if (!Directory.Exists(vstest_path))
                        {
                            Debug.WriteError($"Error: {vstest_path} is not an existing directory.");
                            Environment.Exit(1);
                        }

                        // If this directory doesn't contain coyote executable
                        if (!File.Exists(vstest_path + "\\vstest.console.exe"))
                        {
                            Debug.WriteError($"Error: vstest.console.exe not found in this path {vstest_path + "\\vstest.console.exe"}. Please give the absolute " +
                                                    "path of the directory containing mstest.exe file. " +
                                                    "Usage: cil-analyzer.exe <test|test-many> <directory> vstest-path=<path_to_vstest_dir>");

                            Environment.Exit(1);
                        }

                        vstest_exe_path = Path.GetFullPath(vstest_path) + "\\vstest.console.exe";
                    }

                    if (args[i].Contains("coyote-path="))
                    {
                        string coyote_path = args[i].Split('=')[1];

                        // Make sure that this path exists
                        if (!Directory.Exists(coyote_path))
                        {
                            Debug.WriteError($"Error: {coyote_path} is not an existing directory.");
                            Environment.Exit(1);
                        }

                        /*
                        // If this directory doesn't contain coyote executable
                        if (!File.Exists(coyote_path + "\\coyote.exe"))
                        {
                            Debug.WriteError($"Error: coyote.exe not found in this path {coyote_path + "\\coyote.exe"}. Please give the absolute "+
                                                    "path of the directory containing coyote.exe file. "+
                                                    "Usage: cil-analyzer.exe rewrite-many <directory> coyote-path=<path_to_coyote_dir>");
                            
                            Environment.Exit(1);
                        }
                        */

                        coyote_bin_path = coyote_path;
                    }
                }
            }
        }
    }
}
