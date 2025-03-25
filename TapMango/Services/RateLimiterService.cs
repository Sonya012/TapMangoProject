using System.Collections.Concurrent;
using TapMangoProject.Interfaces;
using TapMangoProject.Models;
using TapMangoTakeHomeProject.Models;
using TapMangoTakeHomeProject.Utilities;

namespace TapMangoTakeHomeProject.Services
{
    public class RateLimiterService : IRateLimiterService
    {
        private readonly Func<string, ITimeWindowCounter> _timeWindowCounter;
        private readonly Func<string, ITimeWindowCounter> _accountWindowCounter;
        private readonly Timer _cleanupOldPhoneNumberTimer;
        private readonly TimeSpan _cleanupPhonesNumbersInterval = TimeSpan.FromMinutes(60);
        private readonly ConcurrentDictionary<string, ITimeWindowCounter> _numberCounter = new();
        private readonly ConcurrentDictionary<Guid, ITimeWindowCounter> _accountCounter = new();
        private readonly ConcurrentDictionary<Guid, Account> _accounts = new();
        private const int COOLDOWNTIME = 1;

        public RateLimiterService(
            Func<string, ITimeWindowCounter>? timeWindowCounter = null,
            Func<string, ITimeWindowCounter>? accountWindowCounter = null)
        {
            _timeWindowCounter = timeWindowCounter ?? (_ => new TimeWindowCounter());
            _accountWindowCounter = accountWindowCounter ?? (_ => new TimeWindowCounter());

            _cleanupOldPhoneNumberTimer = new Timer(
                CleanupOldUnusedPhoneNumbers,
                null,
                _cleanupPhonesNumbersInterval,
                _cleanupPhonesNumbersInterval
            );
        }

        public void InitializeAccounts(List<Account> accounts)
        {
            _accounts.Clear();

            foreach (var account in accounts)
            {
                _accounts[account.AccountNumber] = account;
            }
        }

        /// <summary>
        /// Determines whether the specified phone number is allowed to send an SMS at this time.
        ///
        /// A number may be blocked from sending if any of the following conditions are met:
        /// 1. A recent check was made too soon (cooldown period not satisfied).
        /// 2. The account-wide message limit per second has been exceeded.
        /// 3. The per-number message limit per second has been exceeded.
        /// </summary>
        /// <param name="number">The phone number details to be validated.</param>
        /// <returns>True if the SMS can be sent; otherwise, throws an exception.</returns>
        /// <exception cref="PhoneNumberSMSCheckException">
        /// Thrown when the number fails one of the SMS send eligibility checks.
        /// </exception>
        public bool IfNumberCanSendSMS(Number number)
        {
            DateTime currentDateTime = DateTime.UtcNow;

            var numberCounter = _numberCounter.GetOrAdd(
                number.PhoneNumber,
                _ => _timeWindowCounter(number.PhoneNumber)
            );

            var account = _accounts.GetOrAdd(number.AccountNumber, _ => new Account
            {
                AccountNumber = number.AccountNumber,
                AccountLimit = number.PersonalNumberLimit,
                Numbers = new List<Number>()
            });

            var existingNumber = account.Numbers.FirstOrDefault(n => n.AccountNumber == number.AccountNumber && n.PhoneNumber == number.PhoneNumber);

            if (existingNumber == null)
            {
                existingNumber = new Number
                {
                    AccountNumber = account.AccountNumber,
                    PhoneNumber = number.PhoneNumber,
                    LastSMSCheckTime = DateTime.MinValue,
                    NumberOfChecks = 0,
                    PersonalNumberLimit = number.PersonalNumberLimit,
                };
                account.Numbers.Add(existingNumber);
            }

            var accountCounter = _accountCounter.GetOrAdd(
                account.AccountNumber,
                _ => _accountWindowCounter(account.AccountNumber.ToString())
            );

            //Check if number is inactive
            if(existingNumber.Active == false)
            {
                account.Numbers.Remove(existingNumber);
                _numberCounter.TryRemove(existingNumber.PhoneNumber, out _);

                throw new PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors.NumberIsInactive);
            }

            // Cooldown Check
            if ((currentDateTime - existingNumber.LastSMSCheckTime).TotalSeconds < COOLDOWNTIME ||
                (currentDateTime - account.Numbers.Min(n => n.LastSMSCheckTime)).TotalSeconds < COOLDOWNTIME)
            {
                throw new PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors.CooldownTimeExceeded);
            }

            // Check Number Limit
            if (existingNumber.NumberOfChecks >= existingNumber.PersonalNumberLimit)
            {
                throw new PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors.RateLimitExceededForNumber);
            }

            // Check Account Limit
            if (accountCounter.Count(currentDateTime) >= account.AccountLimit)
            {
                throw new PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors.RateLimitExceededForAccount);
            }

            existingNumber.LastSMSCheckTime = currentDateTime;
            existingNumber.NumberOfChecks++;
            numberCounter.Increment(currentDateTime);
            accountCounter.Increment(currentDateTime);

            return true;
        }

        public List<Account> GetAccounts()
        {
            return _accounts.Values.ToList();
        }

        /// <summary>
        /// Background process that periodically cleans up old, unused phone number counters
        /// to free memory and maintain performance.
        /// </summary>
        /// <param name="state">Unused state object required by the Timer callback signature.</param>
        internal void CleanupOldUnusedPhoneNumbers(object? state)
        {
            CleanUpTimeControl.CleanupUnusedPhoneNumbers(_numberCounter, _cleanupPhonesNumbersInterval);
        }
    }
}
