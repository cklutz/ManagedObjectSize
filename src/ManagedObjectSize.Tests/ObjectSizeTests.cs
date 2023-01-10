using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ManagedObjectSize.Tests
{
    [TestClass]
    public class ObjectSizeTests
    {
        [DataTestMethod]
        [DataRow(ObjectSizeOptions.Default)]
        [DataRow(ObjectSizeOptions.UseRtHelpers)]
        public void Test(ObjectSizeOptions options)
        {
            options |= ObjectSizeOptions.DebugOutput;

            // References are on stack and won't be moved by GC.
            // So when we take their address for use in ClrMD code
            // below, it should still be valid.
            string s = "Hello World";
            var exampleHolder = new ExampleHolder();
            var exampleHolder2 = new ExampleHolder2();
            var exampleHolder3 = new ExampleHolder3();
            var exampleHolder4 = new ExampleHolder4();
            var alignedDoubleSeq = new AlignedDoubleSequential();
            var alignedDoubleAuto = new AlignedDoubleAuto();
            var intArray = new int[] { 1, 2, 3 };
            var empty = new Empty();
            var valueRefArray = new[] { new ValueTypeWithRef("1") , new ValueTypeWithRef("1") };
            var refArray = new[] { new TypeWithRef("1"), new TypeWithRef("2") };

            var sizes = new Dictionary<ulong, (Type Type, long Count, long ExclusiveSize, long InclusiveSize)>();

            GetSize(options, empty, sizes);
            GetSize(options, s, sizes);
            GetSize(options, exampleHolder, sizes);
            GetSize(options, exampleHolder2, sizes);
            GetSize(options, exampleHolder3, sizes);
            GetSize(options, exampleHolder4, sizes);
            GetSize(options, alignedDoubleSeq, sizes);
            GetSize(options, alignedDoubleAuto, sizes);
            GetSize(options, intArray, sizes);
            GetSize(options, valueRefArray, sizes);
            GetSize(options, refArray, sizes);

            using (var dt = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id))
            {
                using (var runtime = dt.ClrVersions.Single().CreateRuntime())
                {
                    foreach (ulong address in sizes.Keys)
                    {
                        var clrObj = runtime.Heap.GetObject(address);
                        Assert.IsTrue(clrObj.IsValid, address.ToString());

                        string id = address.ToString() + ": " + clrObj.Type;

                        Assert.AreEqual(sizes[address].Type.FullName, clrObj.Type?.ToString(), id);
                        (int count, ulong inclusiveSize, ulong exclusiveSize) = ObjSize(clrObj, (options & ObjectSizeOptions.DebugOutput) != 0);
                        Assert.AreEqual(sizes[address].Count, count, id);
                        Assert.AreEqual(sizes[address].InclusiveSize, (long)inclusiveSize, id);
                        Assert.AreEqual(sizes[address].ExclusiveSize, (long)exclusiveSize, id);
                    }
                }
            }
        }

        private static void GetSize(ObjectSizeOptions options, object obj, Dictionary<ulong, (Type Type, long Count, long ExclusiveSize, long InclusiveSize)> sizes)
        {
            long exclusiveSize = ObjectSize.GetObjectExclusiveSize(obj, options);
            long inclusiveSize = ObjectSize.GetObjectInclusiveSize(obj, options, out long count);

            ulong address = (ulong)ObjectSize.GetHeapPointer(obj);
            sizes.Add(address, (obj.GetType(), count, exclusiveSize, inclusiveSize));
        }

        private static (int count, ulong size, ulong excSize) ObjSize(ClrObject input, bool debugOutput)
        {
            var considered = new HashSet<ulong>() { input };
            var stack = new Stack<ClrObject>(100);
            stack.Push(input);

            int count = 0;
            ulong totalSize = 0;

            while (stack.Count > 0)
            {
                var curr = stack.Pop();

                count++;
                totalSize += curr.Size;

                if (debugOutput)
                {
                    Console.WriteLine($"[{count:N0}] {(totalSize - curr.Size):N0} -> {totalSize:N0} ({curr.Size:N0}: {curr.Type})");
                }

                foreach (var obj in curr.EnumerateReferences(carefully: false, considerDependantHandles: false))
                {
                    if (considered.Add(obj))
                    {
                        stack.Push(obj);
                    }
                }
            }

            if (debugOutput)
            {
                Console.WriteLine($"total: {totalSize:N0} ({input.Type})");
            }

            return (count, totalSize, input.Size);
        }

        private class Empty { }

        [StructLayout(LayoutKind.Auto)]
        private struct AlignedDoubleAuto
        {
            public byte B;
            public double D;
            public int I;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AlignedDoubleSequential
        {
            public byte B;
            public double D;
            public int I;
        }

        private class ExampleHolder
        {
            public string StringValue = "A string value";
        }

        private class ExampleHolder2
        {
            public string StringValue1 = "A string value one";
            public string StringValue2 = "A string value number two";
            public ExampleValue ExampleValue = new();
        }

        private class ExampleHolder3
        {
            public string StringValue = "A string value";
            public ExampleType ExampleType = new();
        }

        private class ExampleHolder4
        {
            public FileAccess EnumValue = FileAccess.Read;
        }

        private class ExampleType
        {
        }

        private struct ExampleValue
        {
            public ExampleValue()
            {
            }

            public int Int32Value1 = 1;
            public int Int32Value2 = 2;
        }

        private struct ValueTypeWithRef
        {
            public ValueTypeWithRef(string s)
            {
                Value = s;
            }
            public string Value;
        }

        private class TypeWithRef
        {
            public TypeWithRef(string s)
            {
                Value = s;
            }
            public string Value;
        }
    }
}