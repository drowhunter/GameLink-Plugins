using System;
using System.Collections.Generic;
using System.Linq;

namespace TimHanewich.Toolkit
{
    public static class MathToolkit
    {
        public static float StandardDeviation(this float[] values)
        {
            //Get the mean
            float avg = values.Average();

            List<float> numeratorvals = new List<float>();

            foreach (float f in values)
            {
                numeratorvals.Add((float)Math.Pow(f - avg, 2));
            }

            float numerator = numeratorvals.Sum();
            float val = numerator / (float)values.Length;
            val = (float)Math.Sqrt(val);
            return val;
        }
    }
}