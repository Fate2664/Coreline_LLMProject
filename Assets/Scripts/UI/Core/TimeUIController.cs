using Nova;
using UnityEngine;

namespace Coreline
{
    public sealed class TimeUIController : MonoBehaviour, ITimeTracker
    {
        [Header("Date & Time")]
        [SerializeField] private TextBlock TimeText = null;
        [SerializeField] private TextBlock TimePrefix = null;
        [SerializeField] private TextBlock DayText = null;

        private void Start()
        {
            TimeManager.Instance.RegisterTracker(this);
        }

        public void ClockUpdate(GameTimestamp timestamp)
        {
            int hours = timestamp.hour;
            int minutes = timestamp.minute;
            string prefix = "AM";

            if (hours > 12)
            {
                prefix = "PM";
                hours -= 12;
            }

            TimePrefix.Text = prefix;
            TimeText.Text = hours.ToString("00") +  ":" + minutes.ToString("00");
            DayText.Text = timestamp.day.ToString();
        }

        private void OnDisable() => TimeManager.Instance?.UnregisterTracker(this);
    }
}
