// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace CILAnalyzer
{
    /// <summary>
    /// Entry point to the Coyote tool.
    /// </summary>
    internal class Program
    {
        private static string coyote_exe_path;
        private static string mstest_exe_path;

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
            // Replace the relative path with absolute path
            path = Path.GetFullPath(path);

            if(mstest_exe_path == null)
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
                        Environment.Exit(1);
                    }
                }

                foreach (var testAssembly in info.TestAssemblies)
                {
                    TestDllName = testAssembly;
                }

                if (testframework != null && TestDllName != null) break;
            }

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
                    process.StartInfo.FileName = mstest_exe_path+ "\\mstest.exe ";
                    process.StartInfo.Arguments = "/testcontainer:" + TestDllName;
                    command_issued = process.StartInfo.FileName + process.StartInfo.Arguments;
                }

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                string op = process.StandardOutput.ReadToEnd(); /*Do nothing with this. */
                string err = process.StandardError.ReadToEnd();
                int ex_code = process.ExitCode;

                // MSTest will create this directory. Make sure to remove/change this in case of other
                // Test framework
                //System.Diagnostics.Debug.Assert(Directory.Exists(path + "\\TestResults"));
    
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

            if(coyote_exe_path == null)
            {
                Debug.WriteError("Please give path of the coyote installation directory. Use coyote-path=<> option");
                Environment.Exit(1);
            }

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

            Debug.WriteLine($"Rewrting the following directory: {directory}");

            // This directory should contain the rewrite.coyote.json file!
            System.Diagnostics.Debug.Assert(File.Exists(directory+"\\rewrite.coyote.json"));

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
            if (!(cmd is "analyze" || cmd is "analyze-many" || cmd is "stats" || cmd is "rewrite-many" || cmd is "rewrite" || cmd is "test" || cmd is "test-many"))
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

                    if (args[i].Contains("mstest-path"))
                    {
                        string mstest_path = args[i].Split('=')[1];

                        // Make sure that this path exists
                        if (!Directory.Exists(mstest_path))
                        {
                            Debug.WriteError($"Error: {mstest_path} is not an existing directory.");
                            Environment.Exit(1);
                        }

                        // If this directory doesn't contain coyote executable
                        if (!File.Exists(mstest_path + "\\mstest.exe"))
                        {
                            Debug.WriteError($"Error: mstest.exe not found in this path {mstest_path + "\\mstest.exe"}. Please give the absolute " +
                                                    "path of the directory containing mstest.exe file. " +
                                                    "Usage: cil-analyzer.exe <test|test-many> <directory> mstest-path=<path_to_mstest_dir>");

                            Environment.Exit(1);
                        }

                        mstest_exe_path = Path.GetFullPath(mstest_path);
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

                        // If this directory doesn't contain coyote executable
                        if (!File.Exists(coyote_path + "\\coyote.exe"))
                        {
                            Debug.WriteError($"Error: coyote.exe not found in this path {coyote_path + "\\coyote.exe"}. Please give the absolute "+
                                                    "path of the directory containing coyote.exe file. "+
                                                    "Usage: cil-analyzer.exe rewrite-many <directory> coyote-path=<path_to_coyote_dir>");
                            
                            Environment.Exit(1);
                        }

                        coyote_exe_path = coyote_path + "\\coyote.exe";
                    }
                }
            }
        }
    }
}
