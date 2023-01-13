using ManagedObjectSize;
using System.Diagnostics;

namespace SampleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var graph = CreateObjectGraph(1_000_000);
            var sw = Stopwatch.StartNew();
            long size = ObjectSize.GetObjectInclusiveSize(graph);
            sw.Stop();
            Console.WriteLine(size + ": " + sw.Elapsed);
        }

        private static object CreateObjectGraph(int num)
        {
            var graph = new GraphObject();
            graph.ListField = new List<GraphNodeObject>(num);
            for (int i = 0; i < num; i++)
            {
                graph.ListField.Add(new GraphNodeObject
                {
                    StringField = "Node#" + i
                });
            }
            return graph;
        }

        private class GraphObject
        {
            public int IntField;
            public List<GraphNodeObject> ListField;
        }

        private class GraphNodeObject
        {
            public double DoubleField;
            public int IntField;
            public string StringField;
        }
    }
}