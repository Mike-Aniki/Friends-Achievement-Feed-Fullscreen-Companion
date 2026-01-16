using System;
using System.Text.RegularExpressions;

namespace FriendsAchievementFeedFullscreenCompanion
{
    public static class MsJsonDate
    {
        private static readonly Regex Rx =
            new Regex(@"\/Date\((\-?\d+)(?:[+-]\d+)?\)\/", RegexOptions.Compiled);

        public static DateTimeOffset? Parse(string msJsonDate)
        {
            if (string.IsNullOrWhiteSpace(msJsonDate))
                return null;

            var m = Rx.Match(msJsonDate);
            if (!m.Success)
                return null;

            if (!long.TryParse(m.Groups[1].Value, out var ms))
                return null;

            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }
    }
}
