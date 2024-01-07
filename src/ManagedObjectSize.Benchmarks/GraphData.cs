namespace ManagedObjectSize.Benchmarks
{
    internal class GraphObject
    {
        public static GraphObject CreateObjectGraph(int num, bool inner = false)
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

        public int IntField;
        public List<GraphNodeObject> ListField = null!;

        public class GraphNodeObject
        {
            public double DoubleField;
            public int IntField;
            public string StringField = null!;
            public GraphObject ObjectField = null!;
        }
    }
}
