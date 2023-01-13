using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace ManagedObjectSize.Tests
{
    [TestClass]
    public class ObjectSizeTests
    {
        [TestMethod]
        public void ObjectSize_AbortsIfCancellationIsRequested()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Assert.ThrowsException<OperationCanceledException>(() =>
                {
                    ObjectSize.GetObjectInclusiveSize("", ObjectSizeOptions.Default, out _, null, cts.Token);
                });
            }
        }

        [TestMethod]
        public void ObjectSize_UsesTimeoutIfConfigured()
        {
            // Shortest possible timeout is 1 tick.
            // For any non-null object graph that should be small enough to actually trigger the
            // timeout - hopefully. If we see spurious test failures here, we might need to re-
            // check or provide some sort of mock support for the timeout calculation inside.
            var timeout = TimeSpan.FromTicks(1);

            Assert.ThrowsException<TimeoutException>(() =>
            {
                ObjectSize.GetObjectInclusiveSize(new ExampleHolder(), ObjectSizeOptions.Default, out _, timeout);
            });
        }

        [TestMethod]
        public void ObjectSize_Null_ReturnsZero()
        {
            Assert.AreEqual(0, ObjectSize.GetObjectInclusiveSize(null));
        }

        // We could also use [DynamicData] to conduct the test of different objects/types, which would
        // result in possibly better diagnostics for failed tests, continue running if one test fails,
        // and report the "true" number of tests, not just 2 as it is now.
        // Using this, however, would also mean that a snapshot (using ClrMD) would be created per
        // object/type. While this is relatively cheap on Windows, it would cause much longer times
        // on Linux (where PSS snapshots are not supported and a core dump is generated each time,
        // spawning createdump.exe, reloading the temp, etc.).

        [DataTestMethod]
        [DataRow(ObjectSizeOptions.Default)]
        [DataRow(ObjectSizeOptions.UseRtHelpers)]
        public unsafe void ObjectSize_ReportsCorrectSize(ObjectSizeOptions options)
        {
            var data = new Dictionary<ulong, (string Name, Type Type, long Count, long ExclusiveSize, long InclusiveSize)>();

            // References are on stack and won't be moved by GC.
            // So when we take their address for use in ClrMD code
            // below, it should still be valid.
            var empty = new Empty();
            var valueEmpty = new ValueEmpty();
            string @string = "Hello World";
            var exampleHolder = new ExampleHolder();
            var exampleHolder2 = new ExampleHolder2();
            var exampleHolder3 = new ExampleHolder3();
            var exampleHolder4 = new ExampleHolder4();
            var alignedDoubleSeq = new AlignedDoubleSequential();
            var alignedDoubleAuto = new AlignedDoubleAuto();
            var stringBuilder = new StringBuilder("Hello There");
            var selfRef = new SelfRef { Ref = new SelfRef() };
            selfRef.Ref.Ref = selfRef;
            var withPointer = new TypeWithPointer { Ptr = (void*)ObjectSize.GetHeapPointer(@string) };

            var valueArray = new int[] { 1, 2, 3 };
            var valueRefArray = new[] { new ValueTypeWithRef("1"), new ValueTypeWithRef("1") };
            var refArray = new[] { new TypeWithRef("1"), new TypeWithRef("2") };
            var pointerArray = new void*[] { (void*)ObjectSize.GetHeapPointer(@string), (void*)ObjectSize.GetHeapPointer(empty) };
            var emptyValueArray = new int[] { };
            var emptyRefArray = new Empty[] { };
            var emptyValueRefArray = new ValueTypeWithRef[] { };
            var emptyPointerArray = new void*[] { };

            string internedString1 = String.Intern("INTERNED");
            string internedString2 = String.Intern("INTERNED");
            var internedStrings = new string[] { internedString1, internedString2 };

            // options |= ObjectSizeOptions.DebugOutput;
            GetSize(options, empty, data);
            GetSize(options, valueEmpty, data);
            GetSize(options, @string, data);
            GetSize(options, exampleHolder, data);
            GetSize(options, exampleHolder2, data);
            GetSize(options, exampleHolder3, data);
            GetSize(options, exampleHolder4, data);
            GetSize(options, alignedDoubleSeq, data);
            GetSize(options, alignedDoubleAuto, data);
            GetSize(options, stringBuilder, data);
            GetSize(options, selfRef, data);
            GetSize(options, withPointer, data);

            GetSize(options, valueArray, data);
            GetSize(options, valueRefArray, data);
            GetSize(options, refArray, data);
            GetSize(options, pointerArray, data);
            GetSize(options, emptyValueArray, data);
            GetSize(options, emptyValueRefArray, data);
            GetSize(options, emptyRefArray, data);
            GetSize(options, emptyPointerArray, data);

            GetSize(options, internedStrings, data);

            using (var dt = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id))
            {
                using (var runtime = dt.ClrVersions.Single().CreateRuntime())
                {
                    foreach (ulong address in data.Keys)
                    {
                        string currentName = data[address].Name;

                        var clrObj = runtime.Heap.GetObject(address);

                        // Sanity check that address (still) refers to something valid.
                        Assert.IsTrue(clrObj.IsValid, currentName + " IsValid");

                        // Make sure we are not comparing apples and oranges.
                        Assert.AreEqual(data[address].Type.FullName, clrObj.Type?.ToString(), currentName + " Type");

                        // Compare actual sizes
                        (int count, ulong inclusiveSize, ulong exclusiveSize) = ObjSize(clrObj, (options & ObjectSizeOptions.DebugOutput) != 0);
                        Assert.AreEqual(data[address].Count, count, currentName + " Count");
                        Assert.AreEqual(data[address].InclusiveSize, (long)inclusiveSize, currentName + " InclusiveSize");
                        Assert.AreEqual(data[address].ExclusiveSize, (long)exclusiveSize, currentName + " ExclusiveSize");
                    }
                }
            }
        }

        private static void GetSize(ObjectSizeOptions options, object obj,
            Dictionary<ulong, (string Name, Type Type, long Count, long ExclusiveSize, long InclusiveSize)> sizes,
            [CallerArgumentExpression("obj")] string? name = null)
        {
            long exclusiveSize = ObjectSize.GetObjectExclusiveSize(obj, options);
            long inclusiveSize = ObjectSize.GetObjectInclusiveSize(obj, options, out long count);

            ulong address = (ulong)ObjectSize.GetHeapPointer(obj);

            sizes.Add(address, (name!, obj.GetType(), count, exclusiveSize, inclusiveSize));
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
        private struct ValueEmpty { }

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

        private class SelfRef
        {
            public SelfRef? Ref;
        }

        private unsafe class TypeWithPointer
        {
            public void* Ptr;
        }
    }
}