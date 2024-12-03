using System;
using System.Collections.Generic;

namespace TimHanewich.Toolkit.TextAnalysis
{
    public class TextAnalysisToolkit
    {
        public static TextValuePair[] RankStrings(string[] strings, TextValuePairArg[] word_ratings)
        {
            //Add each string to its own rating
            List<TextValuePair> Filter1 = new List<TextValuePair>();
            foreach (string s in strings)
            {
                TextValuePair tvp = new TextValuePair();
                tvp.Text = s;
                tvp.Value = 0;
                Filter1.Add(tvp);
            }

            //Rate each
            foreach (TextValuePair tvp in Filter1)
            {
                foreach (TextValuePairArg arg in word_ratings)
                {
                    string[] split = tvp.Text.ToLower().Split(new string[] {arg.Text.ToLower()}, StringSplitOptions.None);
                    int appearenceCount = split.Length - 1;
                    if (arg.CountMultiple == false)
                    {
                        appearenceCount = Math.Min(1, appearenceCount); //Drop appearence to either 0 or 1 (it does or it doesn't have it) if it is an appear once
                    }
                    float val = (float)appearenceCount * arg.Value;
                    tvp.Value = tvp.Value + val;
                }
            }


            //Sort from most valuable to least valuable
            List<TextValuePair> Filter2 = new List<TextValuePair>();
            do
            {
                TextValuePair winner = Filter1[0];
                foreach (TextValuePair tvp in Filter1)
                {
                    if (tvp.Value > winner.Value)
                    {
                        winner = tvp;
                    }
                }
                Filter2.Add(winner);
                Filter1.Remove(winner);
            } while (Filter1.Count > 0);


            return Filter2.ToArray();
        }
    }
}