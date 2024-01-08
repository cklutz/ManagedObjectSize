using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace ManagedObjectSize.Benchmarks
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var xargs = new List<string>(args);
            var config = ManualConfig.Create(DefaultConfig.Instance);
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(xargs.ToArray(), config);
        }
    }
}
