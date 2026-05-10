using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance {get ; private set;}
        
        [SerializeField] private GameTimestamp timestamp;
        [SerializeField] private float timeScale = 1.0f;
        public Transform sunTransform;
        
        public GameTimestamp CurrentGameTimeStamp => timestamp;
        private List<ITimeTracker> listeners = new ();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }
        }

        private void Start()
        {
            //starting time is 1pm
            timestamp = new GameTimestamp(13, 0, 0);
            StartCoroutine(UpdateTime());
        }

        IEnumerator UpdateTime()
        {
            while (true)
            {
                yield return new WaitForSeconds(1/timeScale);
                Tick();
            }
        }
        
        public void Tick()
        {
            AdvanceOneMinute();
            UpdateSunAngle();
        }

        private void AdvanceOneMinute()
        {
            timestamp.UpdateClock();

            foreach (ITimeTracker listner in listeners)
            {
                listner.ClockUpdate(timestamp);
            }
        }

        private void UpdateSunAngle()
        {
            //Move sun angle
            int timeInMinutes = GameTimestamp.HoursToMinutes(timestamp.hour) + timestamp.minute;
            float sunAngle = .25f * timeInMinutes - 90;
            sunTransform.localEulerAngles = new Vector3(sunAngle, 0, 0);
        }

        public void Sleep()
        {
            // Skip to the next occurrence of 6:00 AM.
            int currentTimeInMinutes = GameTimestamp.HoursToMinutes(timestamp.hour) + timestamp.minute;
            int nextMorningInMinutes = GameTimestamp.HoursToMinutes(6);
            int minutesToSkip = (nextMorningInMinutes - currentTimeInMinutes + GameTimestamp.HoursToMinutes(24)) %
                                GameTimestamp.HoursToMinutes(24);

            if (minutesToSkip == 0)
            {
                minutesToSkip = GameTimestamp.HoursToMinutes(24);
            }

            for (int i = 0; i < minutesToSkip; i++)
            {
                AdvanceOneMinute();
            }

            UpdateSunAngle();
        }

        public void RegisterTracker(ITimeTracker listener)
        {
            listeners.Add(listener);
        }

        public void UnregisterTracker(ITimeTracker listener)
        {
            listeners.Remove(listener);
        }
    }
}
