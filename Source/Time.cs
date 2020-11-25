using System;


namespace SharpCraft
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
                return new int[] { (int)days, hours, minutes };
            }
        }

        uint days;
        int hours;
        int minutes;
        int seconds;

        const double period = Math.PI / 24;


        public Time(int day, int hour, int minute)
        {
            days = (uint)day;
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

        public override string ToString()
        {
            return $"Day {days}, {hours}:{(minutes < 10 ? "0": null)}{minutes}";
        }
    }
}
