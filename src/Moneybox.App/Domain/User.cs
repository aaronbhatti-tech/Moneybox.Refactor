using System;

namespace Moneybox.App.Domain;

public class User
{
    public Guid Id { get; init; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public User()
    {
    }

    public User(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }
}