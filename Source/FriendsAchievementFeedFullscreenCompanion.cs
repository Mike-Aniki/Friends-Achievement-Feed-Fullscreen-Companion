using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;



namespace FriendsAchievementFeedFullscreenCompanion
{
    public class FriendsAchievementFeedFullscreenCompanion : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // ID FriendsAchievementFeed
        private static readonly Guid SourcePluginId =
            Guid.Parse("10f90193-72aa-4cdb-b16d-3e6b1f0feb17");

        private const string CacheFileName = "friend_achievement_cache.json";

        private readonly FriendsAchievementFeedFullscreenCompanionSettingsViewModel settings;

        public FriendsAchievementFeedFullscreenCompanionSettings Settings => settings.Settings;

        public override Guid Id { get; } =
            Guid.Parse("9fc60469-d8db-4dec-b5c8-65b4b5a88123");

        private string CachePath => Path.Combine(
            PlayniteApi.Paths.ExtensionsDataPath,
            SourcePluginId.ToString(),
            CacheFileName
        );

        private string AvatarCacheDir =>
    Path.Combine(PlayniteApi.Paths.ExtensionsDataPath, Id.ToString(), "AvatarCache");

        private readonly SemaphoreSlim avatarDlSemaphore = new SemaphoreSlim(2, 2); 
        private readonly ConcurrentDictionary<string, byte> avatarDlInFlight = new ConcurrentDictionary<string, byte>();
        private CancellationTokenSource avatarDlCts = new CancellationTokenSource();

        private static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        private FileSystemWatcher cacheWatcher;
        private readonly object reloadGate = new object();
        private System.Timers.Timer reloadDebounceTimer;
        private int reloadInProgress = 0;
        private readonly object avatarSaveGate = new object();
        private System.Timers.Timer avatarIndexSaveDebounceTimer;

        private const int AvatarTtlDays = 30;       
        private const int AvatarCleanupAfterDays = 60; 
        private const int AvatarMaxFiles = 500;     

        private readonly object avatarIndexGate = new object();
        private Dictionary<string, AvatarIndexEntry> avatarIndex = new Dictionary<string, AvatarIndexEntry>(StringComparer.OrdinalIgnoreCase);

        private string AvatarIndexPath => Path.Combine(AvatarCacheDir, "avatar_index.json");

        private class AvatarIndexEntry
        {
            public string Url { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty; 
            public DateTime LastSuccessUtc { get; set; }          
            public DateTime LastSeenUtc { get; set; }             
        }

        private string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(input.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());

            if (cleaned.Length > 80)
                cleaned = cleaned.Substring(0, 80);

            return cleaned;
        }

        private string GetAvatarCacheFilePath(string steamId)
        {
            var safeId = SanitizeFileName(steamId);
            if (string.IsNullOrWhiteSpace(safeId))
                return string.Empty;

            Directory.CreateDirectory(AvatarCacheDir);
            return Path.Combine(AvatarCacheDir, safeId + ".jpg");
        }

