using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using Moneybox.App.Features;
using Moq;
using System;

namespace Moneybox.App.Tests.Features;

[TestClass]
public class TransferMoneyTests
{
    private Mock<IAccountRepository> _mockAccountRepository;
    private Mock<INotificationService> _mockNotificationService;
    private TransferMoney _transferMoney;

    [TestInitialize]
    public void Setup()
    {
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockNotificationService = new Mock<INotificationService>();
        _transferMoney = new TransferMoney(_mockAccountRepository.Object, _mockNotificationService.Object);
    }

    [TestMethod]
    public void GivenSufficientFunds_WhenTransferringMoney_ThenBothAccountsAreUpdated()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromUser = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" };
        var toUser = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" };
        var fromAccount = new Account(fromAccountId, fromUser, 1000m, 0m, 0m);
        var toAccount = new Account(toAccountId, toUser, 500m, 0m, 1000m);
        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 300m);

        // Assert
        Assert.AreEqual(700m, fromAccount.Balance);
        Assert.AreEqual(300m, fromAccount.Withdrawn);
        Assert.AreEqual(800m, toAccount.Balance);
        Assert.AreEqual(1300m, toAccount.PaidIn);
        _mockAccountRepository.Verify(x => x.Update(fromAccount), Times.Once);
        _mockAccountRepository.Verify(x => x.Update(toAccount), Times.Once);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(It.IsAny<string>()), Times.Never);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GivenBalanceWillFallBelowMinBalance_WhenTransferringMoney_ThenLowFundsNotificationIsSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromEmail = "from@test.com";
        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = fromEmail, Name = "From User" }, 600m, 0m, 0m);
        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);
        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 200m);

        // Assert
        Assert.AreEqual(400m, fromAccount.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(fromEmail), Times.Once);
    }

    [TestMethod]
    public void GivenBalanceWillBeExactlyAtMinBalance_WhenTransferringMoney_ThenNoLowFundsNotificationIsSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 700m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 200m);

        // Assert
        Assert.AreEqual(500m, fromAccount.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GivenPayInWillBeWithinWarningMarginOfMaxPayIn_WhenTransferringMoney_ThenPayInLimitNotificationIsSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var toEmail = "to@test.com";

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" }, 500m, 0m, 3600m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 100m);

        // Assert
        Assert.AreEqual(3700m, toAccount.PaidIn);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(toEmail), Times.Once);
    }

    [TestMethod]
    public void GivenPayInWillBeExactlyAtWarningMarginFromMaxPayIn_WhenTransferringMoney_ThenNoPayInLimitNotificationIsSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 3000m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 500m);

        // Assert
        Assert.AreEqual(3500m, toAccount.PaidIn);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void GivenBothThresholdsWillBeTriggered_WhenTransferringMoney_ThenBothNotificationsAreSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromEmail = "from@test.com";
        var toEmail = "to@test.com";

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = fromEmail, Name = "From User" }, 600m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" }, 500m, 0m, 3600m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 200m);

        // Assert
        Assert.AreEqual(400m, fromAccount.Balance);
        Assert.AreEqual(3800m, toAccount.PaidIn);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(fromEmail), Times.Once);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(toEmail), Times.Once);
    }

    [TestMethod]
    public void GivenInsufficientFunds_WhenTransferringMoney_ThenInvalidOperationExceptionIsThrown()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 100m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act & Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            _transferMoney.Execute(fromAccountId, toAccountId, 200m));

        Assert.AreEqual("Insufficient funds to make withdrawal", exception.Message);
    }

    [TestMethod]
    public void GivenInsufficientFunds_WhenTransferAttempted_ThenAccountsAreNotUpdated()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 100m, 0m, 0m);
        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);
        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        try
        {
            _transferMoney.Execute(fromAccountId, toAccountId, 200m);
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }

        // Assert
        Assert.AreEqual(100m, fromAccount.Balance);
        Assert.AreEqual(500m, toAccount.Balance);
        _mockAccountRepository.Verify(x => x.Update(It.IsAny<Account>()), Times.Never);
    }

    [TestMethod]
    public void GivenMaxPayInAmountWillBeExceeded_WhenTransferringMoney_ThenInvalidOperationExceptionIsThrown()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 3900m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act & Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            _transferMoney.Execute(fromAccountId, toAccountId, 200m));
        
        Assert.AreEqual("Account pay in limit reached", exception.Message);
    }

    [TestMethod]
    public void GivenMaxPayInAmountWillBeExceeded_WhenTransferAttempted_ThenFromAccountIsModifiedButRepositoryNotUpdated()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 3900m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        try
        {
            _transferMoney.Execute(fromAccountId, toAccountId, 200m);
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }

        // Assert - From account is modified by Withdraw before Deposit fails
        Assert.AreEqual(800m, fromAccount.Balance); // Withdraw executed before exception
        Assert.AreEqual(200m, fromAccount.Withdrawn);
        Assert.AreEqual(500m, toAccount.Balance); // Deposit did not execute
        Assert.AreEqual(3900m, toAccount.PaidIn);
        _mockAccountRepository.Verify(x => x.Update(It.IsAny<Account>()), Times.Never);
    }

    [TestMethod]
    public void GivenPayInWillBeExactlyAtMaxPayInAmount_WhenTransferringMoney_ThenTransferSucceedsAndNotificationIsSent()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var toEmail = "to@test.com";

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" }, 500m, 0m, 3500m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 500m);

        // Assert
        Assert.AreEqual(4000m, toAccount.PaidIn);
        Assert.AreEqual(1000m, toAccount.Balance);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(toEmail), Times.Once);
        _mockAccountRepository.Verify(x => x.Update(toAccount), Times.Once);
    }

    [TestMethod]
    public void GivenTwoAccounts_WhenTransferringMoney_ThenBothAccountsAreLoadedFromRepository()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);

        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 100m);

        // Assert
        _mockAccountRepository.Verify(x => x.GetAccountById(fromAccountId), Times.Once);
        _mockAccountRepository.Verify(x => x.GetAccountById(toAccountId), Times.Once);
    }

    [TestMethod]
    public void GivenSameFromAndToAccount_WhenTransferringMoney_ThenWithdrawnAndPaidInBothIncrease()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account(accountId, new User { Id = Guid.NewGuid(), Email = "user@test.com", Name = "User" }, 2000m, 100m, 1000m);
        _mockAccountRepository.Setup(x => x.GetAccountById(accountId)).Returns(account);

        // Act
        _transferMoney.Execute(accountId, accountId, 100m);

        // Assert
        Assert.AreEqual(2000m, account.Balance);
        Assert.AreEqual(200m, account.Withdrawn);
        Assert.AreEqual(1100m, account.PaidIn);
    }

    [TestMethod]
    public void GivenZeroAmount_WhenTransferringMoney_ThenTransferCompletesWithoutChangingBalances()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromAccount = new Account(fromAccountId, new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" }, 1000m, 0m, 0m);
        var toAccount = new Account(toAccountId, new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" }, 500m, 0m, 1000m);
        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 0m);

        // Assert
        Assert.AreEqual(1000m, fromAccount.Balance);
        Assert.AreEqual(500m, toAccount.Balance);
        _mockAccountRepository.Verify(x => x.Update(It.IsAny<Account>()), Times.Exactly(2));
    }
}




