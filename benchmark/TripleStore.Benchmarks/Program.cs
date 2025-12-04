using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Loggers;
using System;
using System.IO;

namespace TripleStore.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var artifactsRoot = Path.Combine(Path.GetTempPath(), "TripleStoreBenchmarks", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        var inProcessNoEmit = Job.InProcess
            .WithToolchain(InProcessNoEmitToolchain.Instance)
            .WithWarmupCount(1)
            .WithIterationCount(3)
            .WithUnrollFactor(1);

        var config = ManualConfig.CreateEmpty()
            .AddJob(inProcessNoEmit)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(CsvExporter.Default)
            .AddExporter(MarkdownExporter.GitHub)
            .AddLogger(ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath(artifactsRoot);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
