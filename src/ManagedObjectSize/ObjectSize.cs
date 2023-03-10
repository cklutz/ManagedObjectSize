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
        public static long GetObjectExclusiveSize(object? obj) => GetObjectExclusiveSize(obj, ObjectSizeOptions.Default);

        /// <summary>
        /// Calculates approximate memory size of object itself, not accounting for sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <returns>Approximate size of managed object.</returns>
        public static long GetObjectExclusiveSize(object? obj, ObjectSizeOptions options)
        {
            if ((options & ObjectSizeOptions.UseRtHelpers) != 0)
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
        public static long GetObjectInclusiveSize(object? obj) => GetObjectInclusiveSize(obj, ObjectSizeOptions.Default, out _);

        /// <summary>
        /// Calculates approximate memory size of object and its reference graph, recursively adding up sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <returns>Approximate size of managed object and its reference graph.</returns>
        public static long GetObjectInclusiveSize(object? obj, ObjectSizeOptions options) => GetObjectInclusiveSize(obj, options, out _);

        /// <summary>
        /// Calculates approximate memory size of object and its reference graph, recursively adding up sizes of referenced objects.
        /// </summary>
        /// <param name="obj">Object to calculate size of.</param>
        /// <param name="options">Options to apply during calculation.</param>
        /// <param name="count">Outputs the number of object references seen during calculation.</param>
        /// <param name="timeout">Time after which the operation is to be aborted; <c>null</c> disables timeout.</param>
        /// <param name="cancellationToken">Cancel the operation.</param>
        /// <returns>Approximate size of managed object and its reference graph.</returns>
        /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> has been canceled.</exception>
        /// <exception cref="TimeoutException">The <paramref name="timeout"/> has elapsed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">An invalid <paramref name="timeout"/> was specified.</exception>
        public static unsafe long GetObjectInclusiveSize(object? obj, ObjectSizeOptions options, out long count,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            long stopTime = GetStopTime(timeout);
            long totalSize = 0;
            count = 0;

            if (obj == null)
            {
                return totalSize;
            }

            var eval = new Stack<object>();
            var considered = new HashSet<ulong>();

            eval.Push(obj);

            while (eval.Count > 0)
            {
                // Check abort conditions.
                cancellationToken.ThrowIfCancellationRequested();
                if (stopTime != -1)
                {
                    CheckStopTime(stopTime, totalSize, count, timeout);
                }

                var currentObject = eval.Pop();
                if (currentObject == null)
                {
                    // Cannot get the size for a "null" object.
                    continue;
                }

                ulong objAddr = (ulong)GetHeapPointer(currentObject);
                if (!considered.Add(objAddr))
                {
                    // Already seen this object.
                    continue;
                }

                var currentType = currentObject.GetType();
                if (currentType == typeof(Pointer) || currentType.IsPointer)
                {
                    // Pointers are not considered.
                    continue;
                }

                long currSize = GetObjectExclusiveSize(currentObject, options);
                count++;
                totalSize += currSize;

                if ((options & ObjectSizeOptions.DebugOutput) != 0)
                {
                    Console.WriteLine($"[{count:N0}] {(totalSize - currSize):N0} -> {totalSize:N0} ({currSize:N0}: {currentObject.GetType()})");
                }

                if (currentType == typeof(string))
                {
                    // String is a special object type in the CLR. We have already recorded the correct length of it
                    // by using GetObjectExclusiveSize().
                    continue;
                }

                if (currentType.IsArray)
                {
                    HandleArray(eval, considered, currentObject, currentType);
                }

                AddFields(eval, considered, currentObject, currentType);
            }

            if ((options & ObjectSizeOptions.DebugOutput) != 0)
            {
                Console.WriteLine($"total: {totalSize:N0} ({obj.GetType()})");
            }

            return totalSize;
        }

        private static long GetStopTime(TimeSpan? timeout)
        {
            if (timeout != null)
            {
                if (timeout.Value.TotalMilliseconds < 0 || timeout.Value.TotalMilliseconds > (int.MaxValue - 1))
                {
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, null);
                }

                return Environment.TickCount64 + (int)(timeout.Value.TotalMilliseconds + 0.5);
            }

            return -1;
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

        private static unsafe void HandleArray(Stack<object> eval, HashSet<ulong> considered, object obj, Type objType)
        {
            var elementType = objType.GetElementType();
            if (elementType != null && !elementType.IsPointer)
            {
                foreach (object element in (System.Collections.IEnumerable)obj)
                {
                    if (element != null)
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
                }
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