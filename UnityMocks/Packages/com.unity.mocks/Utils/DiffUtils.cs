using System.Text.RegularExpressions;

namespace Unity.Utils
{
    public static class DiffUtils
    {
        public static bool IsDiff(string candidate)
        {
            const string detectDiffPattern = @"(?mx)
                ^
                ---\ [^\n]+\n
                \+\+\+\ [^\n]+\n
                @@\ ";

            return Regex.IsMatch(candidate, detectDiffPattern);
        }
    }
}
