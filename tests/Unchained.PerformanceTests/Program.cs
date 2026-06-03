using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithId("net8"))
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core90).WithId("net9"))
    .AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0).WithId("net10"));

// Run a specific benchmark with --filter, e.g.:
//   dotnet run -c Release -- --filter *Baseline*
//   dotnet run -c Release -- --filter *Comparison*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
