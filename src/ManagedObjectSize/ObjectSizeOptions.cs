
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
        private double? m_arraySampleConfidenceLevel;
        private int m_arraySampleConfidenceInterval = 5;

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

                if (value != null && (value.Value.TotalMilliseconds < 0 || value.Value.TotalMilliseconds > (int.MaxValue - 1)))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
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

        /// <summary>
        /// Gets or sets a value that describes how many elements of an array should be checked at a maximum.
        /// If the array contains less elements than this value, the array is processed as if sampling would
        /// not have been enabled. Also see the <i>remarks</i> section.
        /// </summary>
        /// <value>
        /// The number of elements of an array to check at a maximum. The minimum value is <c>2</c>.
        /// Also see the <i>remarks</i> section.
        /// </value>
        /// <remarks>
        /// Sampling will contain too high estimates, when the elements in the array share a lot of objects.
        /// For example, if the array (elements) contain a lot of strings that are all the same (address).
        /// This can be circumvented (a bit) by choosing a sample size that is not too small, compared to the
        /// actual data. However, this quickly questions the usefulness of sampling in the first place. You
        /// should use sampling only if you can live with number that are higher than the actual usage, or
        /// when you know your data (to contain many unique objects).
        /// </remarks>
        public int? ArraySampleCount
        {
            get => m_arraySampleCount;
            set
            {
                CheckReadOnly();

                if (value != null && value.Value < 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value.Value, "Need at least a sample count of two");
                }

                m_arraySampleCount = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that determines the sample size (<see cref="ArraySampleCount"/>) based on a given
        /// confidence level.
        /// If the array contains less elements than the calculated sample size, the array is processed as if sampling would
        /// not have been enabled. Also see the <i>remarks</i> section.
        /// </summary>
        /// <remarks>
        /// Sampling will contain too high estimates, when the elements in the array share a lot of objects.
        /// For example, if the array (elements) contain a lot of strings that are all the same (address).
        /// This can be circumvented (a bit) by choosing a sample size that is not too small, compared to the
        /// actual data. However, this quickly questions the usefulness of sampling in the first place. You
        /// should use sampling only if you can live with number that are higher than the actual usage, or
        /// when you know your data (to contain many unique objects).
        /// </remarks>
        public double? ArraySampleConfidenceLevel
        {
            get => m_arraySampleConfidenceLevel;
            set
            {
                CheckReadOnly();

                if (value != null && (value.Value > 100 || value.Value <= 0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value.Value, "Value must be greater than zero and 100 or less");
                }

                m_arraySampleConfidenceLevel = value;
            }
        }

        public int ArraySampleConfidenceInterval
        {
            get => m_arraySampleConfidenceInterval;
            set
            {
                CheckReadOnly();

                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be negative");
                }

                m_arraySampleConfidenceInterval = value;
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
                ArraySampleConfidenceInterval = m_arraySampleConfidenceInterval,
                ArraySampleConfidenceLevel = m_arraySampleConfidenceLevel,
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