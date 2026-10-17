using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YawVR_Game_Engine.Plugin
{
    public class ExponentinalClass
    {
        // A függvény
        private static float Calculate(float x, float k = 15)
        {
            if (x < 0)
            {
                return 0.0f;
            }

            if (x > 90) 
            {
                return 1.0f;
            }

            // Az exponenciális rész
            double result = (x / 90) * (1 - Math.Exp(-k * (90 - x) / 90));
            return (float)Math.Min(result, x / 90);  // Biztosítja, hogy ne lépje túl a lineáris függvényt
        }

        // Numerikus maximum keresés (Newton módszer)
        private static float FindMaximum(float k = 10, float step = 1.0f)
        {
            float fMaxY = 0.0f;
            float retX = 0.0f;

            for (float x = 0.0f; x < 90.0f; x += step) 
            {
                float y = Calculate(x, k);

                if (y > fMaxY) 
                {
                    fMaxY = y;
                    retX = x;
                }
            }

            return retX;
        }

        public static float Calc(float x, float k = 15) 
        {
            x = x * 0.8f;

            float y = (float)Calculate(x, k);

            float maxX = (float)FindMaximum(k, 1.0f);
            float maxY = (float)Calculate(maxX, k);


            float ret = y;
            if (x > maxX && y < maxY) 
            {
                ret = maxY;
            }

            return ret;
        }
    }
}
