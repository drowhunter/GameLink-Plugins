using System;

namespace TimHanewich.Toolkit
{
    public class WebToolkit
    {
        public static string HtmlToPlainText(string html_text)
        {
            //Pull out the html elements.
            string ALL = "";
            bool IsInsideHtml = false;
            foreach (char c in html_text)
            {
                if (IsInsideHtml == false)
                {
                    if (c.ToString() != "<")
                    {
                        ALL = ALL + c.ToString();
                    }
                    else
                    {
                        IsInsideHtml = true;
                    }
                }
                else
                {
                    if (c.ToString() == ">")
                    {
                        IsInsideHtml = false;
                    }
                }
            }

            //Remove common html stuff
            //https://www.w3.org/MarkUp/html-spec/html-spec_13.html
            ALL = ALL.Replace("&amp;", "&");
            ALL = ALL.Replace("&#10;", Environment.NewLine);
            ALL = ALL.Replace("&#39;", "'");
            ALL = ALL.Replace("&quot;", "\"");
            ALL = ALL.Replace("??", ""); //Random emojis

            return ALL;
        }
    }
}