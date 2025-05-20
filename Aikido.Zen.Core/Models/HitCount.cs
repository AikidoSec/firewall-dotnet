using System.Threading;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Abstract base class that tracks the number of hits for derived objects.
    /// Provides thread-safe hit counting functionality.
    /// </summary>
    public class HitCount
    {
        private int _hits;

        /// <summary>
        /// Gets the current number of hits.
        /// </summary>
        public int Hits => _hits;

        /// <summary>
        /// Initializes a new instance of the <see cref="HitCount"/> class.
        /// Sets the initial hit count to 0.
        /// </summary>
        public HitCount()
        {
            _hits = 0;
        }

        /// <summary>
        /// Increments the hit count in a thread-safe manner.
        /// </summary>
        public void Increment()
        {
            Interlocked.Increment(ref _hits);
        }

        /// <summary>
        /// Resets the hit count to zero in a thread-safe manner.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _hits, 0);
        }
    }
}
