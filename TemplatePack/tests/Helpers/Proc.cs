using System;
using System.Diagnostics;

namespace ProjectTestRunner.Helpers
{
    public static class Proc
    {
        public static ProcessEx Run(string command, string args)
        {
            Console.WriteLine($"Running proc: {command} {args}");

            var psi = new ProcessStartInfo(command, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var p = Process.Start(psi);
            var wrapper = new ProcessEx(p);
            return wrapper;
        }
    }
}
