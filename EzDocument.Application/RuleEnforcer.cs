using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EzDocument.Application
{
    public static class RuleEnforcer
    {

        static List<string> Actions = new List<string> { "get",  "post", "put", "patch", "options" };

        static string JsonPattern = @"{[^}]+}";


        public static bool RuleMatchEndpointPath(string text)
        {
            var splittedText = text.Split(" ");

            var count = splittedText.Length;

            if (count > 2)
                return false;

            var action = splittedText[0];

            var path = splittedText[1];

            if (!Actions.Contains(action.ToLower())) return false;

            if (!path.StartsWith("/"))
                return false;

            return true;
        }

        public static bool RuleMatchJson(string text)
        {
            if (Regex.IsMatch(text, JsonPattern)) return true;

            return false;
        }
    }
}
