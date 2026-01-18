using AlSuitBuilder.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Manages persistence of build state, history, and event logging.
    /// Provides crash recovery and resume capabilities.
    /// </summary>
    public class BuildPersistenceManager
    {
        private static readonly object _lockObject = new object();

        private readonly string _persistenceDirectory;
        private readonly string _activeStatePath;
        private readonly string _historyPath;
        private readonly string _logsDirectory;

        private StreamWriter _currentLogWriter;
        private string _currentLogPath;
        private string _currentBuildId;

        /// <summary>
        /// Creates a new BuildPersistenceManager with the specified base directory.
        /// </summary>
        /// <param name="baseDirectory">Base directory for persistence files (typically the build directory).</param>
        public BuildPersistenceManager(string baseDirectory)
        {
            _persistenceDirectory = Path.Combine(baseDirectory, "builds");
            _activeStatePath = Path.Combine(_persistenceDirectory, "active_build.json");
            _historyPath = Path.Combine(_persistenceDirectory, "build_history.json");
            _logsDirectory = Path.Combine(_persistenceDirectory, "logs");

            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(_persistenceDirectory))
                Directory.CreateDirectory(_persistenceDirectory);
            if (!Directory.Exists(_logsDirectory))
                Directory.CreateDirectory(_logsDirectory);
        }

        #region State Persistence

        /// <summary>
        /// Saves the current build state to disk atomically.
        /// </summary>
        public void SaveActiveState(PersistentBuildState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            lock (_lockObject)
            {
                try
                {
                    state.LastSaveTime = DateTime.Now;
                    var json = SerializeToJson(state);

                    // Write to temp file first, then rename (atomic operation)
                    var tempPath = _activeStatePath + ".tmp";
                    File.WriteAllText(tempPath, json, Encoding.UTF8);

                    if (File.Exists(_activeStatePath))
                        File.Delete(_activeStatePath);
                    File.Move(tempPath, _activeStatePath);
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Loads the persisted build state from disk.
        /// </summary>
        /// <returns>The persisted state, or null if none exists.</returns>
        public PersistentBuildState LoadActiveState()
        {
            lock (_lockObject)
            {
                if (!File.Exists(_activeStatePath))
                    return null;

                try
                {
                    var json = File.ReadAllText(_activeStatePath, Encoding.UTF8);
                    return DeserializeFromJson<PersistentBuildState>(json);
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Clears the active build state file.
        /// </summary>
        public void ClearActiveState()
        {
            lock (_lockObject)
            {
                try
                {
                    if (File.Exists(_activeStatePath))
                        File.Delete(_activeStatePath);
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Checks if there is a persisted active build state.
        /// </summary>
        public bool HasActiveState()
        {
            return File.Exists(_activeStatePath);
        }

        #endregion

        #region Build History

        /// <summary>
        /// Adds a new entry to the build history.
        /// </summary>
        public void AddHistoryEntry(BuildHistoryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            lock (_lockObject)
            {
                try
                {
                    var history = LoadHistory();
                    history.Add(entry);

                    // Keep last 100 entries
                    while (history.Count > 100)
                        history.RemoveAt(0);

                    var json = SerializeToJson(history);
                    File.WriteAllText(_historyPath, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Loads the build history from disk.
        /// </summary>
        public List<BuildHistoryEntry> LoadHistory()
        {
            lock (_lockObject)
            {
                if (!File.Exists(_historyPath))
                    return new List<BuildHistoryEntry>();

                try
                {
                    var json = File.ReadAllText(_historyPath, Encoding.UTF8);
                    return DeserializeFromJson<List<BuildHistoryEntry>>(json) ?? new List<BuildHistoryEntry>();
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                    return new List<BuildHistoryEntry>();
                }
            }
        }

        /// <summary>
        /// Gets the most recent history entries.
        /// </summary>
        /// <param name="count">Maximum number of entries to return.</param>
        public List<BuildHistoryEntry> GetRecentHistory(int count = 10)
        {
            var history = LoadHistory();
            return history.Skip(Math.Max(0, history.Count - count)).ToList();
        }

        #endregion

        #region Event Logging

        /// <summary>
        /// Starts a new log file for the specified build.
        /// </summary>
        public void StartBuildLog(string buildId)
        {
            CloseCurrentLog();

            lock (_lockObject)
            {
                try
                {
                    _currentBuildId = buildId;
                    var filename = $"build_{DateTime.Now:yyyyMMdd_HHmmss}_{buildId.Substring(0, Math.Min(8, buildId.Length))}.log";
                    _currentLogPath = Path.Combine(_logsDirectory, filename);
                    _currentLogWriter = new StreamWriter(_currentLogPath, true, Encoding.UTF8);
                    _currentLogWriter.AutoFlush = true;

                    _currentLogWriter.WriteLine($"=== Build Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _currentLogWriter.WriteLine($"Build ID: {buildId}");
                    _currentLogWriter.WriteLine(new string('=', 60));
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Logs an event to the current build log.
        /// </summary>
        public void LogEvent(BuildEventLog eventLog)
        {
            if (eventLog == null || _currentLogWriter == null)
                return;

            lock (_lockObject)
            {
                try
                {
                    var line = $"[{eventLog.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{eventLog.EventType,-20}] {eventLog.Message}";

                    if (eventLog.WorkItemId.HasValue)
                        line += $" | WorkItemId: {eventLog.WorkItemId.Value}";

                    if (!string.IsNullOrEmpty(eventLog.CharacterName))
                        line += $" | Character: {eventLog.CharacterName}";

                    if (!string.IsNullOrEmpty(eventLog.Details))
                        line += $" | {eventLog.Details}";

                    _currentLogWriter.WriteLine(line);
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Closes the current build log file.
        /// </summary>
        public void CloseCurrentLog()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_currentLogWriter != null)
                    {
                        _currentLogWriter.WriteLine(new string('=', 60));
                        _currentLogWriter.WriteLine($"=== Build Log Closed: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                        _currentLogWriter.Close();
                        _currentLogWriter.Dispose();
                        _currentLogWriter = null;
                    }
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Gets the path to the current log file.
        /// </summary>
        public string GetCurrentLogPath()
        {
            return _currentLogPath;
        }

        #endregion

        #region JSON Serialization Helpers

        private string SerializeToJson<T>(T obj)
        {
            var settings = new DataContractJsonSerializerSettings
            {
                DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            var serializer = new DataContractJsonSerializer(typeof(T), settings);

            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, obj);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private T DeserializeFromJson<T>(string json)
        {
            var settings = new DataContractJsonSerializerSettings
            {
                DateTimeFormat = new System.Runtime.Serialization.DateTimeFormat("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            var serializer = new DataContractJsonSerializer(typeof(T), settings);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        #endregion

        #region Conversion Helpers

        /// <summary>
        /// Creates a PersistentBuildState from a BuildInfo instance.
        /// </summary>
        public static PersistentBuildState FromBuildInfo(BuildInfo buildInfo, string originalFilePath)
        {
            if (buildInfo == null)
                throw new ArgumentNullException(nameof(buildInfo));

            var state = new PersistentBuildState
            {
                BuildId = buildInfo.BuildId ?? Guid.NewGuid().ToString(),
                Name = buildInfo.Name,
                DropCharacter = buildInfo.DropCharacter,
                RelayCharacter = buildInfo.RelayCharacter,
                InitiatedId = buildInfo.InitiatedId,
                StartTime = buildInfo.StartTime,
                Status = BuildStatus.Active,
                TotalItemCount = buildInfo.WorkItems?.Count ?? 0,
                OriginalFilePath = originalFilePath
            };

            if (buildInfo.WorkItems != null)
            {
                foreach (var item in buildInfo.WorkItems)
                {
                    state.WorkItems.Add(new PersistentWorkItem
                    {
                        Id = item.Id,
                        Character = item.Character,
                        ItemName = item.ItemName,
                        Requirements = item.Requirements,
                        MaterialId = item.MaterialId,
                        SetId = item.SetId,
                        Burden = item.Burden,
                        Value = item.Value,
                        LastAttempt = item.LastAttempt,
                        Status = WorkItemStatus.Pending,
                        AttemptCount = 0
                    });
                }
            }

            return state;
        }

        /// <summary>
        /// Creates a BuildInfo from a PersistentBuildState, filtering out completed items.
        /// </summary>
        public static BuildInfo ToBuildInfo(PersistentBuildState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var buildInfo = new BuildInfo
            {
                BuildId = state.BuildId,
                Name = state.Name,
                DropCharacter = state.DropCharacter,
                RelayCharacter = state.RelayCharacter,
                InitiatedId = state.InitiatedId,
                StartTime = state.StartTime,
                WorkItems = new List<WorkItem>()
            };

            foreach (var pItem in state.WorkItems)
            {
                // Skip completed items
                if (pItem.Status == WorkItemStatus.Completed)
                    continue;

                buildInfo.WorkItems.Add(new WorkItem
                {
                    Id = pItem.Id,
                    Character = pItem.Character,
                    ItemName = pItem.ItemName,
                    Requirements = pItem.Requirements,
                    MaterialId = pItem.MaterialId,
                    SetId = pItem.SetId,
                    Burden = pItem.Burden,
                    Value = pItem.Value,
                    // Reset LastAttempt for items that were in-flight during crash
                    LastAttempt = pItem.Status == WorkItemStatus.InProgress
                        ? DateTime.MinValue
                        : pItem.LastAttempt
                });
            }

            return buildInfo;
        }

        /// <summary>
        /// Updates a PersistentWorkItem from a WorkItem.
        /// </summary>
        public static void UpdatePersistentWorkItem(PersistentWorkItem persistent, WorkItem workItem)
        {
            if (persistent == null || workItem == null)
                return;

            persistent.LastAttempt = workItem.LastAttempt;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets statistics for the current build state.
        /// </summary>
        public BuildStatistics GetCurrentStatistics()
        {
            var state = LoadActiveState();
            if (state == null)
                return null;

            return new BuildStatistics
            {
                BuildId = state.BuildId,
                SuitName = state.Name,
                Status = state.Status,
                TotalItems = state.TotalItemCount,
                CompletedItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Completed),
                PendingItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Pending),
                InProgressItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.InProgress),
                FailedItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Failed),
                StartTime = state.StartTime,
                ElapsedTime = DateTime.Now - state.StartTime
            };
        }

        #endregion
    }

    /// <summary>
    /// Statistics about a build's progress.
    /// </summary>
    public class BuildStatistics
    {
        public string BuildId { get; set; }
        public string SuitName { get; set; }
        public BuildStatus Status { get; set; }
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
        public int PendingItems { get; set; }
        public int InProgressItems { get; set; }
        public int FailedItems { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan ElapsedTime { get; set; }

        public double ProgressPercentage => TotalItems > 0 ? (CompletedItems * 100.0 / TotalItems) : 0;
    }
}
