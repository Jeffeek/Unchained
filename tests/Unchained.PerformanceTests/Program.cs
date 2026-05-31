using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Unchained.PerformanceTests.Benchmarks;

var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithId("net8"))
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core90).WithId("net9"))
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0).WithId("net10"));

BenchmarkRunner.Run<PdfBaselineBenchmark>(config, args);
