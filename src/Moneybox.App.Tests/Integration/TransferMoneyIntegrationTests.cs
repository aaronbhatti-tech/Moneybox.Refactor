using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using Moneybox.App.Features;
using Moq;
using System;
using System.Collections.Generic;

namespace Moneybox.App.Tests.Integration;

[TestClass]
public class TransferMoneyIntegrationTests
{
    private InMemoryAccountRepository _accountRepository;
    private Mock<INotificationService> _mockNotificationService;
    private TransferMoney _transferMoney;

    [TestInitialize]
    public void Setup()
    {
        _accountRepository = new InMemoryAccountRepository();
        _mockNotificationService = new Mock<INotificationService>();
        _transferMoney = new TransferMoney(_accountRepository, _mockNotificationService.Object);
    }

    [TestMethod]
    public void CompleteTransferScenario_MultipleTransfers_MaintainsDataIntegrity()
    {
        // Arrange
        var account1 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user1@test.com", Name = "User 1" },
            Balance = 3000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var account2 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user2@test.com", Name = "User 2" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var account3 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user3@test.com", Name = "User 3" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        _accountRepository.AddAccount(account1);
        _accountRepository.AddAccount(account2);
        _accountRepository.AddAccount(account3);

        // Act - Perform multiple transfers
        _transferMoney.Execute(account1.Id, account2.Id, 1000m);
        _transferMoney.Execute(account2.Id, account3.Id, 500m);
        _transferMoney.Execute(account1.Id, account3.Id, 500m);

        // Assert
        var updated1 = _accountRepository.GetAccountById(account1.Id);
        var updated2 = _accountRepository.GetAccountById(account2.Id);
        var updated3 = _accountRepository.GetAccountById(account3.Id);

        Assert.AreEqual(1500m, updated1.Balance); // 3000 - 1000 - 500
        Assert.AreEqual(1500m, updated1.Withdrawn);
        
        Assert.AreEqual(1500m, updated2.Balance); // 1000 + 1000 - 500
        Assert.AreEqual(500m, updated2.Withdrawn);
        Assert.AreEqual(1000m, updated2.PaidIn);
        
        Assert.AreEqual(1500m, updated3.Balance); // 500 + 500 + 500
        Assert.AreEqual(0m, updated3.Withdrawn);
        Assert.AreEqual(1000m, updated3.PaidIn);
    }

    [TestMethod]
    public void TransferToPayInLimit_TriggersNotificationSequence()
    {
        // Arrange
        var fromAccount = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 5000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 3000m
        };

        _accountRepository.AddAccount(fromAccount);
        _accountRepository.AddAccount(toAccount);

        // Act - First transfer: within 500 of limit
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 600m);

        // Assert - First notification
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit("to@test.com"), Times.Once);

        var updated1 = _accountRepository.GetAccountById(toAccount.Id);
        Assert.AreEqual(3600m, updated1.PaidIn);

        // Act - Second transfer: exactly at limit
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 400m);

        // Assert - Second notification
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit("to@test.com"), Times.Exactly(2));

        var updated2 = _accountRepository.GetAccountById(toAccount.Id);
        Assert.AreEqual(4000m, updated2.PaidIn);
    }

    [TestMethod]
    public void TransferCausingLowBalance_ThenFailsOnInsufficientFunds()
    {
        // Arrange
        var fromAccount = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 600m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        _accountRepository.AddAccount(fromAccount);
        _accountRepository.AddAccount(toAccount);

        // Act - First transfer causes low balance
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 200m);

        // Assert
        var updated = _accountRepository.GetAccountById(fromAccount.Id);
        Assert.AreEqual(400m, updated.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow("from@test.com"), Times.Once);

        // Act & Assert - Second transfer should fail
        Assert.ThrowsException<InvalidOperationException>(() =>
            _transferMoney.Execute(fromAccount.Id, toAccount.Id, 500m));
    }

    [TestMethod]
    public void AccountPersistence_UpdatesAreReflectedInSubsequentReads()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            User = new User { Id = Guid.NewGuid(), Email = "user@test.com", Name = "User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var otherAccountId = Guid.NewGuid();
        var otherAccount = new Account
        {
            Id = otherAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "other@test.com", Name = "Other" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        _accountRepository.AddAccount(account);
        _accountRepository.AddAccount(otherAccount);

        // Act - First transfer
        _transferMoney.Execute(accountId, otherAccountId, 200m);

        // Assert - After first transfer
        var afterFirstTransfer = _accountRepository.GetAccountById(accountId);
        Assert.AreEqual(800m, afterFirstTransfer.Balance);
        Assert.AreEqual(200m, afterFirstTransfer.Withdrawn);

        // Act - Second transfer
        _transferMoney.Execute(accountId, otherAccountId, 150m);

        // Assert - After second transfer
        var afterSecondTransfer = _accountRepository.GetAccountById(accountId);
        Assert.AreEqual(650m, afterSecondTransfer.Balance);
        Assert.AreEqual(350m, afterSecondTransfer.Withdrawn);
    }

    [TestMethod]
    public void CircularTransfers_MaintainCorrectBalances()
    {
        // Arrange
        var account1 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user1@test.com", Name = "User 1" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var account2 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user2@test.com", Name = "User 2" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var account3 = new Account
        {
            Id = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), Email = "user3@test.com", Name = "User 3" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        _accountRepository.AddAccount(account1);
        _accountRepository.AddAccount(account2);
        _accountRepository.AddAccount(account3);

        // Act - Circular transfers: 1 -> 2 -> 3 -> 1
        _transferMoney.Execute(account1.Id, account2.Id, 100m);
        _transferMoney.Execute(account2.Id, account3.Id, 100m);
        _transferMoney.Execute(account3.Id, account1.Id, 100m);

        // Assert
        var updated1 = _accountRepository.GetAccountById(account1.Id);
        var updated2 = _accountRepository.GetAccountById(account2.Id);
        var updated3 = _accountRepository.GetAccountById(account3.Id);

        // All balances should be back to 1000m
        Assert.AreEqual(1000m, updated1.Balance);
        Assert.AreEqual(1000m, updated2.Balance);
        Assert.AreEqual(1000m, updated3.Balance);

        // Each account withdrew and received 100m
        Assert.AreEqual(100m, updated1.Withdrawn);
        Assert.AreEqual(100m, updated2.Withdrawn);
        Assert.AreEqual(100m, updated3.Withdrawn);

        Assert.AreEqual(100m, updated1.PaidIn);
        Assert.AreEqual(100m, updated2.PaidIn);
        Assert.AreEqual(100m, updated3.PaidIn);
    }
}

/// <summary>
/// In-memory implementation of IAccountRepository for integration testing
/// </summary>
public class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<Guid, Account> _accounts = new();

    public void AddAccount(Account account)
    {
        _accounts[account.Id] = account;
    }

    public Account GetAccountById(Guid accountId)
    {
        if (_accounts.TryGetValue(accountId, out var account))
        {
            return account;
        }
        throw new InvalidOperationException($"Account {accountId} not found");
    }

    public void Update(Account account)
    {
        if (_accounts.ContainsKey(account.Id))
        {
            _accounts[account.Id] = account;
        }
        else
        {
            throw new InvalidOperationException($"Cannot update non-existent account {account.Id}");
        }
    }
}
