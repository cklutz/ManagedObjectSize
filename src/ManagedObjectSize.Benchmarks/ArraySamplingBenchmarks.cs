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
            m_graphData = CreateObjectGraph(N);
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

        // ---------------------------------------------------------------------------------------

        private static GraphObject CreateObjectGraph(int num, bool inner = false)
        {
            var graph = new GraphObject
            {
                ListField = new List<GraphNodeObject>(num)
            };

            int digits = (int)Math.Log10(num) + 1;
            var options = new ParallelOptions { MaxDegreeOfParallelism = (inner || num < 100) ? 1 : Environment.ProcessorCount };
            Parallel.For(0, num, options,
                () => new List<GraphNodeObject>(),
                (i, state, local) =>
                {
                    var node = new GraphNodeObject { StringField = "Node#" };
                    if (!inner)
                    {
                        node.ObjectField = CreateObjectGraph(100, true);
                    }
                    local.Add(node);
                    return local;
                },
                local =>
                {
                    lock (graph.ListField)
                    {
                        graph.ListField.AddRange(local);
                    }
                });

            return graph;
        }

#pragma warning disable CS0649

        private class GraphObject
        {
            public int IntField;
            public List<GraphNodeObject> ListField = null!;
        }

        private class GraphNodeObject
        {
            public double DoubleField;
            public int IntField;
            public string StringField = null!;
            public GraphObject ObjectField = null!;
        }
    }
}
