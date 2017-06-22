using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace ChewsiPlugin.UI
{
    internal static class Utils
    {
        public static FlowDocument FormatDocument(this string input, params Tuple<string, Func<Inline, Inline>> [] formatters)
        {
            var pieces = new List<Tuple<int, string, Func<Inline, Inline>>>();
            foreach (var formatter in formatters)
            {
                var match = Regex.Match(input, formatter.Item1, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    pieces.Add(new Tuple<int, string, Func<Inline, Inline>>(match.Index, match.Value, formatter.Item2));
                }               
            }
            
            // formatting
            var p = new Paragraph();

            int lastIndex = 0;
            var list = pieces.OrderBy(m => m.Item1).ToList();
            //list.Dump();
            for (int i = 0; i < list.Count; i++)
            {
                var piece = list[i];
                // before
                var s = input.Substring(lastIndex, piece.Item1 - lastIndex);
                if (s.Length > 0)
                {
                    p.Inlines.Add(new Run(s));
                }

                // pattern
                var r = new Run(piece.Item2);
                p.Inlines.Add(piece.Item3(r));

                // after
                if (i < list.Count - 1)
                {
                    // till the next piece
                    lastIndex = list[i + 1].Item1;
                    int start = piece.Item1 + piece.Item2.Length;
                    s = input.Substring(start, lastIndex - start);
                }
                else
                {
                    // till the end
                    s = input.Substring(piece.Item1 + piece.Item2.Length);
                }

                if (s.Length > 0)
                {
                    p.Inlines.Add(new Run(s));
                }
            }

            var doc = new FlowDocument();
            doc.Blocks.Add(p);
            return doc;
        }
    }
}
