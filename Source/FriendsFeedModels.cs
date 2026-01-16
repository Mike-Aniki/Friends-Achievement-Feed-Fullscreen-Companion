using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace FriendsAchievementFeedFullscreenCompanion
{
    public class FriendAchievementCache
    {
        public string LastUpdatedUtc { get; set; }
        public List<FriendAchievementEntry> Entries { get; set; } = new List<FriendAchievementEntry>();
    }

    public class FriendAchievementEntry
    {
        public string AchievementApiName { get; set; }
        public string AchievementDisplayName { get; set; }
        public string AchievementDescription { get; set; }

        public int AppId { get; set; }
        public Guid? PlayniteGameId { get; set; }

        public string GameName { get; set; }

        public string FriendPersonaName { get; set; }
        public string FriendAvatarUrl { get; set; }
        public string FriendSteamId { get; set; }

        public string FriendAchievementIcon { get; set; }

        public string FriendUnlockTimeUtc { get; set; }

        public bool IsRevealed { get; set; }
        public bool HideAchievementsLockedForSelf { get; set; }

        public string SelfAchievementIcon { get; set; }
        public string SelfUnlockTime { get; set; }
    }
}
