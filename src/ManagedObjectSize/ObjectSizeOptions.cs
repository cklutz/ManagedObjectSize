
using System.Threading;

namespace ManagedObjectSize
{
    public class ObjectSizeOptions
    {
        private bool m_debugOutput;
        private bool m_useRtHelpers;
        private int? m_arraySampleCount;
        private TimeSpan? m_timeout;
        private CancellationToken m_cancellationToken;
        private TextWriter m_debugWriter = Console.Out;

        public CancellationToken CancellationToken
        {
            get => m_cancellationToken;
            set
            {
                CheckReadOnly();
                m_cancellationToken = value;
            }
        }

        public TimeSpan? Timeout
        {
            get => m_timeout;
            set
            {
                CheckReadOnly();

                if (value != null)
                {
                    if (value.Value.TotalMilliseconds < 0 || value.Value.TotalMilliseconds > (int.MaxValue - 1))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                    }
                }

                m_timeout = value;
            }
        }

        public TextWriter DebugWriter
        {
            get => m_debugWriter;
            set
            {
                CheckReadOnly();
                m_debugWriter = value ?? Console.Out;
            }
        }

        public bool DebugOutput
        {
            get => m_debugOutput;
            set
            {
                CheckReadOnly();
                m_debugOutput = value;
            }
        }

        public bool UseRtHelpers
        {
            get => m_useRtHelpers;
            set
            {
                CheckReadOnly();
                m_useRtHelpers = value;
            }
        }

        public int? ArraySampleCount
        {
            get => m_arraySampleCount;
            set
            {
                CheckReadOnly();
                if (value != null)
                {
                    if (value.Value < 2)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), value.Value, "Need at least a sample count of two");
                    }
                }
                m_arraySampleCount = value;
            }
        }

        internal long GetStopTime(long ticksNow)
        {
            if (Timeout != null)
            {
                return ticksNow + (int)(Timeout.Value.TotalMilliseconds + 0.5);
            }

            return -1;
        }

        public bool IsReadOnly { get; private set; }

        internal ObjectSizeOptions GetReadOnly()
        {
            var result = new ObjectSizeOptions
            {
                DebugOutput = m_debugOutput,
                UseRtHelpers = m_useRtHelpers,
                ArraySampleCount = m_arraySampleCount,
                Timeout = m_timeout,
                CancellationToken = m_cancellationToken,
                DebugWriter = m_debugWriter,
                IsReadOnly = true
            };
            return result;
        }

        private void CheckReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Cannot change a read only instance");
            }
        }
    }
}