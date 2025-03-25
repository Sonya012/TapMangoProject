using Moq;
using TapMangoTakeHomeProject.Models;
using TapMangoTakeHomeProject.Services;
using TapMangoTakeHomeProject.Utilities;
using TapMangoProject.Interfaces;
using AutoFixture;
using TapMangoProject.Models;
using System.Collections.Concurrent;
using System;
using System.Security.Principal;

namespace TapMangoProject.Tests.Services
{
    public class RateLimiterServiceTests
    {
        private readonly Mock<ITimeWindowCounter> _mockTimeWindowCounter;
        private readonly Mock<ITimeWindowCounter> _mockAccountWindowCounter;
        private readonly RateLimiterService _rateLimiterService;
        private readonly Fixture _fixture = new();

        public RateLimiterServiceTests()
        {
            _mockTimeWindowCounter = new Mock<ITimeWindowCounter>();
            _mockAccountWindowCounter = new Mock<ITimeWindowCounter>();

            _rateLimiterService = new RateLimiterService(
                timeWindowCounter: _ => _mockTimeWindowCounter.Object,
                accountWindowCounter: _ => _mockAccountWindowCounter.Object
            );
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldReturnTrue_WhenWithinLimits()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var result = _rateLimiterService.IfNumberCanSendSMS(number);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrowCooldownException_WhenCooldownNotPassed()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,

            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            _rateLimiterService.IfNumberCanSendSMS(number);

            var smsSendException = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.CooldownTimeExceeded, smsSendException.ErrorCode);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrowRateLimitExceededForNumber_WhenNumberLimitExceeded()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 1
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(999);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var firstReault = _rateLimiterService.IfNumberCanSendSMS(number);

            Thread.Sleep(1100);

            var smsSendException = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.RateLimitExceededForNumber, smsSendException.ErrorCode);
            Assert.True(firstReault);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrowRateLimitExceededForAccount_WhenAccountLimitExceeded()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(999);

