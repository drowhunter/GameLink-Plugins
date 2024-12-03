using System;
using System.Collections.Generic;

namespace TimHanewich.Toolkit
{
    public class HanewichTimeStamp
    {
        public static string GetTimeStamp(DateTime ds)
        {
            string a = ds.Year.ToString() + "." + ds.Month.ToString() + "." + ds.Day.ToString() + "." + ds.Hour.ToString() + "." + ds.Minute.ToString() + "." + ds.Second.ToString() + "." + ds.Millisecond.ToString();
            return a;
        }

        public static DateTime GetDateTimeFromTimeStamp(string hanewich_time_stamp)
        {
            List<string> Splitter = new List<string>();
            Splitter.Add(".");
            string[] parts = hanewich_time_stamp.Split(Splitter.ToArray(), StringSplitOptions.RemoveEmptyEntries);

            int year = 0;
            int month = 0;
            int day = 0;
            int hour = 0;
            int minute = 0;
            int second = 0;
            int millisecond = 0;

            if (parts.Length > 0)
            {
                year = Convert.ToInt32(parts[0]);
            }

            if (parts.Length > 1)
            {
                month = Convert.ToInt32(parts[1]);
            }

            if (parts.Length > 2)
            {
                day = Convert.ToInt32(parts[2]);
            }

            if (parts.Length > 3)
            {
                hour = Convert.ToInt32(parts[3]);
            }

            if (parts.Length > 4)
            {
                minute = Convert.ToInt32(parts[4]);
            }

            if (parts.Length > 5)
            {
                second = Convert.ToInt32(parts[5]);
            }

            if (parts.Length > 6)
            {
                millisecond = Convert.ToInt32(parts[6]);
            }

            DateTime tr = new DateTime(year, month, day, hour, minute, second, millisecond);

            return tr;

        }
    }
}