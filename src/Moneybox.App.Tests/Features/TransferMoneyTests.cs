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
    public void Execute_SuccessfulTransfer_UpdatesBothAccounts()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

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
    public void Execute_FromAccountBalanceFallsBelow500_SendsLowFundsNotification()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromEmail = "from@test.com";

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = fromEmail, Name = "From User" },
            Balance = 600m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 200m);

        // Assert
        Assert.AreEqual(400m, fromAccount.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(fromEmail), Times.Once);
    }

    [TestMethod]
    public void Execute_FromAccountExactly500_DoesNotSendLowFundsNotification()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 700m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 200m);

        // Assert
        Assert.AreEqual(500m, fromAccount.Balance);
        _mockNotificationService.Verify(x => x.NotifyFundsLow(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void Execute_ToAccountApproachingPayInLimit_SendsPayInLimitNotification()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var toEmail = "to@test.com";

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3600m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 100m);

        // Assert
        Assert.AreEqual(3700m, toAccount.PaidIn);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(toEmail), Times.Once);
    }

    [TestMethod]
    public void Execute_ToAccountExactly500FromLimit_DoesNotSendNotification()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 500m);

        // Assert
        Assert.AreEqual(3500m, toAccount.PaidIn);
        _mockNotificationService.Verify(x => x.NotifyApproachingPayInLimit(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void Execute_BothThresholdsTriggered_SendsBothNotifications()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var fromEmail = "from@test.com";
        var toEmail = "to@test.com";

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = fromEmail, Name = "From User" },
            Balance = 600m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3600m
        };

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
    public void Execute_InsufficientFunds_ThrowsException()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 100m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act & Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            _transferMoney.Execute(fromAccountId, toAccountId, 200m));

        Assert.AreEqual("Insufficient funds to make withdrawal", exception.Message);
    }

    [TestMethod]
    public void Execute_InsufficientFunds_DoesNotUpdateAccounts()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 100m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

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

        // Assert - Balances should not change
        Assert.AreEqual(100m, fromAccount.Balance);
        Assert.AreEqual(500m, toAccount.Balance);
        _mockAccountRepository.Verify(x => x.Update(It.IsAny<Account>()), Times.Never);
    }

    [TestMethod]
    public void Execute_PayInLimitExceeded_ThrowsException()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3900m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act & Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            _transferMoney.Execute(fromAccountId, toAccountId, 200m));
        
        Assert.AreEqual("Account pay in limit reached", exception.Message);
    }

    [TestMethod]
    public void Execute_PayInLimitExceeded_DoesNotUpdateAccounts()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3900m
        };

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
    public void Execute_ExactlyAtPayInLimit_SucceedsAndSendsNotification()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var toEmail = "to@test.com";

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = toEmail, Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 3500m
        };

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
    public void Execute_LoadsBothAccountsFromRepository()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 100m);

        // Assert
        _mockAccountRepository.Verify(x => x.GetAccountById(fromAccountId), Times.Once);
        _mockAccountRepository.Verify(x => x.GetAccountById(toAccountId), Times.Once);
    }

    [TestMethod]
    public void Execute_TransferToSelf_WorksCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            User = new User { Id = Guid.NewGuid(), Email = "user@test.com", Name = "User" },
            Balance = 2000m,
            Withdrawn = 100m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(accountId)).Returns(account);

        // Act
        _transferMoney.Execute(accountId, accountId, 100m);

        // Assert - Withdrawn and PaidIn both increase
        Assert.AreEqual(2000m, account.Balance); // No net change
        Assert.AreEqual(200m, account.Withdrawn); // 100 + 100
        Assert.AreEqual(1100m, account.PaidIn); // 1000 + 100
    }

    [TestMethod]
    public void Execute_ZeroAmount_CompletesSuccessfully()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();

        var fromAccount = new Account
        {
            Id = fromAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "from@test.com", Name = "From User" },
            Balance = 1000m,
            Withdrawn = 0m,
            PaidIn = 0m
        };

        var toAccount = new Account
        {
            Id = toAccountId,
            User = new User { Id = Guid.NewGuid(), Email = "to@test.com", Name = "To User" },
            Balance = 500m,
            Withdrawn = 0m,
            PaidIn = 1000m
        };

        _mockAccountRepository.Setup(x => x.GetAccountById(fromAccountId)).Returns(fromAccount);
        _mockAccountRepository.Setup(x => x.GetAccountById(toAccountId)).Returns(toAccount);

        // Act
        _transferMoney.Execute(fromAccountId, toAccountId, 0m);

        // Assert - No changes
        Assert.AreEqual(1000m, fromAccount.Balance);
        Assert.AreEqual(500m, toAccount.Balance);
        _mockAccountRepository.Verify(x => x.Update(It.IsAny<Account>()), Times.Exactly(2));
    }
}
