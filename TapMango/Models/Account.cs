using TapMangoTakeHomeProject.Models;

namespace TapMangoProject.Models
{
    public class Account
    {
        public required Guid AccountNumber { get; set; }
        public int AccountLimit { get; set; }
        public List<Number> Numbers { get; set; } = new List<Number>();
        public int AccountNumberOfChecks
        {
            get
            {
                return Numbers.Sum(n => n.NumberOfChecks);
            }
        }
    }
}
