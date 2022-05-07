namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;

    public static class SearchStringSpecificationHelper
    {
        public static (string text, bool isConcrete)[] PrepareFilters(string searchString)
        {
            var list = new List<(string text, bool isConcrete)>();

            var isInConcrete = false;
            var wordStart = 0;
            for (var i = 0; i < searchString.Length; i++)
            {
                var character = searchString[i];
                if (character == '"')
                {
                    if (isInConcrete)
                    {
                        isInConcrete = false;
                        var length = i - wordStart;
                        if (length > 0)
                        {
                            var currentText = searchString.Substring(wordStart, length);
                            list.Add((currentText, true));
                        }
                    }
                    else
                    {
                        isInConcrete = true;
                    }
                    wordStart = i + 1;
                }
                if (character == ' '
                    && !isInConcrete)
                {
                    var length = i - wordStart;
                    if (length > 0)
                    {
                        var currentText = searchString.Substring(wordStart, length).GetLettersAndDigits();
                        list.Add((currentText, false));
                    }
                    wordStart = i;
                }
            }
            if (wordStart < searchString.Length - 1)
            {
                var length = isInConcrete ? searchString.Length - wordStart - 1 : searchString.Length - wordStart;
                if (length > 0)
                {
                    var currentText = searchString.Substring(wordStart, length);
                    if (isInConcrete)
                    {
                        list.Add((currentText, true));
                    }
                    else
                    {
                        list.Add((currentText.GetLettersAndDigits(), false));
                    }
                }
            }

            return list.ToArray();
        }

        public static bool CheckPlainString(string? input, string filter, bool isConcreteFilter)
        {
            if (input == null)
            {
                return false;
            }

            if (isConcreteFilter)
            {
                return input.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else
            {
                return input.GetLettersAndDigits().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
