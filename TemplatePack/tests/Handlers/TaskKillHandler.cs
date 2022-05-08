using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using ProjectTestRunner.HandlerResults;

namespace ProjectTestRunner.Handlers
{
    public class TaskKillHandler : IHandler
    {
        public static string Handler => "taskkill";

        public string HandlerName => Handler;

        public IHandlerResult Execute(IReadOnlyDictionary<string, string> tokens, IReadOnlyList<IHandlerResult> results,
            JObject json)
        {
            var watch = Stopwatch.StartNew();

            try {
                string targetName = json["name"].ToString();
                var result = results.FirstOrDefault(x => x.Name == targetName);
                if (result is ExecuteHandlerResult xr) {
                    xr.Kill();
                }

                return new GenericHandlerResult(watch.Elapsed, true, null);
            }
            finally {
                watch.Stop();
            }
        }

        public string Summarize(IReadOnlyDictionary<string, string> tokens, JObject json) =>
            "Kill process in named step " + json["name"];
    }
}
