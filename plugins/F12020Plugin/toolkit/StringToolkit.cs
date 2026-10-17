using System;

namespace TimHanewich.Toolkit
{
    public static class HanewichStringToolkit
    {
        public static string FilterCharacters(this string s, string keep_characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVQXYZ1234567890. ")
        {
            string ToReturn = "";
            foreach (char c in s)
            {
                if (keep_characters.Contains(c.ToString()))
                {
                    ToReturn = ToReturn + c.ToString();
                }
            }
            return ToReturn;
        }
    }
}