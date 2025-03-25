using Microsoft.AspNetCore.Mvc;
using TapMangoProject.Interfaces;
using TapMangoTakeHomeProject.Models;
using TapMangoTakeHomeProject.Utilities;

namespace TapMangoTakeHomeProject.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class RateLimitController : ControllerBase
    {
        private readonly IRateLimiterService _rateLimiterService;

        public RateLimitController(IRateLimiterService rateLimiterService)
        {
            _rateLimiterService = rateLimiterService;
        }

        [HttpGet("CheckRateLimitForSms")]
        public IActionResult CheckRateLimitForSms(Number number)
        {
            try
            {
                bool canSendSMS = _rateLimiterService.IfNumberCanSendSMS(number);
                return Ok(canSendSMS);
            }
            catch (PhoneNumberSMSCheckException ex) when
                  (ex.ErrorCode == PhoneNumberCanSendResponseErrors.RateLimitExceededForNumber)
            {
                return StatusCode(429, "The rate limit exceeded for this phone number.");
            }
            catch (PhoneNumberSMSCheckException ex) when
                  (ex.ErrorCode == PhoneNumberCanSendResponseErrors.RateLimitExceededForAccount)
            {
                return StatusCode(429, "The rate limit exceeded for the account.");
            }
            catch
            {
                return StatusCode(500, "Unexpected error has occurred, please contact administration.");
            }
        }

        [HttpGet("GetAccounts")]
        public IActionResult GetAccounts()
        {
            var accounts = _rateLimiterService.GetAccounts();
            if (accounts == null || accounts.Count == 0)
                return NotFound("No accounts found.");

            return Ok(accounts);
        }
    }
}
