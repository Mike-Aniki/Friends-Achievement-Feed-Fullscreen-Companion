using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;

namespace FriendsAchievementFeedFullscreenCompanion
{
    public class FriendsAchievementFeedFullscreenCompanionSettings : ObservableObject
    {
        private bool isEnabled = true;
        public bool IsEnabled
        {
            get => isEnabled;
            set => SetValue(ref isEnabled, value);
        }

        private int maxItems = 60;
        public int MaxItems
        {
            get => maxItems;
            set => SetValue(ref maxItems, Clamp(value, 10, 1000));
        }

        private bool filterToSelectedGame = false;
        public bool FilterToSelectedGame
        {
            get => filterToSelectedGame;
            set => SetValue(ref filterToSelectedGame, value);
        }

        private bool hideLockedForSelf = false;
        public bool HideLockedForSelf
        {
            get => hideLockedForSelf;
            set => SetValue(ref hideLockedForSelf, value);
        }

        private bool showUnrevealed = true;
        public bool ShowUnrevealed
        {
            get => showUnrevealed;
            set => SetValue(ref showUnrevealed, value);
        }

        private bool hasFriendsAchievements;
        [DontSerialize]
        public bool HasFriendsAchievements
        {
            get => hasFriendsAchievements;
            set => SetValue(ref hasFriendsAchievements, value);
        }

        private DateTime? lastRefreshLocal;
        [DontSerialize]
        public DateTime? LastRefreshLocal
        {
            get => lastRefreshLocal;
            set => SetValue(ref lastRefreshLocal, value);
        }

        private string statusMessage = string.Empty;
        [DontSerialize]
        public string StatusMessage
        {
            get => statusMessage;
            set => SetValue(ref statusMessage, value);
        }

        private int totalItems;
        [DontSerialize]
        public int TotalItems
        {
            get => totalItems;
            set => SetValue(ref totalItems, value);
        }

        private string selectedGameName = string.Empty;
        [DontSerialize]
        public string SelectedGameName
        {
            get => selectedGameName;
            set => SetValue(ref selectedGameName, value);
        }

       

        private ObservableCollection<FriendAchievementEntryView> friendAchievements
            = new ObservableCollection<FriendAchievementEntryView>();

        [DontSerialize]
        public ObservableCollection<FriendAchievementEntryView> FriendAchievements
        {
            get => friendAchievements;
            set => SetValue(ref friendAchievements, value);
        }

        private ObservableCollection<FriendFeedRowView> friendFeedGrouped
            = new ObservableCollection<FriendFeedRowView>();

        [DontSerialize]
        public ObservableCollection<FriendFeedRowView> FriendFeedGrouped
        {
            get => friendFeedGrouped;
            set => SetValue(ref friendFeedGrouped, value);
        }

        private ObservableCollection<FriendAchievementGroupView> friendGroups
            = new ObservableCollection<FriendAchievementGroupView>();

        [DontSerialize]
        public ObservableCollection<FriendAchievementGroupView> FriendGroups
        {
            get => friendGroups;
            set => SetValue(ref friendGroups, value);
        }

        private ObservableCollection<FriendAchievementEntryView> selectedGameAchievements
            = new ObservableCollection<FriendAchievementEntryView>();

        [DontSerialize]
        public ObservableCollection<FriendAchievementEntryView> SelectedGameAchievements
        {
            get => selectedGameAchievements;
            set => SetValue(ref selectedGameAchievements, value);
        }

        private string expandedEventId = string.Empty;
        [DontSerialize]
        public string ExpandedEventId
        {
            get => expandedEventId;
            set => SetValue(ref expandedEventId, value);
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }

  
    public class FriendAchievementEntryView : ObservableObject
    {
        public Guid? PlayniteGameId { get; set; }
        public int AppId { get; set; }

        public string GameName { get; set; } = string.Empty;
        public string GameCoverImage { get; set; } = string.Empty;
        public string GameIcon { get; set; } = string.Empty;

