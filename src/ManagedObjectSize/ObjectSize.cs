//#define FEATURE_STATISTICS
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ManagedObjectSize
{
    /// <summary>
    /// Object memory size calculation.
    /// </summary>
    public static class ObjectSize
    {
        //
        // Material:
        // - https://github.com/dotnet/runtime/issues/24200
        // - https://devblogs.microsoft.com/premier-developer/managed-object-internals-part-1-layout/
        // - ClrMD ObjSize: algorithm (https://github.com/microsoft/clrmd)
        // - https://github.com/dotnet/runtime:
        //      - https://github.com/dotnet/runtime/blob/074a01611837db63e9fe1d7462916d47ed858a75/src/coreclr/vm/object.h
        //      - https://github.com/dotnet/runtime/blob/074a01611837db63e9fe1d7462916d47ed858a75/src/coreclr/vm/methodtable.h
        //

        /// <summary>
        /// Calculates approximate memory size of object itself, not accounting for sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <returns>Approximate size of managed object.</returns>
        public static long GetObjectExclusiveSize(object? obj) => GetObjectExclusiveSize(obj, null);

        /// <summary>
        /// Calculates approximate memory size of object itself, not accounting for sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <returns>Approximate size of managed object.</returns>
        public static long GetObjectExclusiveSize(object? obj, ObjectSizeOptions? options)
        {
            options = (options ?? new()).GetReadOnly();

            if (options.UseRtHelpers)
            {
                return GetObjectExclusiveSizeRtHelpers(obj);
            }

            return GetObjectExclusiveSizeInternal(obj);
        }

        /// <summary>
        /// Calculates approximate memory size of object and its reference graph, recursively adding up sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <returns>Approximate size of managed object and its reference graph.</returns>
        public static long GetObjectInclusiveSize(object? obj) => GetObjectInclusiveSize(obj, null, out _);

        /// <summary>
        /// Calculates approximate memory size of object and its reference graph, recursively adding up sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <returns>Approximate size of managed object and its reference graph.</returns>
        public static long GetObjectInclusiveSize(object? obj, ObjectSizeOptions? options) => GetObjectInclusiveSize(obj, options, out _);

        /// <summary>
        /// Calculates approximate memory size of object and its reference graph, recursively adding up sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <param name="count">Outputs the number of object references seen during calculation.</param>
        /// <returns>Approximate size of managed object and its reference graph.</returns>
        /// <exception cref="OperationCanceledException">The <paramref name="options"/>.<see cref="ObjectSizeOptions.CancellationToken"/> has been canceled.</exception>
        /// <exception cref="TimeoutException">The <paramref name="options"/><see cref="ObjectSizeOptions.Timeout"/> has elapsed.</exception>
        public static unsafe long GetObjectInclusiveSize(object? obj, ObjectSizeOptions? options, out long count)
        {
            if (obj == null)
            {
                count = 0;
                return 0;
            }

            options = (options ?? new()).GetReadOnly();

            var eval = new Stack<object>();
            var state = new EvaluationState
            {
                Considered = new(),
                StopTime = options.GetStopTime(Environment.TickCount64),
                Options = options
            };

            eval.Push(obj);

#if FEATURE_STATISTICS
            state.StartStatistics();
            state.UpdateEval(eval);
#endif

            long totalSize = ProcessEvaluationStack(eval, state, out count);

#if FEATURE_STATISTICS
            state.StopStatistics();
            state.DumpStatistics(totalSize);
#endif

            if (options.DebugOutput)
            {
                state.Options.DebugWriter.WriteLine($"total: {totalSize:N0} ({obj.GetType()})");
            }

            return totalSize;
        }

        private class EvaluationState
        {
            public long StopTime { get; set; }
            public ObjectSizeOptions Options { get; set; } = null!;
            public HashSet<ulong> Considered { get; set; } = null!;

#if FEATURE_STATISTICS
            private long m_started;
            private long m_complete;
            private int m_maxConsidered;
            private int m_sampleMaxConsidered;
            private int m_maxEval;
            private int m_sampleMaxEval;
            private int m_sampled;
            private int m_notSampled;
            private int m_arrays;

            public void DumpStatistics(long totalSize)
            {
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
                Options.DebugWriter.WriteLine("STATISTICS");
                Options.DebugWriter.WriteLine($"  enabled options        : {Options.GetEnabledString()}");
                Options.DebugWriter.WriteLine($"  elapsed                : {new TimeSpan(m_complete - m_started)}");
                Options.DebugWriter.WriteLine($"  total size             : {totalSize.ToString("N0")} bytes");
                Options.DebugWriter.WriteLine($"  max seen/evaluated     : {m_maxConsidered:N0}/{m_maxEval:N0}");
                Options.DebugWriter.WriteLine($"  arrays                 : {m_arrays:N0}");
                Options.DebugWriter.WriteLine($"    not sampled          : {m_notSampled:N0}");
                Options.DebugWriter.WriteLine($"    sampled              : {m_sampled:N0}");
                Options.DebugWriter.WriteLine($"      max seen/evaluated : {m_sampleMaxConsidered:N0}/{m_sampleMaxEval:N0}");
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
            }

            public void StartStatistics() => m_started = System.Diagnostics.Stopwatch.GetTimestamp();
            public void StopStatistics() => m_complete = System.Diagnostics.Stopwatch.GetTimestamp();
            public void UpdateConsidered() => m_maxConsidered = Math.Max(Considered.Count, m_maxConsidered);
            public void UpdateSampleConsidered(HashSet<ulong> considered) => m_sampleMaxConsidered = Math.Max(considered.Count, m_sampleMaxConsidered);
            public void UpdateEval(Stack<object> eval) => m_maxEval = Math.Max(eval.Count, m_maxEval);
            public void UpdateSampleEval(Stack<object> eval) => m_sampleMaxEval = Math.Max(eval.Count, m_sampleMaxEval);
            public void UpdateSampled() => m_sampled++;
            public void UpdateNotSampled() => m_notSampled++;
            public void UpdateArrays() => m_arrays++;
#endif
        }

        private static unsafe long ProcessEvaluationStack(Stack<object> eval, EvaluationState state, out long count)
        {
            count = 0;
            long totalSize = 0;

            while (eval.Count > 0)
            {
                // Check abort conditions.
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                if (state.StopTime != -1)
                {
                    CheckStopTime(state.StopTime, totalSize, count, state.Options.Timeout);
                }

                var currentObject = eval.Pop();

                if (currentObject == null)
                {
                    // Cannot get the size for a "null" object.
                    continue;
                }

                ulong objAddr = (ulong)GetHeapPointer(currentObject);
                if (!state.Considered.Add(objAddr))
                {
                    // Already seen this object.
                    continue;
                }

#if FEATURE_STATISTICS
                state.UpdateConsidered();
#endif

                var currentType = currentObject.GetType();
                if (currentType == typeof(Pointer) || currentType.IsPointer)
                {
                    // Pointers are not considered.
                    continue;
                }

                long currSize;
                if (currentObject is ArraySample arraySample)
                {
                    currSize = arraySample.Size;
                    count += arraySample.ElementCount;
                }
                else
                {
                    currSize = GetObjectExclusiveSize(currentObject, state.Options);
                    count++;
                }
                totalSize += currSize;

                if (state.Options.DebugOutput)
                {
                    state.Options.DebugWriter.WriteLine($"[{count:N0}] {(totalSize - currSize):N0} -> {totalSize:N0} ({currSize:N0}: {currentObject.GetType()})");
                }

                if (currentType == typeof(string))
                {
                    // String is a special object type in the CLR. We have already recorded the correct length of it
                    // by using GetObjectExclusiveSize().
                    continue;
                }

                if (currentType.IsArray)
                {
                    HandleArray(eval, state, currentObject, currentType);
                }
                else
                {
                    AddFields(eval, state.Considered, currentObject, currentType);
                }
            }

            return totalSize;
        }

        private static void CheckStopTime(long stopAt, long totalSize, long count, TimeSpan? timeout)
        {
            if (Environment.TickCount64 >= stopAt)
            {
                throw new TimeoutException(
                    $"The allotted time of {timeout} to determine the inclusive size of the object (graph) has passed. " +
                    $"The incomplete result so far is {totalSize:N0} bytes for processing {count:N0} objects. ");
            }
        }

        private static unsafe void HandleArray(Stack<object> eval, EvaluationState state, object obj, Type objType)
        {
            var elementType = objType.GetElementType();
            if (elementType != null && !elementType.IsPointer)
            {
#if FEATURE_STATISTICS
                state.UpdateArrays();
#endif
                (int sampleSize, int? populationSize, bool always) = GetSampleAndPopulateSize(state, obj, objType);

                // Only sample if:
                // - the "always" flag has not been set in options
                // - we have determined an actual sample size
                // - if the total number of elements in the array is not less than the sample size
                if (!always && (
                        sampleSize == 0 ||
                        (populationSize != null && populationSize <= sampleSize) ||
                        HasLessElements(obj, sampleSize, elementType))
                    )
                {
                    HandleArrayNonSampled(eval, state, obj, elementType);
                }
                else
                {
                    HandleArraySampled(eval, state, obj, elementType, sampleSize);
                }
            }
        }

        private static unsafe void HandleArraySampled(Stack<object> eval, EvaluationState state, object obj, Type? elementType, int sampleSize)
        {
#if FEATURE_STATISTICS
            state.UpdateSampled();
#endif

            int elementCount = 0;

            // TODO: Should these be from a pool? Measure if cost is too high allocating if we have
            // a "large" number of arrays to sample.
            var localEval = new Stack<object>();
            var localConsidered = new HashSet<ulong>();

            foreach (object element in (System.Collections.IEnumerable)obj)
            {
                if (ShouldCountElement(element, elementType))
                {
                    // We're only counting the elements that are actually non-null. This might
                    // be less then the size of the array, when the array contains null elements.
                    // On the other hand, if we could every element, we also count excess elements.
                    // For example, the extra (unused) capacity of a List<>.
                    // Only considering non-null elements is still correct, however, because null
                    // elements don't contribute to the size.
                    elementCount++;

                    if (elementCount <= sampleSize)
                    {
                        ulong elementAddr = (ulong)GetHeapPointer(element);
                        if (!localConsidered.Contains(elementAddr))
                        {
                            HandleArrayElement(localEval, localConsidered, elementType, element);
                            localConsidered.Add(elementAddr);
#if FEATURE_STATISTICS
                                state.UpdateSampleConsidered(localConsidered);
                                state.UpdateSampleEval(localEval);
#endif
                        }
                    }
                }
            }

            if (localEval.Count > 0)
            {
                double sizeOfSamples = ProcessEvaluationStack(localEval, state, out _);

                var sample = new ArraySample
                {
                    Size = (long)((sizeOfSamples / localConsidered.Count) * elementCount),
                    ElementCount = elementCount
                };

                eval.Push(sample);
#if FEATURE_STATISTICS
                    state.UpdateEval(eval);
#endif
            }
        }

        private static unsafe (int SampleSize, int? PopulationSize, bool Always) GetSampleAndPopulateSize(EvaluationState state, object obj, Type elementType)
        {
            if (state.Options.AlwaysUseArraySampleAlgorithm)
            {
                int populationSize = CountNonNullElements(obj, elementType);
                return (populationSize, populationSize, true);
            }
            else if (state.Options.ArraySampleCount != null)
            {
                int sampleSize = state.Options.ArraySampleCount.Value;

                if (state.Options.DebugOutput)
                {
                    state.Options.DebugWriter.WriteLine($"array {GetHeapPointer(obj)}/{elementType}[]: sampleSize={sampleSize:N0}");
                }

                return (sampleSize, null, false);
            }
            else if (state.Options.ArraySampleConfidenceLevel != null)
            {
                // For size calculation we also only consider non-null elements, so here we have to do it as well.
                // If we wouldn't, the population size would be too big and the sample size thus too small.
                int populationSize = CountNonNullElements(obj, elementType);
                int sampleSize = Utils.CalculateSampleCount(state.Options.ArraySampleConfidenceLevel.Value, state.Options.ArraySampleConfidenceInterval, populationSize);

                if (state.Options.DebugOutput)
                {
                    state.Options.DebugWriter.WriteLine($"array {GetHeapPointer(obj)}/{elementType}[]: population={populationSize:N0} sampleSize={sampleSize:N0}");
                }

                return (sampleSize, populationSize, false);
            }

            return (0, null, false);
        }

        private static void AddRange(HashSet<ulong> first, HashSet<ulong> second)
        {
            foreach (ulong s in second)
            {
                first.Add(s);
            }
        }

        private class ArraySample
        {
            public long Size { get; set; }
            public int ElementCount { get; set; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldCountElement(object element, Type elementType) => elementType.IsValueType || element != null;

        private static int CountNonNullElements(object obj, Type elementType)
        {
            if (elementType.IsValueType)
            {
                return ((Array)obj).Length;
            }

            int count = 0;
            foreach (object element in (System.Collections.IEnumerable)obj)
            {
                if (ShouldCountElement(element, elementType))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasLessElements(object obj, int max, Type elementType)
        {
            int count = 0;
            foreach (object element in (System.Collections.IEnumerable)obj)
            {
                if (ShouldCountElement(element, elementType))
                {
                    count++;
                    if (count >= max)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static unsafe void HandleArrayNonSampled(Stack<object> eval, EvaluationState state, object obj, Type elementType)
        {
#if FEATURE_STATISTICS
            state.UpdateNotSampled();
#endif

            foreach (object element in (System.Collections.IEnumerable)obj)
            {
                if (ShouldCountElement(element, elementType))
                {
                    HandleArrayElement(eval, state.Considered, elementType, element);
                }
            }
        }

        private static unsafe void HandleArrayElement(Stack<object> eval, HashSet<ulong> considered, Type elementType, object element)
        {
            if (!elementType.IsValueType)
            {
                ulong elementAddr = (ulong)GetHeapPointer(element);
                if (!considered.Contains(elementAddr))
                {
                    eval.Push(element);
                }
            }
            else
            {
                AddFields(eval, considered, element, elementType);
            }
        }

        private static unsafe void AddFields(Stack<object> eval, HashSet<ulong> considered, object currentObject, Type objType)
        {
            foreach (var field in objType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType.IsValueType)
                {
                    // Non reference type fields are "in place" in the actual type and thus are already included in
                    // GetObjectExclusiveSize(). This is also true for custom value types.
                    continue;
                }

                var fieldValue = field.GetValue(currentObject);
                if (fieldValue != null)
                {
                    ulong fieldAddr = (ulong)GetHeapPointer(fieldValue);
                    if (!considered.Contains(fieldAddr))
                    {
                        eval.Push(fieldValue);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe IntPtr GetHeapPointer(object @object)
        {
            var indirect = Unsafe.AsPointer(ref @object);
            return **(IntPtr**)(&indirect);
        }

        // "Constants" are adapted from vm/object.h.
        private static readonly uint ObjHeaderSize = (uint)IntPtr.Size;
        private static readonly uint ObjSize = (uint)IntPtr.Size;
        private static readonly uint ObjBaseSize = ObjHeaderSize + ObjSize;
        private static readonly uint MinObjSize = (2 * (uint)IntPtr.Size) + ObjHeaderSize;

        // The CoreCLR provides an internal "RuntimeHelpers.GetRawObjectDataSize()" method.
        // We don't want to use it by default, but allow calling it to compare results.
        private delegate nuint GetRawObjectDataSize(object obj);
        private static GetRawObjectDataSize? s_getRawObjectDataSize;
        private static long GetObjectExclusiveSizeRtHelpers(object? obj)
        {
            if (obj == null)
            {
                return 0;
            }

            var gros = LazyInitializer.EnsureInitialized(ref s_getRawObjectDataSize, () =>
            {
                var bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
                var method = typeof(RuntimeHelpers).GetMethod("GetRawObjectDataSize", bindingFlags)
                    ?? throw new InvalidOperationException($"Method 'RuntimeHelpers.GetRawObjectDataSize()' not found");
                return (GetRawObjectDataSize)Delegate.CreateDelegate(typeof(GetRawObjectDataSize), method);
            });

            long size = (long)gros(obj);
            // RuntimeHelpers.GetRawObjectDataSize strips off the "ObjectBaseSize", hence the name "Data".
            // For our purposes we want it included.
            size += ObjBaseSize;

            return size < MinObjSize ? MinObjSize : size;
        }

        private static long GetObjectExclusiveSizeInternal(object? obj)
        {
            if (obj == null)
            {
                return 0;
            }

            unsafe
            {
                var mt = GetMethodTable(obj);
                long size = mt->BaseSize;
                if (mt->HasComponentSize)
                {
                    uint componentSize = mt->ComponentSize;

                    if (componentSize > 0)
                    {
                        // Get number of components (strings and arrays)
                        var objAddr = GetHeapPointer(obj);
                        int numComponentsOffset = IntPtr.Size;
                        int numComponents = Marshal.ReadInt32(objAddr, numComponentsOffset);

                        size += componentSize * numComponents;
                    }
                }

                size = size < MinObjSize ? MinObjSize : size;
                return size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe MethodTable* GetMethodTable(object obj)
        {
            //
            // Get the MethodTable structure. The following code has been lifted from RuntimeHelpers.GetMethodTable().
            //
            // In RuntimeHelpers, the method is internal, but the code is enlightening and especially so, when you look
            // at the comment of the JIT instrinct that will be used instead of the actual implementation code below.
            //
            // (source: https://github.com/dotnet/runtime/blob/074a01611837db63e9fe1d7462916d47ed858a75/src/coreclr/vm/jitinterface.cpp#L7243):
            //
            //     In the CLR, an object is laid out as follows.
            //     [ object_header || MethodTable* (64-bit pointer) || instance_data ]
            //                        ^                                ^-- ref <theObj>.firstField points here
            //                        `-- <theObj> reference (type O) points here
            // 
            //     [ snip more comment]
            //
            // Essentially, the "<theObj>.firstField" part is what "GetRawData()" returns, we then go back by one (which is
            // IntPtr.Size in bytes) to get the actual MethodTable*.
            //

            return (MethodTable*)Unsafe.Add(ref Unsafe.As<byte, IntPtr>(ref GetRawData(obj)), -1);

            // IL (pseudo) code for what the JIT generates for the actual RuntimeHelpers.GetMethodTable() function
            // (not for this one of course!) would be something like this:
            //
            // MethodTable* GetMethodTable(object obj)
            // {
            //     ldarg_0
            //     ldflda <obj>.firstField
            //     ldc_i4_s -IntPtr.Size
            //     add
            //     ldind_i
            //     ret
            // }
            //
            // We could achieve the same using DynamicMethod and ILGenerator. However, the "<obj>.firstField" is what is
            // tricky. The JIT can get this from internal CLR data structures, but for managed code it basically be "GetRawData()" again.
            // So in the end we wouldn't have won too much by using IL.
            //
            // We could also just reflection invoke RuntimeHelpers.GetMethodTable(), but that is costly and relies on the method actually being
            // there. The above approach also uses established information about objects are laid out and is thus more robust than
            // invoking the internal RuntimeHelpers.GetMethodTable() method.
            //
            //
            // Note: this works also
            //
            //      return (MethodTable*)obj.GetType().TypeHandle.Value.ToPointer();
            // 
            // But since the CLR itself uses the above code internally, we rather stick with that.
        }

        internal sealed class RawData
        {
            public byte Data;
        }

        internal static ref byte GetRawData(object obj) => ref Unsafe.As<RawData>(obj).Data;

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct MethodTable
        {
            // According to src\vm\methodtable.h we have the following members in the MethodTable (that interest us here;
            // there a more).
            //
            // Offset   Size
            // [0x0000]    4    DWORD m_dwFlags;       // Low WORD is component size for array and string types when
            //                                         // (m_dwFlags & enum_flag_HasComponentSize)!=0; otherwise flags.
            // [0x0004]    4    DWORD m_BaseSize;      // Base size of instance of this class when allocated on the heap
            // [0x0008]    2    WORD  m_wFlags2;
            // [0x000A]    2    WORD  m_wToken;        // Class token if it fits into 16-bits.
            // [0x000C]    2    WORD  m_wNumVirtuals;
            // [0x000E]    2    WORD  m_wNumInterfaces;
            //

            // Put both fields at index 0; access ComponentSize for respective value if "HasComponentSize == true"
            [FieldOffset(0)]
            public ushort ComponentSize;
            [FieldOffset(0)]
            private uint Flags;
            [FieldOffset(4)]
            public uint BaseSize;

            private const uint enum_flag_ContainsPointers = 0x01000000;
            private const uint enum_flag_HasComponentSize = 0x80000000;
            public bool HasComponentSize => (Flags & enum_flag_HasComponentSize) != 0;
            public bool ContainsPointers => (Flags & enum_flag_ContainsPointers) != 0;
        }
    }
}