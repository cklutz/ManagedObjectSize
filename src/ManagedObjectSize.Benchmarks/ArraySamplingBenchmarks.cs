using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace ManagedObjectSize.Benchmarks
{
    [MemoryDiagnoser, EtwProfiler]
    public class ArraySamplingBenchmarks
    {
        [Params(20, 100)] public int N;

        private GraphObject m_graphData = null!;
        private int[] m_intData = null!;
        private string[] m_stringData = null!;

        private ObjectSizeOptions m_samplingOptions = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            m_graphData = GraphObject.CreateObjectGraph(N);
            m_intData = new int[N];
            m_stringData = new string[N];

            for (int i = 0; i < N; i++)
            {
                m_intData[i] = i;
                m_stringData[i] = "string#" + i;
            }

            m_samplingOptions = new() { ArraySampleCount = N / 10 };
        }

        [Benchmark] public long NoSampling_Int32() => ObjectSize.GetObjectInclusiveSize(m_intData);
        [Benchmark] public long NoSampling_String() => ObjectSize.GetObjectInclusiveSize(m_stringData);
        [Benchmark] public long NoSampling_Graph() => ObjectSize.GetObjectInclusiveSize(m_graphData);

        [Benchmark] public long Sampling_Int32() => ObjectSize.GetObjectInclusiveSize(m_intData, m_samplingOptions);
        [Benchmark] public long Sampling_String() => ObjectSize.GetObjectInclusiveSize(m_stringData, m_samplingOptions);
        [Benchmark] public long Sampling_Graph() => ObjectSize.GetObjectInclusiveSize(m_graphData, m_samplingOptions);
    }
}
