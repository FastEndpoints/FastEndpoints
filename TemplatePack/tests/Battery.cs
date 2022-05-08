using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using ProjectTestRunner.HandlerResults;
using ProjectTestRunner.Handlers;
using ProjectTestRunner.Helpers;
using Xunit;
using Xunit.Abstractions;
using static System.Console;

// ReSharper disable Xunit.XunitTestWithConsoleOutput

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ProjectTestRunner
{
    public class Battery
    {
        private static readonly IReadOnlyDictionary<string, IHandler> HandlerLookup = new Dictionary<string, IHandler>
        {
            {ExecuteHandler.Handler, new ExecuteHandler()},
            {TaskKillHandler.Handler, new TaskKillHandler()},
            {HttpRequestHandler.Handler, new HttpRequestHandler()},
            {FindProcessHandler.Handler, new FindProcessHandler()},
            {FileInspectHandler.Handler, new FileInspectHandler()},
            {DirectoryInspectHandler.Handler, new DirectoryInspectHandler()}
        };

        private static readonly string Creator;
        private static readonly string BasePath;
        private static readonly string OriginalCurrentDir;

        static Battery()
        {
            string assemblyPath = typeof(Battery).GetTypeInfo().Assembly.Location;
            var assemblyUri = new Uri(assemblyPath!, UriKind.Absolute);
            assemblyPath = assemblyUri.LocalPath;
            BasePath = Path.GetDirectoryName(assemblyPath);

            Creator = Environment.GetEnvironmentVariable("CREATION_TEST_RUNNER");

            OriginalCurrentDir = Directory.GetCurrentDirectory();

            if (string.IsNullOrWhiteSpace(Creator)) {
                Creator = "new";
            }

            Proc.Run("dotnet", $"{Creator} --debug:reinit").WaitForExit();
            Proc.Run("dotnet", $"{Creator}").WaitForExit();

            string templateFeedDirectory = FindTemplateFeedDirectory(BasePath);
            Proc.Run("dotnet", $"{Creator} -i \"{templateFeedDirectory}\"").WaitForExit();
        }

        public Battery(ITestOutputHelper outputHelper)
        {
            Console.SetOut(new OutputHelperHelper(outputHelper));
            Console.SetError(new OutputHelperHelper(outputHelper));
        }

        [PrettyTheory]
        [MemberData(nameof(Discover))]
        public void Run(params string[] file)
        {
            WriteLine($"Running tests for {string.Join(Path.DirectorySeparatorChar, file)}");

            WriteLine($"Reset current directory to {OriginalCurrentDir}");
            // reset current directory, because it is being changed later in code
            Directory.SetCurrentDirectory(OriginalCurrentDir);

            string[] allParts = new string[file.Length + 2];
            allParts[0] = BasePath;
            allParts[1] = "TestCases";

            for (int i = 0; i < file.Length; ++i) {
                allParts[i + 2] = file[i];
            }

            WriteLine($"Reading contents of {Path.Combine(allParts)}");

            string contents = File.ReadAllText(Path.Combine(allParts));
            contents = Environment.ExpandEnvironmentVariables(contents);

            WriteLine("Finished reading contents of file");

            var json = JObject.Parse(contents);

            if (json["skip"]?.Value<bool>() ?? false) {
                WriteLine("Test Skipped");
                return;
            }

            string targetPath = Path.Combine(Path.GetTempPath(), "_" + Guid.NewGuid().ToString().Replace("-", ""));

            WriteLine($"target path: {targetPath}");

            try {
                string install = json["install"]?.ToString();
                string command = json["create"].ToString();
                WriteLine("Testing: " + json["name"]);
                var dict = new Dictionary<string, string>
                {
                    {"targetPath", targetPath},
                    {"targetPathName", Path.GetFileName(targetPath)}
                };

                var results = new List<IHandlerResult>();
                IHandlerResult current;
                string message;

                if (!string.IsNullOrWhiteSpace(install)) {
                    WriteLine($"Executing step {results.Count + 1} (install)...");
                    current = Install(Creator, install);

                    message = current.VerificationSuccess
                        ? $"PASS ({current.Duration})"
                        : $"FAIL ({current.Duration}): {current.FailureMessage}";
                    WriteLine($"    {message}");
                    WriteLine(" ");

                    if (!current.VerificationSuccess) {
                        Assert.False(true, current.FailureMessage);
                    }
                }

                WriteLine($"Executing step {results.Count + 1} (create)...");
                current = Create(Creator, install, command, targetPath);

                message = current.VerificationSuccess
                    ? $"PASS ({current.Duration})"
                    : $"FAIL ({current.Duration}): {current.FailureMessage}";
                WriteLine($"    {message}");
                WriteLine(" ");

                if (!current.VerificationSuccess) {
                    Assert.False(true, current.FailureMessage);
                }

                results.Add(current);

                foreach (var entry in ((JArray) json["tasks"]).Children().OfType<JObject>()) {
                    string handlerKey = entry["handler"].ToString();
                    var handler = HandlerLookup[handlerKey];
                    WriteLine($"Executing step {results.Count + 1} ({handler.Summarize(dict, entry)})...");
                    current = handler.Execute(dict, results, entry);
                    message = current.VerificationSuccess
                        ? $"PASS ({current.Duration})"
                        : $"FAIL ({current.Duration}): {current.FailureMessage}";
                    WriteLine($"    {message}");
                    WriteLine(" ");
                    results.Add(current);
                }

                foreach (var result in results) {
                    Assert.False(!result.VerificationSuccess, result.FailureMessage);
                }
            }
            finally {
                for (int i = 0; i < 5; ++i) {
                    try {
                        Directory.Delete(targetPath, true);
                        break;
                    }
                    catch {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        private IHandlerResult Install(string creator, string installPackage)
        {
            var watch = Stopwatch.StartNew();
            try {
                Process install = Proc.Run("dotnet", $"{creator} -i \"{installPackage}\"");
                install.WaitForExit();

                if (install.ExitCode != 0) {
                    return new GenericHandlerResult(watch.Elapsed, false, $"\"{installPackage}\" failed to install");
                }

                return new GenericHandlerResult(watch.Elapsed, true, null);
            }
            finally {
                watch.Stop();
            }
        }

        private static string FindTemplateFeedDirectory(string batteryDirectory)
        {
            var currentDirectory = new DirectoryInfo(batteryDirectory);
            string templateFeed = Path.Combine(currentDirectory.FullName, "templates");

            while (!Directory.Exists(templateFeed)) {
                currentDirectory = currentDirectory.Parent;
                templateFeed = Path.Combine(currentDirectory.FullName, "templates");
            }

            return templateFeed;
        }

        private static IHandlerResult Create(string creator, string installPackage, string command, string targetPath)
        {
            var watch = Stopwatch.StartNew();
            try {
                WriteLine($"Creating target directory {targetPath}");
                var targetDir = Directory.CreateDirectory(targetPath);
                WriteLine($"Directory created? {targetDir.Exists}");
                Process create = Proc.Run("dotnet", $"{creator} {command} -o \"{targetPath}\"");
                create.WaitForExit();

                if (create.ExitCode != 0) {
                    return new GenericHandlerResult(watch.Elapsed, false, $"\"{command}\" failed create");
                }

                Directory.SetCurrentDirectory(targetPath);
                return new GenericHandlerResult(watch.Elapsed, true, null);
            }
            finally {
                watch.Stop();
            }
        }

        [SuppressMessage("ReSharper", "CoVariantArrayConversion")]
        public static IEnumerable<object[]> Discover()
        {
            string basePath = Path.Combine(BasePath, "TestCases");

            foreach (string testCase in Directory.EnumerateFiles(basePath, "*.json", SearchOption.AllDirectories)) {
                WriteLine($"Discovered test case: {testCase}");

                string relPath = testCase.Substring(basePath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string[] file = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                yield return file;
            }
        }
    }
}
