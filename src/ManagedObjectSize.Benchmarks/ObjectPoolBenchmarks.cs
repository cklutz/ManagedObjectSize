using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.ObjectPool;

namespace ManagedObjectSize.Benchmarks
{
    [MemoryDiagnoser]
    public class ObjectPoolBenchmarks
    {
        [Params(100, 1000)] public int N;

        private GraphObject m_graphData = null!;
        private ObjectSizeOptions m_options = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            m_graphData = GraphObject.CreateObjectGraph(N);
            m_options = new ObjectSizeOptions
            {
                PoolProvider = new DefaultObjectPoolProvider()
            };
        }

        [Benchmark] public long NoPool() => ObjectSize.GetObjectInclusiveSize(m_graphData);

        [Benchmark] public long Pool() => ObjectSize.GetObjectInclusiveSize(m_graphData, m_options);
    }
}
