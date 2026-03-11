namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class WithdrawMoney(IAccountRepository accountRepository, INotificationService notificationService)
{
    public void Execute(Guid fromAccountId, decimal amount)
    {
        var account = accountRepository.GetAccountById(fromAccountId);

        account.Withdraw(amount);

        if (account.HasLowBalance)
        {
            notificationService.NotifyFundsLow(account.User.Email);
        }

        accountRepository.Update(account);
    }
}