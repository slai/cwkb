using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;

namespace cwkb
{
    public class SearchUtil
    {
        // should be treated as constants
        private static string[] MOD_OPERATORS = new string[] { "NOT", "!", "NEAR", "~", "FORMSOF", "ISABOUT" };
        private static string[] JOIN_OPERATORS = new string[] { "AND", "&", "&!", "OR", "|" };

        // add in missing operators between terms
        public static string MassageQuery(string query)
        {
            var termRegex = new Regex("^(\"[^\"]*\"|\\w+(\\s|$))", RegexOptions.Compiled);
            var modOperators = new HashSet<string>(MOD_OPERATORS);
            var joinOperators = new HashSet<string>(JOIN_OPERATORS);
            var queryQueue = new Queue<string>();

            while (true)
            {
                var match = termRegex.Match(query);
                if (!match.Success) break;

                var capture = match.Groups[0].Captures[0];
                queryQueue.Enqueue(capture.Value.Trim());
                query = query.Substring(capture.Index + capture.Length);
            }

            var newQuery = new Stack<string>();
            while (queryQueue.Count > 0)
            {
                var word = queryQueue.Dequeue();
                if (joinOperators.Contains(word.ToUpper()))
                {
                    word = word.ToUpper();

                    /* don't want to fix query, just add in missing ANDs
                    // ignore this join operator as there's nothing to join with
                    if (newQuery.Count == 0)
                        continue;

                    // two join operators - ignore this one
                    if (joinOperators.Contains(newQuery.Peek()))
                        continue;

                    // a join and a mod operator, ignore this too as NOT AND doesn't make sense (AND NOT does though)
                    if (modOperators.Contains(newQuery.Peek()))
                        continue;
                    */

                    newQuery.Push(word);
                }
                else if (modOperators.Contains(word.ToUpper()))
                {
                    word = word.ToUpper();

                    /* don't want to fix query, just add in missing ANDs
                    // ensure previous word isn't a mod operator either
                    if (newQuery.Count > 0 && modOperators.Contains(newQuery.Peek()))
                        continue;
                    */

                    newQuery.Push(word);
                }
                else // just a word
                {
                    // two words together with no operator in between; assume AND
                    if (newQuery.Count > 0 && !joinOperators.Contains(newQuery.Peek()) && !modOperators.Contains(newQuery.Peek()))
                        newQuery.Push("AND");

                    newQuery.Push(word);
                }
            }

            return string.Join(" ", newQuery.Reverse().ToList());
        }

        public static List<string> GetQueryWords(string query)
        {
            var termRegex = new Regex("^(\"[^\"]*\"|\\w+(\\s|$))", RegexOptions.Compiled);
            var modOperators = new HashSet<string>(MOD_OPERATORS);
            var joinOperators = new HashSet<string>(JOIN_OPERATORS);
            var words = new List<string>();

            while (true)
            {
                var match = termRegex.Match(query);
                if (!match.Success) break;

                var capture = match.Groups[0].Captures[0];

                var word = capture.Value.Trim();
                word = word.Replace("\"", "");
                if (!modOperators.Contains(word) && !joinOperators.Contains(word))
                    words.Add(word);

                query = query.Substring(capture.Index + capture.Length);
            }

            return words;
        }

