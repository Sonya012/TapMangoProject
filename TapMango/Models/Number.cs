namespace TapMangoTakeHomeProject.Models
{
    public class Number
    {
        public required Guid AccountNumber { get; set; }
        public int PersonalNumberLimit { get; set; }
        public required string PhoneNumber { get; set; }
        public DateTime LastSMSCheckTime { get; set; }
        public int NumberOfChecks { get; set; }
        public bool Active { get; set; } = true;
    }
}
