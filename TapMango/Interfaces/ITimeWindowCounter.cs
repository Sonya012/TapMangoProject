namespace TapMangoProject.Interfaces
{
    public interface ITimeWindowCounter
    {
        int Count(DateTime currentDateTime);
        void Increment(DateTime currentDateTime);
        DateTime LastUsed { get; }
    }
}
