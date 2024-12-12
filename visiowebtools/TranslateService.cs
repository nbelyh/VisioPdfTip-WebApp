
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VisioWebTools
{
    public class ParsedItem
    {
        public bool IsTag { get; set; }
        public string Content { get; set; }
        public int IX { get; set; }
    }

    public class TextResult 
    {
        public string PlainText { get; set; }
        public string FormattedText { get; set; }
    }

    public class TranslateService
    {
        public static TextResult GetShapeText(XElement xmlText)
        {
            var plainText = new StringBuilder();
            var formattedText = new StringBuilder();
            foreach (var node in xmlText.Nodes())
            {
                if (node is XText text)
                {
                    plainText.Append(text.Value);
                    formattedText.Append(text.Value);
                }
                else if (node is XElement el)
                {
                    formattedText.Append($"{{{el.Name.LocalName}{el.Attribute("IX")?.Value}}}");
                }
            }
            return new TextResult
            {
                PlainText = plainText.ToString(),
                FormattedText = formattedText.ToString()
            };
        }

        public static List<ParsedItem> ParseShapeText(string input)
        {
            var result = new List<ParsedItem>();
            string pattern = @"\{(?<TagName>[a-zA-Z]+)(?<IX>\d+)\}|(?<Text>[^{}]+)";

            foreach (Match match in Regex.Matches(input, pattern))
            {
                if (match.Groups["TagName"].Success && match.Groups["IX"].Success)
                {
                    result.Add(new ParsedItem
                    {
                        IsTag = true,
                        Content = match.Groups["TagName"].Value,
                        IX = int.Parse(match.Groups["IX"].Value)
                    });
                }
                else if (match.Groups["Text"].Success)
                {
                    result.Add(new ParsedItem
                    {
                        IsTag = false,
                        Content = match.Groups["Text"].Value
                    });
                }
            }
            return result;
        }

        public static XElement BuildXElement(List<ParsedItem> items)
        {
            XElement root = new("Root");

            foreach (var item in items)
            {
                if (item.IsTag)
                {
                    root.Add(new XElement(item.Content, new XAttribute("IX", item.IX)));
                }
                else
                {
                    root.Add(new XText(item.Content));
                }
            }

            return root;
        }
    }
}