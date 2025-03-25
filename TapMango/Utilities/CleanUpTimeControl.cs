using System.Collections.Concurrent;
using TapMangoProject.Interfaces;

namespace TapMangoTakeHomeProject.Utilities
{
    public static class CleanUpTimeControl
    {
        private static readonly TimeSpan _timeSpan = TimeSpan.FromSeconds(1);

        public static int Count(ConcurrentQueue<DateTime> smsSendDateTime, DateTime currentDateTime)
        {
            CleanupOldPhoneNumbersTimestamps(smsSendDateTime, currentDateTime);
            return smsSendDateTime.Count;
        }

        public static void CleanupOldPhoneNumbersTimestamps(ConcurrentQueue<DateTime> smsSendDateTime, DateTime currentDateTime)
        {
            while (smsSendDateTime.TryPeek(out DateTime smsDateTime) &&
                   (currentDateTime - smsDateTime) > _timeSpan)
            {
                smsSendDateTime.TryDequeue(out _);
            }
        }

        public static void CleanupUnusedPhoneNumbers(
            ConcurrentDictionary<string, ITimeWindowCounter> timeWindowCounter,
            TimeSpan cleanupTimeSpan)
        {
            var currentDateTime = DateTime.UtcNow;

            foreach (var phoneNumberEntry in timeWindowCounter)
            {
                if ((currentDateTime - phoneNumberEntry.Value.LastUsed) > cleanupTimeSpan)
                {
                    timeWindowCounter.TryRemove(phoneNumberEntry.Key, out _);
                }
            }
        }
    }
}
