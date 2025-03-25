namespace TapMangoTakeHomeProject.Models
{
    public enum PhoneNumberCanSendResponseErrors
    {
        RateLimitExceededForNumber,
        RateLimitExceededForAccount,
        NumberNotFound,
        AccountNotFound,
        CooldownTimeExceeded,
        NumberIsInactive
    }
}