        public static string GetExcerpt(List<string> words, List<string> excerptFields, string defaultExcerpt, int length)
        {
            int maxLeftAnchorLength = length / 2;
            int maxRightAnchorLength = length - maxLeftAnchorLength;
            int anchorLeeway = 10;

            string excerpt = null;
            foreach (var e in excerptFields)
            {
                foreach (var word in words)
                {
                    var i = e.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
                    if (i < 0) continue;

                    var leftAnchor = i - maxLeftAnchorLength;
                    var rightAnchor = 0;
                    if (leftAnchor < 0)
                    {
                        // this is the overflow; put that here, to add on later
                        rightAnchor = leftAnchor * -1;
                        leftAnchor = 0;
                    }

                    rightAnchor += i + word.Length + maxRightAnchorLength;
                    if (rightAnchor > e.Length)
                    {
                        leftAnchor -= rightAnchor - e.Length;
                        rightAnchor = e.Length;
                    }

                    // if the left anchor overflows again due to right anchor, reset to 0
                    if (leftAnchor < 0)
                        leftAnchor = 0;

                    // try to align to word boundaries.
                    if (leftAnchor > 0)
                    {
                        // try looking for the first space before leftAnchor
                        i = e.Substring(0, leftAnchor).LastIndexOf(" ");
                        if (i >= 0)
                            leftAnchor = i;
                        else if (leftAnchor < anchorLeeway) // prevent chopped off first word
                            leftAnchor = 0;
                    }

                    if (rightAnchor < e.Length)
                    {
                        // try looking for the first space after rightAnchor
                        i = e.Substring(rightAnchor).IndexOf(" ");
                        if (i >= 0)
                            rightAnchor += i; // added due to substring
                        else if (e.Length - rightAnchor < anchorLeeway) // prevent chopped off last word
                            rightAnchor = e.Length;
                    }

                    excerpt = e.Substring(leftAnchor, rightAnchor - leftAnchor);

                    // add in ellipsises
                    if (leftAnchor != 0)
                        excerpt = "..." + excerpt;
                    if (rightAnchor != e.Length)
                        excerpt += "...";

                    break;
                }

                if (excerpt != null)
                    break;
            }

            // if none of the words can be found in specified fields, default to problem
            if (excerpt == null)
            {
                if (length > defaultExcerpt.Length)
                    excerpt = defaultExcerpt;
                else
                    excerpt = defaultExcerpt.Substring(0, length);
            }

            return excerpt;
        }
    }

    [NDjango.Interfaces.Name("highlight")]
    public class HighlightFilter : NDjango.Interfaces.IFilter
    {
        // by returning null, it makes the filter parameter required
        public object DefaultValue
        {
            get { return null; }
        }

        public object PerformWithParam(object value, object parameter)
        {
            var valueStr = value.ToString();
            var words = parameter as List<string>;

            if (words == null)
                return value;

            // sort words by longest so the longest matches occur first
            words.Sort(new Comparison<string>((w1, w2) => w2.Length - w1.Length));

            var parts = new List<HighlightStringPart>();
            parts.Add(new HighlightStringPart(valueStr, false));
            foreach (var word in words)
            {
                var p = 0;
                while (p < parts.Count)
                {
                    var part = parts[p];

                    // don't rematch already highlighted parts
                    if (part.Highlighted)
                    {
                        p++;
                        continue;
                    }

                    // look for a word match
                    var i = part.Text.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
                    if (i < 0)
                    {
                        p++;
                        continue;
                    }

                    // remove this part, and re-add the split up parts
                    parts.RemoveAt(p);
                    if (i > 0)
                    {
                        parts.Insert(p, new HighlightStringPart(part.Text.Substring(0, i), false));
                        p++;
                    }

                    parts.Insert(p, new HighlightStringPart(part.Text.Substring(i, word.Length), true));
                    p++;

                    if (i + word.Length < part.Text.Length)
                    {
                        parts.Insert(p, new HighlightStringPart(part.Text.Substring(i + word.Length), false));
                        // don't increment p as we want to look at this part again for further matches
                    }
                }
            }

            // join the string back together
            return string.Join("", parts);
        }

        public object Perform(object value)
        {
            throw new NotImplementedException();
        }
    }

    public class HighlightStringPart
    {
        public string Text { get; set; }
        public bool Highlighted { get; set; }

        public HighlightStringPart(string text, bool highlighted)
        {
            Text = text;
            Highlighted = highlighted;
        }

        public override string ToString()
        {
            if (Highlighted)
                return "<span class=\"highlight\">" + Text + "</span>";
            else
                return Text;
        }
    }
}