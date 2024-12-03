using System;

namespace TimHanewich.Toolkit
{
    public class UnixToolkit
    {
        public static int GetUnixTime(DateTime timestamp)
        {
            DateTime epochtime = DateTime.Parse("1/1/1970");
            TimeSpan ts = timestamp - epochtime;
            return System.Convert.ToInt32(ts.TotalSeconds);
        }
    }
}