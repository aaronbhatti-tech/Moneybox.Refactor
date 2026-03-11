namespace Moneybox.App.Domain;

using System;

public class Account
{
    public const decimal PayInLimit = 4000m;
    private const decimal LowFundsThreshold = 500m;
    private const decimal ApproachingPayInLimitThreshold = 500m;

    public Guid Id { get; init; }

    public User User { get; init; }

    public decimal Balance { get; private set; }

    public decimal Withdrawn { get; private set; }

    public decimal PaidIn { get; private set; }

    internal bool Deposit(decimal amount)
    {
        var newPaidIn = PaidIn + amount;

        if (newPaidIn > PayInLimit)
        {
            throw new InvalidOperationException("Account pay in limit reached");
        }

        Balance += amount;
        PaidIn = newPaidIn;

        return (PayInLimit - newPaidIn) < ApproachingPayInLimitThreshold;
    }

    internal bool Withdraw(decimal amount)
    {
        decimal newBalance = Balance - amount;

        if (newBalance < 0m)
        {
            throw new InvalidOperationException("Insufficient funds to make withdrawal");
        }

        Balance = newBalance;
        Withdrawn += amount;

        return newBalance < LowFundsThreshold;
    }
}