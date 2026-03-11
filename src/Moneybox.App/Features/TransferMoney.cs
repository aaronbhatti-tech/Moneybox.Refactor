namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class TransferMoney(IAccountRepository accountRepository, INotificationService notificationService)
{
    public void Execute(Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        var from = accountRepository.GetAccountById(fromAccountId);
        var to = accountRepository.GetAccountById(toAccountId);

        from.Withdraw(amount);
        if (from.HasLowBalance)
        {
            notificationService.NotifyFundsLow(from.User.Email);
        }

        to.Deposit(amount);
        if (to.IsApproachingPayInLimit)
        {
            notificationService.NotifyApproachingPayInLimit(to.User.Email);
        }

        accountRepository.Update(from);
        accountRepository.Update(to);
    }
}
