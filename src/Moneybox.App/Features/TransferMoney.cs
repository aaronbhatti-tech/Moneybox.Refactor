namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class TransferMoney(IAccountRepository accountRepository, INotificationService notificationService)
{
    public void Execute(Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        Account from = accountRepository.GetAccountById(fromAccountId);
        Account to = accountRepository.GetAccountById(toAccountId);

        var hasLowFunds = from.Withdraw(amount);
        if (hasLowFunds)
        {
            notificationService.NotifyFundsLow(from.User.Email);
        }

        var isApproachingPayInLimit = to.Deposit(amount);
        if (isApproachingPayInLimit)
        {
            notificationService.NotifyApproachingPayInLimit(to.User.Email);
        }

        accountRepository.Update(from);
        accountRepository.Update(to);
    }
}
