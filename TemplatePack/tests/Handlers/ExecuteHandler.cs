using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using ProjectTestRunner.HandlerResults;
using ProjectTestRunner.Helpers;

namespace ProjectTestRunner.Handlers
{
    public class ExecuteHandler : IHandler
    {
        public static string Handler => "execute";

        public string HandlerName => Handler;

        public IHandlerResult Execute(IReadOnlyDictionary<string, string> tokens, IReadOnlyList<IHandlerResult> results,
            JObject json)
        {
            var watch = Stopwatch.StartNew();
            try {
                string args = json["args"].ToString();

                foreach (var entry in tokens) {
                    args = args.Replace($"%{entry.Key}%", entry.Value);
                }

                string command = json["command"].ToString();
                var p = Proc.Run(command, args);
                string name = json["name"]?.ToString();
                int? exitTimeoutMs = json["exitTimeout"]?.Value<int>();

                if (json["noExit"]?.Value<bool>() ?? false) {
                    if (p.WaitForExit(exitTimeoutMs ?? 1000)) {
                        return new ExecuteHandlerResult(watch.Elapsed, false, "Process exited unexpectedly",
                            name: name);
                    }

                    return new ExecuteHandlerResult(watch.Elapsed, true, null, p, name);
                }
                else {
                    bool exited = p.WaitForExit(exitTimeoutMs ?? -1);
                    if (!exited) {
                        var result = new ExecuteHandlerResult(watch.Elapsed, false,
                            $"Process did not exit after defined timeout ({exitTimeoutMs} ms)", p, name);
                        result.Kill();
                        return result;
                    }

                    int expectedExitCode = json["exitCode"]?.Value<int>() ?? 0;
                    bool success = expectedExitCode == p.ExitCode;

                    if (!success) {
                        return new ExecuteHandlerResult(watch.Elapsed, false,
                            $"Process exited with code {p.ExitCode} instead of {expectedExitCode}", name: name);
                    }

                    var expectations = json["expectations"]?.Value<JArray>();

                    if (expectations != null) {
                        foreach (var expectation in expectations.Children().OfType<JObject>()) {
                            string assertion = expectation["assertion"]?.Value<string>()?.ToUpperInvariant();
                            string text;
                            StringComparison c;

                            switch (assertion) {
                                case "OUTPUT_CONTAINS":
                                    text = expectation["text"]?.Value<string>();
                                    if (!Enum.TryParse(
                                        expectation["comparison"]?.Value<string>() ?? "OrdinalIgnoreCase", out c)) {
                                        c = StringComparison.OrdinalIgnoreCase;
                                    }

                                    if (p.Output.IndexOf(text, c) < 0) {
                                        return new ExecuteHandlerResult(watch.Elapsed, false,
                                            $"Expected output to contain \"{text}\" ({c}), but it did not", name: name);
                                    }

                                    break;
                                case "OUTPUT_DOES_NOT_CONTAIN":
                                    text = expectation["text"]?.Value<string>();
                                    if (!Enum.TryParse(
                                        expectation["comparison"]?.Value<string>() ?? "OrdinalIgnoreCase", out c)) {
                                        c = StringComparison.OrdinalIgnoreCase;
                                    }

                                    if (p.Output.IndexOf(text, c) > -1) {
                                        return new ExecuteHandlerResult(watch.Elapsed, false,
                                            $"Expected output to NOT contain \"{text}\" ({c}), but it did", name: name);
                                    }

                                    break;
                                default:
                                    return new ExecuteHandlerResult(watch.Elapsed, false,
                                        $"Unkown assertion: {assertion}", name: name);
                            }
                        }
                    }

                    return new ExecuteHandlerResult(watch.Elapsed, true, null, name: name);
                }
            }
            finally {
                watch.Stop();
            }
        }

        public string Summarize(IReadOnlyDictionary<string, string> tokens, JObject json)
        {
            string args = json["args"].ToString();

            foreach (var entry in tokens) {
                args = args.Replace($"%{entry.Key}%", entry.Value);
            }

            string command = json["command"].ToString();

            return $"Execute {command} {args}";
        }
    }
}
