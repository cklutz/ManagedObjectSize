using ManagedObjectSize;
using ManagedObjectSize.ObjectPool;
using System.Diagnostics;

namespace SampleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();
            var graph = CreateObjectGraph(100_000_000, true);
            sw.Stop();
            Console.WriteLine("Object created: " + sw.Elapsed);
            Console.Out.Flush();



            sw = Stopwatch.StartNew();
            long size = ObjectSize.GetObjectInclusiveSize(graph);
            sw.Stop();
            Console.WriteLine("Full:            " + size.ToString("N0") + " bytes : " + sw.Elapsed);

            sw = Stopwatch.StartNew();
            size = ObjectSize.GetObjectInclusiveSize(graph, new ObjectSizeOptions { 
                ArraySampleCount = 1000,
            });
            sw.Stop();
            Console.WriteLine("Sample:          " + size.ToString("N0") + " bytes : " + sw.Elapsed);

            sw = Stopwatch.StartNew();
            size = ObjectSize.GetObjectInclusiveSize(graph, new()
            {
                PoolProvider = new MicrosoftExtensionsObjectPoolPoolProvider()
            });
            sw.Stop();
            Console.WriteLine("Full (pooled):   " + size.ToString("N0") + " bytes : " + sw.Elapsed);

            sw = Stopwatch.StartNew();
            size = ObjectSize.GetObjectInclusiveSize(graph, new()
            {
                ArraySampleCount = 1000,
                PoolProvider = new MicrosoftExtensionsObjectPoolPoolProvider()
            });
            sw.Stop();
            Console.WriteLine("Sample (pooled): " + size.ToString("N0") + " bytes : " + sw.Elapsed);
        }

#if false
Object created: 00:01:27.3333068
10.377.777.676 bytes : 00:02:09.7285067

Object created: 00:00:54.2183866
10.377.755.170 bytes : 00:01:23.4178055

Object created: 00:00:50.5841990
10.278.925.504 bytes : 00:01:13.4623666

Object created: 00:02:39.7571474
Full:   10.377.777.868 bytes : 00:02:20.4062759
Sample:    800.085.782 bytes : 00:00:02.3662649

Object created: 00:02:27.7242993
Full:   10.600.000.088 bytes : 00:02:29.2508853
Sample:    800.097.990 bytes : 00:00:01.1667667

#endif

        private static GraphObject CreateObjectGraph(int num, bool inner = false)
        {
            var graph = new GraphObject
            {
                ListField = new List<GraphNodeObject>(num)
            };

            int digits = (int)Math.Log10(num) + 1;
            var options = new ParallelOptions { MaxDegreeOfParallelism = inner ? 1 : Environment.ProcessorCount };
            Parallel.For(0, num, options,
                () => new List<GraphNodeObject>(),
                (i, state, local) =>
                {
                    //var node = new GraphNodeObject { StringField = "Node#" + i.ToString().PadRight(digits) };
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

            //Parallel.For(0, num, i =>
            //{
            //    var node = new GraphNodeObject { StringField = "Node#" + i };
            //    if (!inner)
            //    {
            //        node.ObjectField = CreateObjectGraph(10_000, true);
            //    }

            //    lock (graph.ListField)
            //    {
            //        graph.ListField.Add(node);
            //    }
            //});

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