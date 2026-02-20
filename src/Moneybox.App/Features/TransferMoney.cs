using Moneybox.App.DataAccess;
using Moneybox.App.Domain.Services;
using System;
using Moneybox.App.Domain;

namespace Moneybox.App.Features;

public class TransferMoney(IAccountRepository accountRepository, INotificationService notificationService)
{
    public void Execute(Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        var from = accountRepository.GetAccountById(fromAccountId);
        var to = accountRepository.GetAccountById(toAccountId);

        var fromBalance = from.Balance - amount;
        if (fromBalance < 0m)
        {
            throw new InvalidOperationException("Insufficient funds to make transfer");
        }

        if (fromBalance < 500m)
        {
            notificationService.NotifyFundsLow(from.User.Email);
        }

        var paidIn = to.PaidIn + amount;
        if (paidIn > Account.PayInLimit)
        {
            throw new InvalidOperationException("Account pay in limit reached");
        }

        if (Account.PayInLimit - paidIn < 500m)
        {
            notificationService.NotifyApproachingPayInLimit(to.User.Email);
        }

        from.Balance = from.Balance - amount;
        from.Withdrawn = from.Withdrawn + amount;

        to.Balance = to.Balance + amount;
        to.PaidIn = to.PaidIn + amount;

        accountRepository.Update(from);
        accountRepository.Update(to);
    }
}