        public string FriendPersonaName { get; set; } = string.Empty;
        public string FriendAvatarUrl { get; set; } = string.Empty;
        public string FriendSteamId { get; set; } = string.Empty;
        private string friendAvatarPath = string.Empty;
        public string FriendAvatarPath
        {
            get => friendAvatarPath;
            set => SetValue(ref friendAvatarPath, value);
        }


        public string AchievementApiName { get; set; } = string.Empty;
        public string AchievementDisplayName { get; set; } = string.Empty;
        public string AchievementDescription { get; set; } = string.Empty;

        public string FriendAchievementIcon { get; set; } = string.Empty;

        public bool IsRevealed { get; set; }
        public bool HideAchievementsLockedForSelf { get; set; }

        public DateTimeOffset? UnlockTimeUtc { get; set; }

        public DateTime? UnlockTimeLocal => UnlockTimeUtc?.ToLocalTime().DateTime;
    }


    public class FriendFeedRowView : ObservableObject
    {
        public bool IsHeader { get; set; }

        public string EventId { get; set; } = string.Empty;

        public string HeaderTitle { get; set; } = string.Empty;
        public string HeaderSubtitle { get; set; } = string.Empty;

        public string FriendPersonaName { get; set; } = string.Empty;
        public string FriendAvatarUrl { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string GameCoverImage { get; set; } = string.Empty;
        public string GameIcon { get; set; } = string.Empty;

        public string AchievementDisplayName { get; set; } = string.Empty;
        public string AchievementDescription { get; set; } = string.Empty;
        public string FriendAchievementIcon { get; set; } = string.Empty;
        public DateTime? UnlockTimeLocal { get; set; }
    }

    public class FriendAchievementGroupView : ObservableObject
    {
        public string EventId { get; set; } = string.Empty;

        public string FriendPersonaName { get; set; } = string.Empty;
        public string FriendAvatarUrl { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string GameCoverImage { get; set; } = string.Empty;
        public string GameIcon { get; set; } = string.Empty;
        public string FriendSteamId { get; set; } = string.Empty;
        private string friendAvatarPath = string.Empty;
        public string FriendAvatarPath
        {
            get => friendAvatarPath;
            set => SetValue(ref friendAvatarPath, value);
        }

        public string HeaderTitle { get; set; } = string.Empty;
        public string HeaderSubtitle { get; set; } = string.Empty;

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set => SetValue(ref isExpanded, value);
        }

        public ObservableCollection<FriendAchievementEntryView> Items { get; }
            = new ObservableCollection<FriendAchievementEntryView>();
    }

    public class FriendsAchievementFeedFullscreenCompanionSettingsViewModel : ObservableObject, ISettings
    {
        private readonly FriendsAchievementFeedFullscreenCompanion plugin;
        private FriendsAchievementFeedFullscreenCompanionSettings editingClone;

        private FriendsAchievementFeedFullscreenCompanionSettings settings;
        public FriendsAchievementFeedFullscreenCompanionSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public FriendsAchievementFeedFullscreenCompanionSettingsViewModel(FriendsAchievementFeedFullscreenCompanion plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<FriendsAchievementFeedFullscreenCompanionSettings>();
            Settings = savedSettings ?? new FriendsAchievementFeedFullscreenCompanionSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (Settings.MaxItems < 10 || Settings.MaxItems > 1000)
            {
                errors.Add("MaxItems must be between 10 and 1000.");
            }

            return errors.Count == 0;
        }

        public void SetError(string message)
        {
            Settings.StatusMessage = message ?? string.Empty;
        }

