using TapMangoTakeHomeProject.Services;
using TapMangoProject.Interfaces;
using TapMangoProject.Models;
using TapMangoTakeHomeProject.Models;

var builder = WebApplication.CreateBuilder(args);

Guid accountNumberOne = Guid.NewGuid();
Guid accountNumberTwo = Guid.NewGuid();

List<Account> sampleAccounts = new()
{
    new Account
    {
        AccountNumber = accountNumberOne,
        AccountLimit = 10,
        Numbers = new List<Number>
        {
            new Number
            {
                AccountNumber = accountNumberOne,
                PhoneNumber = "+1234567890",
                PersonalNumberLimit = 5,
                LastSMSCheckTime = DateTime.UtcNow,
                NumberOfChecks = 0,
                Active = true
            },
            new Number
            {
                AccountNumber = accountNumberOne,
                PhoneNumber = "+1234567891",
                PersonalNumberLimit = 3,
                LastSMSCheckTime = DateTime.UtcNow,
                NumberOfChecks = 2
            }
        }
    },
    new Account
    {
        AccountNumber = accountNumberTwo,
        AccountLimit = 5,
        Numbers = new List<Number>
        {
            new Number
            {
                AccountNumber = accountNumberTwo,
                PhoneNumber = "+1987654321",
                PersonalNumberLimit = 2,
                LastSMSCheckTime = DateTime.UtcNow,
                NumberOfChecks = 1
            }
        }
    }
};

builder.Services.AddSingleton<IRateLimiterService>(sp =>
{
    var service = new RateLimiterService();
    service.InitializeAccounts(sampleAccounts);
    return service;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();

app.UseCors("DevCors");

app.MapControllers();

app.Run();
