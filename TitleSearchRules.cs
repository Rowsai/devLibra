using System;
using System.Globalization;

namespace devLibra;

internal static class TitleSearchRules
{
    internal static bool Matches(string title, string query) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(title, query.Trim(),
            CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType) >= 0;
}
