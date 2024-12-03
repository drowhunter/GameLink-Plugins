using System;

namespace TimHanewich.Toolkit.TextAnalysis
{
    public class TextValuePairArg : TextValuePair
    {
        public bool CountMultiple { get; set; }

        public static TextValuePairArg Create(string text_, float value_, bool count_multiple)
        {
            TextValuePairArg ToReturn = new TextValuePairArg();
            ToReturn.Text = text_;
            ToReturn.Value = value_;
            ToReturn.CountMultiple = count_multiple;
            return ToReturn;
        }
    }
}