namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class TransferMoney(IAccountRepository accountRepository, INotificationService notificationService, IUnitOfWork unitOfWork)
{
    public void Execute(Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        try
        {
            unitOfWork.BeginTransaction();

            var from = accountRepository.GetAccountById(fromAccountId);
            var to = accountRepository.GetAccountById(toAccountId);

            from.Withdraw(amount);
            to.Deposit(amount);

            accountRepository.Update(from);
            accountRepository.Update(to);

            unitOfWork.Commit();

            if (from.HasLowBalance)
            {
                notificationService.NotifyFundsLow(from.User.Email);
            }

            if (to.IsApproachingPayInLimit)
            {
                notificationService.NotifyApproachingPayInLimit(to.User.Email);
            }
        }
        catch
        {
            unitOfWork.Rollback();
            throw;
        }
    }
}