        public void UpdateGlobalFeed(IEnumerable<FriendAchievementEntryView> items)
        {
            var list = Normalize(items)
                .Take(Settings.MaxItems)
                .ToList();

            Settings.FriendAchievements.Clear();
            foreach (var it in list)
            {
                Settings.FriendAchievements.Add(it);
            }

            Settings.TotalItems = Settings.FriendAchievements.Count;
            Settings.HasFriendsAchievements = Settings.TotalItems > 0;
            Settings.LastRefreshLocal = DateTime.Now;
            Settings.StatusMessage = string.Empty;
        }

        public void UpdateGroupedFeed(IEnumerable<FriendAchievementEntryView> items)
        {
            var list = Normalize(items)
                .Take(Settings.MaxItems)
                .ToList();

            Settings.FriendFeedGrouped.Clear();

            string lastEventId = null;

            foreach (var it in list)
            {
                var eventId = BuildEventId(it);

                if (eventId != lastEventId)
                {
                    Settings.FriendFeedGrouped.Add(new FriendFeedRowView
                    {
                        IsHeader = true,
                        EventId = eventId,

                        FriendPersonaName = it.FriendPersonaName ?? string.Empty,
                        FriendAvatarUrl = it.FriendAvatarUrl ?? string.Empty,
                        GameName = it.GameName ?? string.Empty,

                        HeaderTitle = BuildHeaderTitle(it),
                        HeaderSubtitle = BuildHeaderSubtitle(it)
                    });

                    lastEventId = eventId;
                }

                Settings.FriendFeedGrouped.Add(new FriendFeedRowView
                {
                    IsHeader = false,
                    EventId = eventId,

                    FriendPersonaName = it.FriendPersonaName ?? string.Empty,
                    FriendAvatarUrl = it.FriendAvatarUrl ?? string.Empty,
                    GameName = it.GameName ?? string.Empty,

                    AchievementDisplayName = it.AchievementDisplayName ?? string.Empty,
                    AchievementDescription = it.AchievementDescription ?? string.Empty,
                    FriendAchievementIcon = it.FriendAchievementIcon ?? string.Empty,
                    UnlockTimeLocal = it.UnlockTimeLocal
                });
            }

            Settings.TotalItems = Settings.FriendFeedGrouped.Count; 
            Settings.HasFriendsAchievements = Settings.TotalItems > 0;
            Settings.LastRefreshLocal = DateTime.Now;
            Settings.StatusMessage = string.Empty;
        }

        public void UpdateAccordionGroups(IEnumerable<FriendAchievementEntryView> items)
        {
            var list = Normalize(items)
                .Take(Settings.MaxItems)
                .ToList();

            Settings.FriendGroups.Clear();

            FriendAchievementGroupView current = null;
            string lastEventId = null;

            foreach (var it in list)
            {
                var eventId = BuildEventId(it);

                if (current == null || eventId != lastEventId)
                {
                    Game dbGame = null;

                    try
                    {
                        if (it.PlayniteGameId.HasValue)
                        {
                            dbGame = plugin?.PlayniteApi?.Database?.Games?.Get(it.PlayniteGameId.Value);

                        }
                    }
                    catch
                    {
                        dbGame = null;
                    }

                    string cover = string.Empty;
                    string icon = string.Empty;

                    try
                    {
                        if (dbGame != null)
                        {
                            if (!string.IsNullOrWhiteSpace(dbGame.CoverImage))
                                cover = plugin.PlayniteApi.Database.GetFullFilePath(dbGame.CoverImage) ?? string.Empty;

                            if (!string.IsNullOrWhiteSpace(dbGame.Icon))
                                icon = plugin.PlayniteApi.Database.GetFullFilePath(dbGame.Icon) ?? string.Empty;
                        }
                    }
                    catch
                    {
                        cover = string.Empty;
                        icon = string.Empty;
                    }


                    current = new FriendAchievementGroupView
                    {
                        EventId = eventId,
                        FriendPersonaName = it.FriendPersonaName ?? string.Empty,
                        FriendSteamId = it.FriendSteamId ?? string.Empty,

                        FriendAvatarUrl = it.FriendAvatarUrl ?? string.Empty,
                        FriendAvatarPath = !string.IsNullOrEmpty(it.FriendAvatarPath)
                            ? it.FriendAvatarPath
                            : (it.FriendAvatarUrl ?? string.Empty),

                        GameName = it.GameName ?? string.Empty,

                        GameCoverImage = cover,   
                        GameIcon = icon,          

                        HeaderTitle = BuildHeaderTitle(it),
                        HeaderSubtitle = BuildHeaderSubtitle(it),
                        IsExpanded = false
                    };



                    Settings.FriendGroups.Add(current);
                    lastEventId = eventId;
                }

                current.Items.Add(it);
            }

            Settings.TotalItems = list.Count;
            Settings.HasFriendsAchievements = Settings.TotalItems > 0;
            Settings.LastRefreshLocal = DateTime.Now;
            Settings.StatusMessage = string.Empty;
        }

