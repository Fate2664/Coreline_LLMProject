using UnityEngine;

namespace Coreline
{
    [System.Serializable]
    public class GameTimestamp
    {
        public int hour;
        public int minute;
        public int day;

        public GameTimestamp(int hour, int minute, int day)
        {
            this.hour = hour;
            this.minute = minute;
            this.day = day;
        }

        public void UpdateClock()
        {
            minute++;

            if (minute >= 60)
            {
                minute = 0;
                hour++;
            }

            if (hour >= 24)
            {
                hour = 0;
                day++;
            }
          
        }

        public static int HoursToMinutes(int hour) => hour * 60;
        public static int DaysToHours(int days) => days * 24;
    }
}