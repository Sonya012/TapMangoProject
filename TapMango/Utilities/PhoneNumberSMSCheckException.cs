using TapMangoTakeHomeProject.Models;

namespace TapMangoTakeHomeProject.Utilities
{
    public class PhoneNumberSMSCheckException : Exception
    {
        public PhoneNumberCanSendResponseErrors ErrorCode { get; }

        public PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors errorCode)
            : base($"SMS sending error occurred: {errorCode}")
        {
            ErrorCode = errorCode;
        }

        public PhoneNumberSMSCheckException(PhoneNumberCanSendResponseErrors errorCode, string customMessage)
            : base(customMessage)
        {
            ErrorCode = errorCode;
        }
    }
}