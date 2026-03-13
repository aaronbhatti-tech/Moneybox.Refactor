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
    private Mock<IUnitOfWork> _mockUnitOfWork;
    private TransferMoney _transferMoney;

    [TestInitialize]
    public void Setup()
    {
        _accountRepository = new InMemoryAccountRepository();
        _mockNotificationService = new Mock<INotificationService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _transferMoney = new TransferMoney(_accountRepository, _mockNotificationService.Object, _mockUnitOfWork.Object);
    }

    [TestMethod]
    public void GivenThreeAccounts_WhenPerformingMultipleTransfers_ThenDataIntegrityIsMaintained()
    {
        // Arrange
        var account1 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 1", "user1@test.com"), 3000m, 0m, 0m);

        var account2 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 2", "user2@test.com"), 1000m, 0m, 0m);

        var account3 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 3", "user3@test.com"), 500m, 0m, 0m);

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
    public void GivenTransfersApproachingMaxPayInAmount_WhenTransferringToLimit_ThenNotificationSequenceIsTriggered()
    {
        // Arrange
        var fromAccount = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "From User", "from@test.com"), 5000m, 0m, 0m);
        var toAccount = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "To User", "to@test.com"), 1000m, 0m, 3000m);
        _accountRepository.AddAccount(fromAccount);
        _accountRepository.AddAccount(toAccount);

        // Act
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 600m);
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 400m);

        // Assert
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit("to@test.com"), Times.Exactly(2));

        var updated = _accountRepository.GetAccountById(toAccount.Id);
        Assert.AreEqual(4000m, updated.PaidIn);
    }

    [TestMethod]
    public void GivenLowBalanceAfterFirstTransfer_WhenAttemptingSecondLargeTransfer_ThenInsufficientFundsExceptionIsThrown()
    {
        // Arrange
        var fromAccount = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "From User", "from@test.com"), 600m, 0m, 0m);
        var toAccount = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "To User", "to@test.com"), 1000m, 0m, 0m);
        _accountRepository.AddAccount(fromAccount);
        _accountRepository.AddAccount(toAccount);

        // Act
        _transferMoney.Execute(fromAccount.Id, toAccount.Id, 200m);

        // Assert
        var updated = _accountRepository.GetAccountById(fromAccount.Id);
        Assert.AreEqual(400m, updated.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow("from@test.com"), Times.Once);
        Assert.ThrowsException<InvalidOperationException>(() =>
            _transferMoney.Execute(fromAccount.Id, toAccount.Id, 500m));
    }

    [TestMethod]
    public void GivenMultipleTransfers_WhenReadingAccountFromRepository_ThenUpdatesAreReflectedInSubsequentReads()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId, new User(Guid.NewGuid(), "User", "user@test.com"), 1000m, 0m, 0m);
        var otherAccountId = Guid.NewGuid();
        var otherAccount = new Account(otherAccountId, new User(Guid.NewGuid(), "Other", "other@test.com"), 500m, 0m, 0m);
        _accountRepository.AddAccount(account);
        _accountRepository.AddAccount(otherAccount);

        // Act
        _transferMoney.Execute(accountId, otherAccountId, 200m);
        _transferMoney.Execute(accountId, otherAccountId, 150m);

        // Assert
        var finalAccount = _accountRepository.GetAccountById(accountId);
        Assert.AreEqual(650m, finalAccount.Balance);
        Assert.AreEqual(350m, finalAccount.Withdrawn);
    }

    [TestMethod]
    public void GivenThreeAccountsWithSameBalance_WhenPerformingCircularTransfers_ThenAllBalancesReturnToOriginalAmounts()
    {
        // Arrange
        var account1 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 1", "user1@test.com"), 1000m, 0m, 0m);
        var account2 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 2", "user2@test.com"), 1000m, 0m, 0m);
        var account3 = new Account(Guid.NewGuid(), new User(Guid.NewGuid(), "User 3", "user3@test.com"), 1000m, 0m, 0m);
        _accountRepository.AddAccount(account1);
        _accountRepository.AddAccount(account2);
        _accountRepository.AddAccount(account3);

        // Act
        _transferMoney.Execute(account1.Id, account2.Id, 100m);
        _transferMoney.Execute(account2.Id, account3.Id, 100m);
        _transferMoney.Execute(account3.Id, account1.Id, 100m);

        // Assert
        var updated1 = _accountRepository.GetAccountById(account1.Id);
        var updated2 = _accountRepository.GetAccountById(account2.Id);
        var updated3 = _accountRepository.GetAccountById(account3.Id);
        Assert.AreEqual(1000m, updated1.Balance);
        Assert.AreEqual(1000m, updated2.Balance);
        Assert.AreEqual(1000m, updated3.Balance);
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







