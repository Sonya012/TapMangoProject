using System.Collections.Concurrent;
using TapMangoProject.Interfaces;

namespace TapMangoTakeHomeProject.Utilities
{
    public class TimeWindowCounter : ITimeWindowCounter
    {
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
        private readonly TimeSpan _windowSize = TimeSpan.FromSeconds(1);
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        public int Count(DateTime now)
        {
            CleanupOld(now);
            return _timestamps.Count;
        }

        public void Increment(DateTime now)
        {
            _timestamps.Enqueue(now);
            LastUsed = now;
            CleanupOld(now);
        }

        private void CleanupOld(DateTime currentDateTime)
        {
            while (_timestamps.TryPeek(out DateTime tsDateTime) && currentDateTime - tsDateTime > _windowSize)
            {
                _timestamps.TryDequeue(out _);
            }
        }
    }
}
