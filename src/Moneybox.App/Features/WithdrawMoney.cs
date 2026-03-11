namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class WithdrawMoney(IAccountRepository accountRepository, INotificationService notificationService)
{
    public void Execute(Guid fromAccountId, decimal amount)
    {
        Account account = accountRepository.GetAccountById(fromAccountId);

        bool hasLowFunds = account.Withdraw(amount);

        if (hasLowFunds)
        {
            notificationService.NotifyFundsLow(account.User.Email);
        }

        accountRepository.Update(account);
    }
}