using UnityEngine;

namespace Coreline
{
    public interface ITimeTracker
    {
        void ClockUpdate(GameTimestamp timestamp);
    }
}
