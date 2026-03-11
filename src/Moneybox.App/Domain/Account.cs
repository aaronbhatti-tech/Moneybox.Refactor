namespace Moneybox.App.Domain;

using System;

public class Account
{
    public const decimal MaxPayInAmount = 4000m;
    private const decimal MinBalanceAmount = 500m;
    private const decimal MaxPayInAmountWarningMargin = 500m;

    public Guid Id { get; set; }

    public User User { get; set; }

    public decimal Balance { get; set; }

    public decimal Withdrawn { get; set; }

    public decimal PaidIn { get; set; }

    internal bool Deposit(decimal amount)
    {
        var newPaidIn = PaidIn + amount;

        if (newPaidIn > MaxPayInAmount)
        {
            throw new InvalidOperationException("Account pay in limit reached");
        }

        Balance += amount;
        PaidIn = newPaidIn;

        return (MaxPayInAmount - newPaidIn) < MaxPayInAmountWarningMargin;
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

        return newBalance < MinBalanceAmount;
    }
}