using TapMangoProject.Models;
using TapMangoTakeHomeProject.Models;

namespace TapMangoProject.Interfaces
{
    public interface IRateLimiterService
    {
        bool IfNumberCanSendSMS(Number number);

        List<Account> GetAccounts();
    }
}