        private void LoadAvatarIndex()
        {
            try
            {
                Directory.CreateDirectory(AvatarCacheDir);

                if (!File.Exists(AvatarIndexPath))
                    return;

                var json = File.ReadAllText(AvatarIndexPath);
                var dict = Serialization.FromJson<Dictionary<string, AvatarIndexEntry>>(json);

                if (dict != null)
                {
                    lock (avatarIndexGate)
                    {
                        avatarIndex = new Dictionary<string, AvatarIndexEntry>(dict, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to load avatar index (will rebuild gradually).");
            }
        }

        private void SaveAvatarIndex()
        {
            try
            {
                Directory.CreateDirectory(AvatarCacheDir);

                Dictionary<string, AvatarIndexEntry> snapshot;
                lock (avatarIndexGate)
                {
                    snapshot = new Dictionary<string, AvatarIndexEntry>(avatarIndex, StringComparer.OrdinalIgnoreCase);
                }

                var json = Serialization.ToJson(snapshot, true);
                File.WriteAllText(AvatarIndexPath, json);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to save avatar index.");
            }
        }

        private void DebouncedSaveAvatarIndex()
        {
            try
            {
                lock (avatarSaveGate)
                {
                    if (avatarIndexSaveDebounceTimer == null)
                    {
                        avatarIndexSaveDebounceTimer = new System.Timers.Timer(1000);
                        avatarIndexSaveDebounceTimer.AutoReset = false;
                        avatarIndexSaveDebounceTimer.Elapsed += (s, e) =>
                        {
                            try { SaveAvatarIndex(); } catch { }
                        };
                    }

                    avatarIndexSaveDebounceTimer.Stop();
                    avatarIndexSaveDebounceTimer.Start();
                }
            }
            catch
            {
                
            }
        }


        private string GetAvatarExtensionFromResponse(HttpResponseMessage resp)
        {
            try
            {
                var mediaType = resp?.Content?.Headers?.ContentType?.MediaType;
                if (string.IsNullOrWhiteSpace(mediaType))
                    return ".jpg";

                mediaType = mediaType.ToLowerInvariant();

                if (mediaType.Contains("png")) return ".png";
                if (mediaType.Contains("webp")) return ".webp";
                if (mediaType.Contains("jpeg") || mediaType.Contains("jpg")) return ".jpg";

                return ".jpg";
            }
            catch
            {
                return ".jpg";
            }
        }

        private string GetAvatarCacheFilePathFromIndex(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return string.Empty;

            var safeId = SanitizeFileName(steamId);
            if (string.IsNullOrWhiteSpace(safeId))
                return string.Empty;

            lock (avatarIndexGate)
            {
                if (avatarIndex.TryGetValue(steamId, out var entry) && !string.IsNullOrWhiteSpace(entry.FileName))
                {
                    return Path.Combine(AvatarCacheDir, entry.FileName);
                }
            }

            // fallback
            return Path.Combine(AvatarCacheDir, safeId + ".jpg");
        }

        private string TryGetCachedAvatarPath(string steamId, string currentUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(steamId))
                    return string.Empty;

                var now = DateTime.UtcNow;
                var legacyPath = GetAvatarCacheFilePath(steamId);

                AvatarIndexEntry entry = null;
                bool hasEntry = false;

                lock (avatarIndexGate)
                {
                    hasEntry = avatarIndex.TryGetValue(steamId, out entry);
                }

                if (!hasEntry)
                {
                    if (!string.IsNullOrWhiteSpace(legacyPath) && File.Exists(legacyPath))
                    {
                        lock (avatarIndexGate)
                        {
                            avatarIndex[steamId] = new AvatarIndexEntry
                            {
                                Url = currentUrl ?? string.Empty,
                                FileName = Path.GetFileName(legacyPath),
                                LastSuccessUtc = now,
                                LastSeenUtc = now
                            };
                        }

                        DebouncedSaveAvatarIndex();
                        return legacyPath;
                    }

                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(entry.FileName))
                    return string.Empty;

                var path = Path.Combine(AvatarCacheDir, entry.FileName);

                if (!File.Exists(path))
                    return string.Empty;

                if (!string.IsNullOrWhiteSpace(currentUrl) &&
                    !string.IsNullOrWhiteSpace(entry.Url) &&
                    !string.Equals(entry.Url, currentUrl, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (entry.LastSuccessUtc <= now.AddDays(-AvatarTtlDays))
                    return string.Empty;

                return path;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void MarkAvatarSeen(string steamId, string url)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            bool changed = false;

            lock (avatarIndexGate)
            {
                if (!avatarIndex.TryGetValue(steamId, out var entry))
                {
                    entry = new AvatarIndexEntry();
                    avatarIndex[steamId] = entry;
                    changed = true;
                }

                entry.LastSeenUtc = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(entry.Url))
                {
                    entry.Url = url;
                    changed = true;
                }
            }

            if (changed)
            {
                DebouncedSaveAvatarIndex();
            }
        }


        private void CleanupAvatarCache(HashSet<string> usedSteamIds)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(AvatarCacheDir);

                    List<string> files;
                    try
                    {
                        files = Directory.GetFiles(AvatarCacheDir)
                            .Where(f => !f.EndsWith("avatar_index.json", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    catch
                    {
                        return;
                    }

                    List<string> toDelete = new List<string>();

                    lock (avatarIndexGate)
                    {
                        var now = DateTime.UtcNow;

                        foreach (var kv in avatarIndex.ToList())
                        {
                            var steamId = kv.Key;
                            var entry = kv.Value;

                            var stillUsed = usedSteamIds != null && usedSteamIds.Contains(steamId);
                            var tooOld = entry.LastSeenUtc <= now.AddDays(-AvatarCleanupAfterDays);

                            if (!stillUsed && tooOld)
                            {
                                if (!string.IsNullOrWhiteSpace(entry.FileName))
                                {
                                    var fp = Path.Combine(AvatarCacheDir, entry.FileName);
                                    toDelete.Add(fp);
                                }

                                avatarIndex.Remove(steamId);
                            }
                        }
                    }

                    foreach (var fp in toDelete.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        try { if (File.Exists(fp)) File.Delete(fp); } catch { }
                    }

                    files = Directory.GetFiles(AvatarCacheDir)
                        .Where(f => !f.EndsWith("avatar_index.json", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (files.Count > AvatarMaxFiles)
                    {
                        var ordered = files
                            .Select(f => new FileInfo(f))
                            .OrderBy(fi => fi.LastWriteTimeUtc)
                            .ToList();

                        var extra = ordered.Count - AvatarMaxFiles;
                        foreach (var fi in ordered.Take(extra))
                        {
                            try { fi.Delete(); } catch { }
                        }
                    }

                    DebouncedSaveAvatarIndex();
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Avatar cache cleanup failed.");
                }
            });
        }


        private void QueueAvatarDownload(string steamId, string url)
        {
            if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(url))
                return;

            if (!settings.Settings.IsEnabled)
                return;

            var cached = TryGetCachedAvatarPath(steamId, url);
            if (!string.IsNullOrEmpty(cached))
                return;

            if (!avatarDlInFlight.TryAdd(steamId, 0))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await avatarDlSemaphore.WaitAsync(avatarDlCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    avatarDlInFlight.TryRemove(steamId, out _);
                    return;
                }

                string tmpPath = null;

                try
                {
                    var cached2 = TryGetCachedAvatarPath(steamId, url);
                    if (!string.IsNullOrEmpty(cached2))
                    {
                        UpdateAvatarPathInUi(steamId, cached2);
                        return;
                    }

                    var safeId = SanitizeFileName(steamId);
                    if (string.IsNullOrWhiteSpace(safeId))
                        return;

                    using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        req.Headers.TryAddWithoutValidation("User-Agent", "Playnite");

                        using (var resp = await http.SendAsync(
                            req,
                            HttpCompletionOption.ResponseHeadersRead,
                            avatarDlCts.Token
                        ).ConfigureAwait(false))
                        {
                            if (!resp.IsSuccessStatusCode)
                                return;

                            var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            if (bytes == null || bytes.Length < 32)
                                return;

                            Directory.CreateDirectory(AvatarCacheDir);

                            var ext = GetAvatarExtensionFromResponse(resp);
                            var finalName = safeId + ext;
                            var finalPath = Path.Combine(AvatarCacheDir, finalName);

                            tmpPath = finalPath + ".tmp";

                            File.WriteAllBytes(tmpPath, bytes);

                            if (File.Exists(finalPath))
                                File.Delete(finalPath);

                            File.Move(tmpPath, finalPath);
                            tmpPath = null; 

                            lock (avatarIndexGate)
                            {
                                if (!avatarIndex.TryGetValue(steamId, out var entry))
                                {
                                    entry = new AvatarIndexEntry();
                                    avatarIndex[steamId] = entry;
                                }

                                entry.Url = url ?? string.Empty;
                                entry.FileName = finalName;
                                entry.LastSuccessUtc = DateTime.UtcNow;
                                entry.LastSeenUtc = DateTime.UtcNow;
                            }

                            DebouncedSaveAvatarIndex();

                            UpdateAvatarPathInUi(steamId, finalPath);
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    logger.Debug(ex, $"Avatar download failed for {steamId}");
                }
                finally
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(tmpPath) && File.Exists(tmpPath))
                            File.Delete(tmpPath);
                    }
                    catch { }

                    avatarDlSemaphore.Release();
                    avatarDlInFlight.TryRemove(steamId, out _);
                }
            });
        }



        private void UpdateAvatarPathInUi(string steamId, string localPath)
        {
            if (string.IsNullOrWhiteSpace(steamId) || string.IsNullOrWhiteSpace(localPath))
                return;

            try
            {
                PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                {
                    foreach (var g in settings.Settings.FriendGroups)
                    {
                        if (!string.IsNullOrWhiteSpace(g.FriendSteamId) &&
                            string.Equals(g.FriendSteamId.Trim(), steamId.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            g.FriendAvatarPath = localPath;
                        }
                    }

                    foreach (var it in settings.Settings.FriendAchievements)
                    {
                        if (!string.IsNullOrWhiteSpace(it.FriendSteamId) &&
                            string.Equals(it.FriendSteamId.Trim(), steamId.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            it.FriendAvatarPath = localPath;
                        }
                    }
                });
            }
            catch
            {
                
            }
        }



        public FriendsAchievementFeedFullscreenCompanion(IPlayniteAPI api) : base(api)
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            settings = new FriendsAchievementFeedFullscreenCompanionSettingsViewModel(this);

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };

            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = "FriendsAchievementFeedFullscreenCompanion",
                SettingsRoot = nameof(Settings)
            });


        }

        // ============================
        // UNIQUE POINT D’ENTRÉE
        // ============================
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            LoadAvatarIndex();
            LoadOnce();
            StartCacheWatcher();
        }

        private void StartCacheWatcher()
        {
            try
            {
                StopCacheWatcher();

                var dir = Path.GetDirectoryName(CachePath);
                var file = Path.GetFileName(CachePath);

                if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(file))
                {
                    logger.Warn("Cache watcher not started (invalid cache path).");
                    return;
                }

                reloadDebounceTimer = new System.Timers.Timer(400);
                reloadDebounceTimer.AutoReset = false;
                reloadDebounceTimer.Elapsed += (s, e) => TriggerReloadFromWatcher();

                cacheWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };

                cacheWatcher.Changed += OnCacheFileEvent;
                cacheWatcher.Created += OnCacheFileEvent;
                cacheWatcher.Renamed += OnCacheFileRenamed;
                cacheWatcher.Deleted += OnCacheFileEvent;
                cacheWatcher.EnableRaisingEvents = true;

                logger.Info($"Cache watcher started on: {CachePath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start cache watcher.");
            }
        }

        private void StopCacheWatcher()
        {
            try
            {
                if (cacheWatcher != null)
                {
                    cacheWatcher.EnableRaisingEvents = false;
                    cacheWatcher.Changed -= OnCacheFileEvent;
                    cacheWatcher.Created -= OnCacheFileEvent;
                    cacheWatcher.Renamed -= OnCacheFileRenamed;
                    cacheWatcher.Deleted -= OnCacheFileEvent;
                    cacheWatcher.Dispose();
                    cacheWatcher = null;
                }
            }
            catch { }

            try
            {
                if (reloadDebounceTimer != null)
                {
                    reloadDebounceTimer.Stop();
                    reloadDebounceTimer.Dispose();
                    reloadDebounceTimer = null;
                }
            }
            catch { }
        }

        private void OnCacheFileEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                lock (reloadGate)
                {
                    if (reloadDebounceTimer == null)
                        return;

                    reloadDebounceTimer.Stop();
                    reloadDebounceTimer.Start();
                }
            }
            catch { }
        }

        private void OnCacheFileRenamed(object sender, RenamedEventArgs e)
        {
            OnCacheFileEvent(sender, e);
        }

        private void TriggerReloadFromWatcher()
        {
            if (Interlocked.CompareExchange(ref reloadInProgress, 1, 0) != 0)
                return;

            _ = Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(150);

                    if (!settings.Settings.IsEnabled)
                        return;

                    LoadOnce();
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Reload from watcher failed.");
                }
                finally
                {
                    Interlocked.Exchange(ref reloadInProgress, 0);
                }
            });
        }


        // ============================
        // Core logic (load once)
        // ============================
        private void LoadOnce()
        {
            try
            {
                // Plugin désactivé -> on ne fait rien + on clean l'UI
                if (!settings.Settings.IsEnabled)
                {
                    PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                    {
                        settings.SetError(string.Empty);

                        // On vide les groupes pour éviter d'afficher de vieux items
                        settings.Settings.FriendGroups?.Clear();

                        settings.Settings.HasFriendsAchievements = false;
                        settings.Settings.TotalItems = 0;
                        settings.Settings.LastRefreshLocal = DateTime.Now;
                    });
                    return;
                }

                // Cache absent -> message + clean
                if (!File.Exists(CachePath))
                {
                    PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                    {
                        settings.SetError("FriendsAchievementFeed cache not found. Install and run the original plugin first.");

                        settings.Settings.FriendGroups?.Clear();

                        settings.Settings.HasFriendsAchievements = false;
                        settings.Settings.TotalItems = 0;
                        settings.Settings.LastRefreshLocal = DateTime.Now;
                    });
                    return;
                }

                // Lire le JSON (ReadWrite = le plugin original peut écrire en même temps)
                string json;
                using (var fs = new FileStream(CachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    json = sr.ReadToEnd();
                }

                var cache = Serialization.FromJson<FriendAchievementCache>(json);
                var entries = cache?.Entries ?? new List<FriendAchievementEntry>();

                // Mapper en ViewModels
                var views = entries
                    .Select(e =>
                    {
                        DateTimeOffset? unlockUtc = null;
                        try
                        {
                            unlockUtc = MsJsonDate.Parse(e.FriendUnlockTimeUtc);
                        }
                        catch
                        {
                            unlockUtc = null;
                        }

                        var avatarUrl = e.FriendAvatarUrl ?? string.Empty;
                        var steamId = e.FriendSteamId ?? string.Empty;
                        var cached = TryGetCachedAvatarPath(steamId, avatarUrl);

                        return new FriendAchievementEntryView
                        {
                            AchievementApiName = e.AchievementApiName ?? string.Empty,
                            AchievementDisplayName = e.AchievementDisplayName ?? string.Empty,
                            AchievementDescription = e.AchievementDescription ?? string.Empty,

                            AppId = e.AppId,
                            PlayniteGameId = e.PlayniteGameId,
                            GameName = e.GameName ?? string.Empty,

                            FriendPersonaName = e.FriendPersonaName ?? string.Empty,
                            FriendAvatarUrl = avatarUrl,
                            FriendSteamId = steamId,
                            FriendAvatarPath = !string.IsNullOrEmpty(cached) ? cached : avatarUrl,

                            FriendAchievementIcon = e.FriendAchievementIcon ?? string.Empty,

                            IsRevealed = e.IsRevealed,
                            HideAchievementsLockedForSelf = e.HideAchievementsLockedForSelf,

                            UnlockTimeUtc = unlockUtc
                        };
                    })
                    .OrderByDescending(v => v.UnlockTimeUtc ?? DateTimeOffset.MinValue)
                    .ToList();

                // IMPORTANT : on alimente UNIQUEMENT FriendGroups (accordion)
                PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                {
                    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in views)
                    {
                        if (!string.IsNullOrWhiteSpace(v.FriendSteamId))
                        {
                            used.Add(v.FriendSteamId);
                            MarkAvatarSeen(v.FriendSteamId, v.FriendAvatarUrl);
                        }
                    }
                    CleanupAvatarCache(used);

                    settings.UpdateAccordionGroups(views);
                    settings.Settings.TotalItems = views.Count;
                    settings.Settings.HasFriendsAchievements = views.Count > 0;
                    settings.Settings.LastRefreshLocal = DateTime.Now;

                    // avatars en fond (non bloquant)
                    foreach (var v in views)
                    {
                        if (!string.IsNullOrWhiteSpace(v.FriendSteamId) && !string.IsNullOrWhiteSpace(v.FriendAvatarUrl))
                        {
                            QueueAvatarDownload(v.FriendSteamId, v.FriendAvatarUrl);
                        }
                    }                  

                    settings.SetError(string.Empty);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load FriendsAchievementFeed cache.");

                PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                {
                    settings.SetError("Error while reading FriendsAchievementFeed cache.");

                    settings.Settings.FriendGroups?.Clear();

                    settings.Settings.HasFriendsAchievements = false;
                    settings.Settings.TotalItems = 0;
                    settings.Settings.LastRefreshLocal = DateTime.Now;
                });
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            StopCacheWatcher();

            try
            {
                lock (avatarSaveGate)
                {
                    if (avatarIndexSaveDebounceTimer != null)
                    {
                        avatarIndexSaveDebounceTimer.Stop();
                        try { SaveAvatarIndex(); } catch { }
                        avatarIndexSaveDebounceTimer.Dispose();
                        avatarIndexSaveDebounceTimer = null;
                    }
                }

            }
            catch { }

            try
            {
                avatarDlCts.Cancel();
                avatarDlCts.Dispose();
            }
            catch { }
        }



    }
}