        public void UpdateSelectedGameFeed(Game selectedGame, IEnumerable<FriendAchievementEntryView> items)
        {
            Settings.SelectedGameName = selectedGame?.Name ?? string.Empty;

            var list = Normalize(items)
                .Take(Settings.MaxItems)
                .ToList();

            Settings.SelectedGameAchievements.Clear();
            foreach (var it in list)
            {
                Settings.SelectedGameAchievements.Add(it);
            }

            Settings.LastRefreshLocal = DateTime.Now;
        }


        private IEnumerable<FriendAchievementEntryView> Normalize(IEnumerable<FriendAchievementEntryView> items)
        {
            return (items ?? Enumerable.Empty<FriendAchievementEntryView>())
                .OrderByDescending(x => x.UnlockTimeUtc ?? DateTimeOffset.MinValue);
        }

        private string BuildEventId(FriendAchievementEntryView it)
        {
            var friendKey =
                !string.IsNullOrWhiteSpace(it.FriendSteamId)
                    ? it.FriendSteamId.Trim()
                    : (it.FriendPersonaName ?? string.Empty)
                        .Trim()
                        .ToLowerInvariant();

            var gameKey = (it.GameName ?? string.Empty).Trim();

            // Group by local day
            var dayKey = it.UnlockTimeUtc.HasValue
                ? it.UnlockTimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd")
                : "unknown-date";

            return $"{friendKey}|{gameKey}|{dayKey}";
        }


        private string BuildHeaderTitle(FriendAchievementEntryView it)
        {
            var friend = SafeFriendName(it);
            var game = SafeGameName(it);

            var format = GetLocOrFallback(
                "LOCFriendsAchievementFeed_HeaderTitleFormat",
                "{0} achieved in {1}"
            );

            try
            {
                return string.Format(format, friend, game);
            }
            catch
            {
                return $"{friend} unlocked an achievement in {game}";
            }
        }


        private string BuildHeaderSubtitle(FriendAchievementEntryView it)
        {
            if (!it.UnlockTimeUtc.HasValue)
                return string.Empty;

            var local = it.UnlockTimeUtc.Value.ToLocalTime();

            // "D" = date longue locale 
            // "d" = date courte locale 
            return local.ToString("d", CultureInfo.CurrentCulture);
        }



        private string GetLocOrFallback(string key, string fallback)
        {
            try
            {
                var s = plugin?.PlayniteApi?.Resources?.GetString(key);

                if (string.IsNullOrWhiteSpace(s) || string.Equals(s, key, StringComparison.OrdinalIgnoreCase))
                {
                    return fallback;
                }

                return s;
            }
            catch
            {
                return fallback;
            }
        }

        private string SafeFriendName(FriendAchievementEntryView it)
        {
            var name = it?.FriendPersonaName;
            return string.IsNullOrWhiteSpace(name) ? "A friend" : name;
        }

        private string SafeGameName(FriendAchievementEntryView it)
        {
            var game = it?.GameName;
            return string.IsNullOrWhiteSpace(game) ? "a game" : game;
        }

    }
}
