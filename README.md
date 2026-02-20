# Moneybox Money Withdrawal

The solution contains a .NET core library (Moneybox.App) which is structured into the following 3 folders:

* Domain - this contains the domain models for a user and an account, and a notification service.
* Features - this contains two operations, one which is implemented (transfer money) and another which isn't (withdraw money).
* DataAccess - this contains a repository for retrieving and saving an account (and the nested user it belongs to).

## The task

The task is to implement a money withdrawal in the `WithdrawMoney.Execute(...)` method in the features folder. For consistency, the logic should be the same as the `TransferMoney.Execute(...)` method i.e. notifications for low funds and exceptions where the operation is not possible. 

As part of this process however, you should look to refactor some of the code in the `TransferMoney.Execute(...)` method into the domain models, and make these models less susceptible to misuse. We're looking to make our domain models rich in behaviour and much more than just plain old objects, however we don't want any data persistance operations (i.e. data access repositories) to bleed into our domain. This should simplify the task of implementing `WithdrawMoney.Execute(...)`.

## Guidelines

* You should aim to spend no more than 1 hour on this task.
* We recommend using the Git interface to clone the repository locally and using an IDE such as Visual Studio or JetBrains Rider.
* Your solution must build and any tests must pass.
* You should not alter the notification service or the the account repository interfaces.
* You may add unit/integration tests using a test framework (and/or mocking framework) of your choice.
* A reasonable use of AI is permitted, but we want to see your own work and understanding reflected in the solution.
* You may edit this README.md if you want to give more details around your work (e.g. why you have done something a particular way, anything else you would look to do but didn't have time, use of AI, etc.).

Once you have completed test, zip up your solution, excluding any build artifacts to reduce the size, and email it back to our recruitment team.

Good luck!