            // Act
            var smsSendException = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.RateLimitExceededForAccount, smsSendException.ErrorCode);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldAccumulateChecksAcrossMultipleNumbers_ForSameAccount()
        {
            // Arrange
            var accountId = _fixture.Create<Guid>();

            var firstNumber = new Number
            {
                AccountNumber = accountId,
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };
            var secondNumber = new Number
            {
                AccountNumber = accountId,
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(x => x.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(x => x.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var firstResult = _rateLimiterService.IfNumberCanSendSMS(firstNumber);

            Thread.Sleep(1100);

            var secondResult = _rateLimiterService.IfNumberCanSendSMS(secondNumber);

            var account = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == accountId);

            // Assert
            Assert.True(firstResult);
            Assert.True(secondResult);

            Assert.NotNull(account);
            Assert.Equal(2, account!.AccountNumberOfChecks);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldAddNewNumber_WhenAccountExistsButNumberDoesNot()
        {
            // Arrange
            var accountId = _fixture.Create<Guid>();
            var existingNumber = _fixture.Create<Number>();
            existingNumber.AccountNumber = accountId;
            existingNumber.PhoneNumber = _fixture.Create<string>();

            var newNumber = _fixture.Create<Number>();
            newNumber.AccountNumber = accountId;
            newNumber.PhoneNumber = _fixture.Create<string>();

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            _rateLimiterService.IfNumberCanSendSMS(existingNumber);

            Thread.Sleep(1100);

            // Act
            var result = _rateLimiterService.IfNumberCanSendSMS(newNumber);

            // Assert
            Assert.True(result);

            var account = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == accountId);

            Assert.NotNull(account);
            Assert.Contains(account!.Numbers, n => n.PhoneNumber == newNumber.PhoneNumber);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrow_WhenAllLimitsAreZero()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 0
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var ex = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.RateLimitExceededForNumber, ex.ErrorCode);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrowCooldownException_OnRapidRepeat()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var result = _rateLimiterService.IfNumberCanSendSMS(number);
            var ex = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.True(result);
            Assert.Equal(PhoneNumberCanSendResponseErrors.CooldownTimeExceeded, ex.ErrorCode);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldIncrementNumberOfChecks()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var firstResult = _rateLimiterService.IfNumberCanSendSMS(number);

            Thread.Sleep(1100);

            var secondResult = _rateLimiterService.IfNumberCanSendSMS(number);

            var storedNumber = _rateLimiterService.GetAccounts()
                .SelectMany(a => a.Numbers)
                .FirstOrDefault(n => n.PhoneNumber == number.PhoneNumber);

            // Assert
            Assert.True(firstResult);
            Assert.True(secondResult);
            Assert.NotNull(storedNumber);
            Assert.Equal(2, storedNumber!.NumberOfChecks);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldNotAffectOtherAccounts()
        {
            // Arrange
            var firstAccountId = _fixture.Create<Guid>();
            var secondAccountId = _fixture.Create<Guid>();

            var firstNumber = new Number
            {
                AccountNumber = firstAccountId,
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            var secondNumber = new Number
            {
                AccountNumber = secondAccountId,
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var result1 = _rateLimiterService.IfNumberCanSendSMS(firstNumber);
            var result2 = _rateLimiterService.IfNumberCanSendSMS(secondNumber);

            var account1 = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == firstAccountId);
            var account2 = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == secondAccountId);


            // Assert
            Assert.True(result1);
            Assert.True(result2);

            Assert.NotNull(account1);
            Assert.NotNull(account2);
            Assert.Single(account1!.Numbers);
            Assert.Single(account2!.Numbers);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldAllowAfterCooldown()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var firstCall = _rateLimiterService.IfNumberCanSendSMS(number);

            Thread.Sleep(1100);

            var secondCall = _rateLimiterService.IfNumberCanSendSMS(number);

            // Assert
            Assert.True(firstCall);
            Assert.True(secondCall);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldFailTwice_OnTripleRapidCalls()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var firstCall = _rateLimiterService.IfNumberCanSendSMS(number);

            var ex1 = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));
            var ex2 = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.True(firstCall);
            Assert.Equal(PhoneNumberCanSendResponseErrors.CooldownTimeExceeded, ex1.ErrorCode);
            Assert.Equal(PhoneNumberCanSendResponseErrors.CooldownTimeExceeded, ex2.ErrorCode);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrow_WhenNumberIsInactive()
        {
            // Arrange
            var accountId = _fixture.Create<Guid>();
            var phoneNumber = _fixture.Create<string>();

            var number = new Number
            {
                AccountNumber = accountId,
                PhoneNumber = phoneNumber,
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            _rateLimiterService.IfNumberCanSendSMS(number);

            var storedAccount = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == accountId);
            var storedNumber = storedAccount?.Numbers.FirstOrDefault(n => n.PhoneNumber == phoneNumber);
            storedNumber!.Active = false;

            // Act
            var ex = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.NumberIsInactive, ex.ErrorCode);
        }


        [Fact]
        public void IfNumberCanSendSMS_ShouldCallIncrementOnCounters()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            _rateLimiterService.IfNumberCanSendSMS(number);

            // Assert
            _mockTimeWindowCounter.Verify(c => c.Increment(It.IsAny<DateTime>()), Times.Once);
            _mockAccountWindowCounter.Verify(c => c.Increment(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public void AccountNumberOfChecks_ShouldBeAccurate()
        {
            // Arrange
            Guid accountId = _fixture.Create<Guid>();

            var account = new Account
            {
                AccountNumber = accountId,
                AccountLimit = 10,
                Numbers = new List<Number>
        {
            new Number { AccountNumber = accountId, PhoneNumber = _fixture.Create<string>(), NumberOfChecks = 2 },
            new Number { AccountNumber = accountId, PhoneNumber = _fixture.Create<string>(), NumberOfChecks = 3 },
            new Number { AccountNumber = accountId, PhoneNumber = _fixture.Create<string>(), NumberOfChecks = 5 }
        }
            };

            // Act
            var totalChecks = account.AccountNumberOfChecks;

            // Assert
            Assert.Equal(10, totalChecks);
        }

        [Fact]
        public void CleanupOldPhoneNumbersTimestamps_ShouldRemoveOldItems()
        {
            // Arrange
            var currentDateTime = DateTime.UtcNow;
            var queue = new ConcurrentQueue<DateTime>(new[]
            {
                currentDateTime.AddSeconds(-5),
                currentDateTime.AddSeconds(-3), 
                currentDateTime,             
                currentDateTime               
            });

            // Act
            CleanUpTimeControl.CleanupOldPhoneNumbersTimestamps(queue, currentDateTime);

            // Assert
            Assert.Equal(2, queue.Count);
        }

        [Fact]
        public void CleanupOldUnusedPhoneNumbers_ShouldNotThrow()
        {
            var method = typeof(RateLimiterService).GetMethod(
                "CleanupOldUnusedPhoneNumbers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            var ex = Record.Exception(() => method!.Invoke(_rateLimiterService, new object?[] { null }));

            Assert.Null(ex);
        }

        [Fact]
        public void GetAccount_ShouldReturnAccount_WhenItExists()
        {
            // Arrange
            var accountId = _fixture.Create<Guid>();
            var number = new Number
            {
                AccountNumber = accountId,
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act 
            _rateLimiterService.IfNumberCanSendSMS(number);
            var result = _rateLimiterService.GetAccounts().FirstOrDefault(x => x.AccountNumber == accountId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(accountId, result!.AccountNumber);
        }

        [Fact]
        public void GetAccount_ShouldReturnNull_WhenAccountDoesNotExist()
        {
            // Arrange / Act
            var result = _rateLimiterService.GetAccounts();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetNumber_ShouldReturnNumber_WhenItExists()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            // Act 
            _rateLimiterService.IfNumberCanSendSMS(number);
            var result = _rateLimiterService.GetAccounts()
                                            .SelectMany(a => a.Numbers)
                                            .FirstOrDefault(n => n.PhoneNumber == number.PhoneNumber);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(number.PhoneNumber, result!.PhoneNumber);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldUpdateLastSMSCheckTime_OnSuccess()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true
            };

            _mockTimeWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(c => c.Count(It.IsAny<DateTime>())).Returns(0);

            // Act
            var before = DateTime.UtcNow;
            _rateLimiterService.IfNumberCanSendSMS(number);
            var storedNumber = _rateLimiterService.GetAccounts()
                                                  .SelectMany(a => a.Numbers)
                                                  .FirstOrDefault(n => n.PhoneNumber == number.PhoneNumber);

            // Assert
            Assert.NotNull(storedNumber);
            Assert.True(storedNumber!.LastSMSCheckTime >= before);
            Assert.True(storedNumber.LastSMSCheckTime <= DateTime.UtcNow);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldUpdateLastSMSCheckTime_AfterSuccessfulCheck()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            var beforeCall = DateTime.UtcNow;

            // Act
            _rateLimiterService.IfNumberCanSendSMS(number);

            var storedNumber = _rateLimiterService.GetAccounts()
                .SelectMany(a => a.Numbers)
                .FirstOrDefault(n => n.PhoneNumber == number.PhoneNumber);

            // Assert
            Assert.NotNull(storedNumber);
            Assert.True(storedNumber!.LastSMSCheckTime >= beforeCall, "LastSMSCheckTime should be updated after call.");
            Assert.True(storedNumber.LastSMSCheckTime <= DateTime.UtcNow, "LastSMSCheckTime should not be in the future.");
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldNotUpdateLastSMSCheckTime_WhenInCooldown()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            _rateLimiterService.IfNumberCanSendSMS(number);

            var storedNumber = _rateLimiterService.GetAccounts()
                .SelectMany(a => a.Numbers)
                .First(n => n.PhoneNumber == number.PhoneNumber);

            var firstTimestamp = storedNumber.LastSMSCheckTime;

            // Act
            var ex = Assert.Throws<PhoneNumberSMSCheckException>(() => _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(firstTimestamp, storedNumber.LastSMSCheckTime);
        }

        /// <summary>
        /// Used for adding sample accounts on loadup of the webservice. 
        /// </summary>
        [Fact]
        public void InitializeAccounts_ShouldReplaceExistingAccounts()
        {
            // Arrange
            var originalAccount = new Account
            {
                AccountNumber = Guid.NewGuid(),
                AccountLimit = 1
            };

            _rateLimiterService.InitializeAccounts(new List<Account> { originalAccount });

            var newAccount = new Account
            {
                AccountNumber = Guid.NewGuid(),
                AccountLimit = 5
            };

            // Act
            _rateLimiterService.InitializeAccounts(new List<Account> { newAccount });

            // Assert
            var accounts = _rateLimiterService.GetAccounts();
            Assert.Single(accounts);
            Assert.Equal(newAccount.AccountNumber, accounts[0].AccountNumber);
        }

        [Fact]
        public void IfNumberCanSendSMS_ShouldThrow_WhenInactiveNumberIsFirstCheck()
        {
            // Arrange
            var number = new Number
            {
                AccountNumber = _fixture.Create<Guid>(),
                PhoneNumber = _fixture.Create<string>(),
                PersonalNumberLimit = 5,
                Active = true 
            };

            _mockTimeWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);
            _mockAccountWindowCounter.Setup(_ => _.Count(It.IsAny<DateTime>())).Returns(0);

            _rateLimiterService.IfNumberCanSendSMS(number);

            var storedAccount = _rateLimiterService.GetAccounts()
                .FirstOrDefault(a => a.AccountNumber == number.AccountNumber);

            var storedNumber = storedAccount?.Numbers.FirstOrDefault(n => n.PhoneNumber == number.PhoneNumber);
            storedNumber!.Active = false;

            Thread.Sleep(1100);

            // Act
            var ex = Assert.Throws<PhoneNumberSMSCheckException>(() =>
                _rateLimiterService.IfNumberCanSendSMS(number));

            // Assert
            Assert.Equal(PhoneNumberCanSendResponseErrors.NumberIsInactive, ex.ErrorCode);
        }



    }
}
