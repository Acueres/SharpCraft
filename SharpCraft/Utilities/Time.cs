using SharpCraft.Persistence;
using System;


namespace SharpCraft.Utility
{
    class Time
    {
        public float LightIntensity
        {
            get
            {
                return (float)Math.Clamp(1.1 * Math.Exp(Math.Sin(period * (hours + minutes / 60.0))) - 1.5, 0.1, 1);
            }
        }

        public int[] Date
        {
            get
            {
                return [(int)days, hours, minutes];
            }
        }

        int days;
        int hours;
        int minutes;
        int seconds;

        const double period = Math.PI / 24;


        public Time(int day, int hour, int minute)
        {
            days = day;
            hours = hour;
            minutes = minute;
            seconds = 0;
        }

        public void Update()
        {
            seconds++;

            if (seconds == 60)
            {
                seconds = 0;
                minutes++;
            }

            if (minutes == 60)
            {
                minutes = 0;
                hours++;
            }

            if (hours == 24)
            {
                hours = 0;
                days++;
            }
        }

        public void SaveParameters(Parameters parameters)
        {
            parameters.Day = days;
            parameters.Hour = hours;
            parameters.Minute = minutes;
        }

        public override string ToString()
        {
            return $"Day {days}, {hours}:{(minutes < 10 ? "0": null)}{minutes}";
        }
    }
}
