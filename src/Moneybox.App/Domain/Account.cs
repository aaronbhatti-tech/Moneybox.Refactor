namespace Moneybox.App.Domain;

using System;

public class Account
{
    public const decimal MaxPayInAmount = 4000m;
    private const decimal MinBalanceAmount = 500m;
    private const decimal MaxPayInAmountWarningMargin = 500m;

    public Guid Id { get; init; }

    public User User { get; init; }

    public decimal Balance { get; private set; }

    public decimal Withdrawn { get; private set; }

    public decimal PaidIn { get; private set; }

    public bool HasLowBalance => Balance < MinBalanceAmount;

    public bool IsApproachingPayInLimit => 
        (MaxPayInAmount - PaidIn) < MaxPayInAmountWarningMargin;

    public Account()
    {
    }

    public Account(Guid id, User user, decimal balance, decimal withdrawn, decimal paidIn)
    {
        Id = id;
        User = user;
        Balance = balance;
        Withdrawn = withdrawn;
        PaidIn = paidIn;
    }

    internal void Deposit(decimal amount)
    {
        var newPaidIn = PaidIn + amount;

        if (newPaidIn > MaxPayInAmount)
        {
            throw new InvalidOperationException("Account pay in limit reached");
        }

        Balance += amount;
        PaidIn = newPaidIn;
    }

    internal void Withdraw(decimal amount)
    {
        decimal newBalance = Balance - amount;

        if (newBalance < 0m)
        {
            throw new InvalidOperationException("Insufficient funds to make withdrawal");
        }

        Balance = newBalance;
        Withdrawn += amount;
    }
}