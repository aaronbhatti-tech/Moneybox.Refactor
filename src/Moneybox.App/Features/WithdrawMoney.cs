namespace Moneybox.App.Features;

using Moneybox.App.DataAccess;
using Moneybox.App.Domain;
using Moneybox.App.Domain.Services;
using System;

public class WithdrawMoney(IAccountRepository accountRepository, INotificationService notificationService, IUnitOfWork unitOfWork)
{
    public void Execute(Guid fromAccountId, decimal amount)
    {
        try
        {
            unitOfWork.BeginTransaction();

            var account = accountRepository.GetAccountById(fromAccountId);

            account.Withdraw(amount);

            accountRepository.Update(account);

            unitOfWork.Commit();

            if (account.HasLowBalance)
            {
                notificationService.NotifyFundsLow(account.User.Email);
            }
        }
        catch
        {
            unitOfWork.Rollback();
            throw;
        }
    }
}