// Procedural Pixel Creatures - minimal deterministic test runner shared by the console harness and
// the Godot --self-test mode. Produces a structured JSON report.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace PixelCreatures.Tests
{
    public sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message) { }
    }

    public sealed class TestResult
    {
        public string Name = string.Empty;
        public bool Passed;
        public string Message = string.Empty;
        public double Milliseconds;
        public readonly List<string> Notes = new List<string>();
        public readonly Dictionary<string, double> Metrics = new Dictionary<string, double>();
    }

    public sealed class TestContext
    {
        internal TestResult Result = new TestResult();

        public void True(bool condition, string message)
        {
            if (!condition) throw new TestFailure(message);
        }

        public void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestFailure($"{what}: expected {expected}, got {actual}");
        }

        public void Near(double expected, double actual, double tol, string what)
        {
            if (!(Math.Abs(expected - actual) <= tol))
                throw new TestFailure($"{what}: expected {expected.ToString("R", CultureInfo.InvariantCulture)} ±{tol}, got {actual.ToString("R", CultureInfo.InvariantCulture)}");
        }

        public void Note(string note) => Result.Notes.Add(note);
        public void Metric(string key, double value) => Result.Metrics[key] = value;
    }

    public sealed class TestRunner
    {
        public readonly List<TestResult> Results = new List<TestResult>();
        public string Filter = string.Empty;
        public Action<string>? Log;

        public void Run(string name, Action<TestContext> test)
        {
            if (!string.IsNullOrEmpty(Filter) && !name.Contains(Filter, StringComparison.OrdinalIgnoreCase)) return;
            var ctx = new TestContext();
            ctx.Result.Name = name;
            var sw = Stopwatch.StartNew();
            try
            {
                test(ctx);
                ctx.Result.Passed = true;
            }
            catch (TestFailure f)
            {
                ctx.Result.Passed = false;
                ctx.Result.Message = f.Message;
            }
            catch (Exception e)
            {
                ctx.Result.Passed = false;
                ctx.Result.Message = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
            sw.Stop();
            ctx.Result.Milliseconds = sw.Elapsed.TotalMilliseconds;
            Results.Add(ctx.Result);
            Log?.Invoke($"[{(ctx.Result.Passed ? "PASS" : "FAIL")}] {name} ({ctx.Result.Milliseconds:0} ms){(ctx.Result.Passed ? "" : " -> " + ctx.Result.Message)}");
        }

        /// <summary>Async variant for engine tests that have to wait for frames.</summary>
        public async System.Threading.Tasks.Task RunAsync(string name, Func<TestContext, System.Threading.Tasks.Task> test)
        {
            if (!string.IsNullOrEmpty(Filter) && !name.Contains(Filter, StringComparison.OrdinalIgnoreCase)) return;
            var ctx = new TestContext();
            ctx.Result.Name = name;
            var sw = Stopwatch.StartNew();
            try
            {
                await test(ctx);
                ctx.Result.Passed = true;
            }
            catch (TestFailure f)
            {
                ctx.Result.Passed = false;
                ctx.Result.Message = f.Message;
            }
            catch (Exception e)
            {
                ctx.Result.Passed = false;
                ctx.Result.Message = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
            }
            sw.Stop();
            ctx.Result.Milliseconds = sw.Elapsed.TotalMilliseconds;
            Results.Add(ctx.Result);
            Log?.Invoke($"[{(ctx.Result.Passed ? "PASS" : "FAIL")}] {name} ({ctx.Result.Milliseconds:0} ms){(ctx.Result.Passed ? "" : " -> " + ctx.Result.Message)}");
        }

        public int Failed
        {
            get
            {
                int n = 0;
                foreach (var r in Results) if (!r.Passed) n++;
                return n;
            }
        }

        public string ReportJson(string environment)
        {
            var arr = new JsonArray();
            foreach (var r in Results)
            {
                var o = new JsonObject
                {
                    ["name"] = r.Name,
                    ["passed"] = r.Passed,
                    ["ms"] = Math.Round(r.Milliseconds, 2),
                };
                if (!r.Passed) o["message"] = r.Message;
                if (r.Notes.Count > 0)
                {
                    var notes = new JsonArray();
                    foreach (var n in r.Notes) notes.Add(JsonValue.Create(n));
                    o["notes"] = notes;
                }
                if (r.Metrics.Count > 0)
                {
                    var m = new JsonObject();
                    foreach (var kv in r.Metrics) m[kv.Key] = Math.Round(kv.Value, 4);
                    o["metrics"] = m;
                }
                arr.Add(o);
            }
            var root = new JsonObject
            {
                ["suite"] = "procedural-pixel-creatures",
                ["environment"] = environment,
                ["frameworkVersion"] = PixelCreatures.Core.CreatureFramework.FrameworkVersion,
                ["generatorVersion"] = PixelCreatures.Core.CreatureFramework.GeneratorVersion,
                ["total"] = Results.Count,
                ["failed"] = Failed,
                ["results"] = arr,
            };
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"{Results.Count - Failed}/{Results.Count} tests passed");
            if (Failed > 0)
            {
                sb.Append(". Failed:");
                foreach (var r in Results) if (!r.Passed) sb.Append("\n  - ").Append(r.Name).Append(": ").Append(r.Message.Split('\n')[0]);
            }
            return sb.ToString();
        }
    }
}
