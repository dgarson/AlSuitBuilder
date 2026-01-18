using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Tests.TestHelpers;
using System;
using System.IO;
using Xunit;

namespace AlSuitBuilder.Tests.Persistence
{
    /// <summary>
    /// Unit tests for BuildPersistenceManager.
    /// </summary>
    public class BuildPersistenceManagerTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly BuildPersistenceManager _manager;

        public BuildPersistenceManagerTests()
        {
            // Create a unique test directory for each test run
            _testDirectory = Path.Combine(Path.GetTempPath(), $"AlSuitBuilder_Tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);
            _manager = new BuildPersistenceManager(_testDirectory);
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch { }
            }
        }

        #region SaveActiveState Tests

        [Fact]
        public void SaveActiveState_CreatesFile()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState();

            // Act
            _manager.SaveActiveState(state);

            // Assert
            Assert.True(_manager.HasActiveState());
        }

        [Fact]
        public void SaveActiveState_UpdatesLastSaveTime()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState();
            var beforeSave = DateTime.Now;

            // Act
            _manager.SaveActiveState(state);
            var loaded = _manager.LoadActiveState();

            // Assert
            Assert.NotNull(loaded);
            Assert.True(loaded.LastSaveTime >= beforeSave);
        }

        [Fact]
        public void SaveActiveState_ThrowsOnNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _manager.SaveActiveState(null));
        }

        #endregion

        #region LoadActiveState Tests

        [Fact]
        public void LoadActiveState_ReturnsNullWhenNoFile()
        {
            // Act
            var result = _manager.LoadActiveState();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void LoadActiveState_ReturnsCorrectData()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(buildId: "test-build-123");
            state.Name = "MyTestSuit";
            state.TotalItemCount = 10;
            _manager.SaveActiveState(state);

            // Act
            var loaded = _manager.LoadActiveState();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal("test-build-123", loaded.BuildId);
            Assert.Equal("MyTestSuit", loaded.Name);
            Assert.Equal(10, loaded.TotalItemCount);
        }

        [Fact]
        public void LoadActiveState_PreservesWorkItems()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 3);
            _manager.SaveActiveState(state);

            // Act
            var loaded = _manager.LoadActiveState();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded.WorkItems.Count);
            Assert.Equal(1, loaded.WorkItems[0].Id);
            Assert.Equal(2, loaded.WorkItems[1].Id);
            Assert.Equal(3, loaded.WorkItems[2].Id);
        }

        #endregion

        #region ClearActiveState Tests

        [Fact]
        public void ClearActiveState_RemovesFile()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState();
            _manager.SaveActiveState(state);
            Assert.True(_manager.HasActiveState());

            // Act
            _manager.ClearActiveState();

            // Assert
            Assert.False(_manager.HasActiveState());
        }

        [Fact]
        public void ClearActiveState_DoesNotThrowWhenNoFile()
        {
            // Act & Assert (should not throw)
            _manager.ClearActiveState();
        }

        #endregion

        #region History Tests

        [Fact]
        public void AddHistoryEntry_CreatesEntry()
        {
            // Arrange
            var entry = TestDataFactory.CreateBuildHistoryEntry();

            // Act
            _manager.AddHistoryEntry(entry);
            var history = _manager.LoadHistory();

            // Assert
            Assert.Single(history);
            Assert.Equal(entry.SuitName, history[0].SuitName);
        }

        [Fact]
        public void AddHistoryEntry_AppendsToExisting()
        {
            // Arrange
            var entry1 = TestDataFactory.CreateBuildHistoryEntry();
            entry1.SuitName = "First.alb";
            var entry2 = TestDataFactory.CreateBuildHistoryEntry();
            entry2.SuitName = "Second.alb";

            // Act
            _manager.AddHistoryEntry(entry1);
            _manager.AddHistoryEntry(entry2);
            var history = _manager.LoadHistory();

            // Assert
            Assert.Equal(2, history.Count);
            Assert.Equal("First.alb", history[0].SuitName);
            Assert.Equal("Second.alb", history[1].SuitName);
        }

        [Fact]
        public void GetRecentHistory_ReturnsLimitedEntries()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var entry = TestDataFactory.CreateBuildHistoryEntry();
                entry.SuitName = $"Build{i}.alb";
                _manager.AddHistoryEntry(entry);
            }

            // Act
            var recent = _manager.GetRecentHistory(5);

            // Assert
            Assert.Equal(5, recent.Count);
        }

        [Fact]
        public void LoadHistory_ReturnsEmptyWhenNoFile()
        {
            // Act
            var history = _manager.LoadHistory();

            // Assert
            Assert.NotNull(history);
            Assert.Empty(history);
        }

        #endregion

        #region Logging Tests

        [Fact]
        public void StartBuildLog_CreatesLogFile()
        {
            // Arrange
            var buildId = Guid.NewGuid().ToString();

            // Act
            _manager.StartBuildLog(buildId);
            var logPath = _manager.GetCurrentLogPath();

            // Assert
            Assert.NotNull(logPath);
            Assert.True(File.Exists(logPath));

            // Cleanup
            _manager.CloseCurrentLog();
        }

        [Fact]
        public void LogEvent_WritesToFile()
        {
            // Arrange
            var buildId = Guid.NewGuid().ToString();
            _manager.StartBuildLog(buildId);

            // Act
            _manager.LogEvent(new BuildEventLog
            {
                EventType = BuildEventType.BuildStarted,
                Message = "Test build started"
            });
            _manager.CloseCurrentLog();

            // Assert
            var logPath = _manager.GetCurrentLogPath();
            var content = File.ReadAllText(logPath);
            Assert.Contains("BuildStarted", content);
            Assert.Contains("Test build started", content);
        }

        #endregion

        #region Conversion Tests

        [Fact]
        public void FromBuildInfo_CreatesValidState()
        {
            // Arrange
            var buildInfo = TestDataFactory.CreateBuildInfo(workItemCount: 3);

            // Act
            var state = BuildPersistenceManager.FromBuildInfo(buildInfo, "/path/to/file.alb");

            // Assert
            Assert.Equal(buildInfo.BuildId, state.BuildId);
            Assert.Equal(buildInfo.Name, state.Name);
            Assert.Equal(3, state.WorkItems.Count);
            Assert.Equal(3, state.TotalItemCount);
            Assert.Equal(BuildStatus.Active, state.Status);
        }

        [Fact]
        public void ToBuildInfo_CreatesValidBuildInfo()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 3);
            state.WorkItems[0].Status = WorkItemStatus.Completed;

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.Equal(state.BuildId, buildInfo.BuildId);
            Assert.Equal(state.Name, buildInfo.Name);
            // Should only have 2 items (completed one filtered out)
            Assert.Equal(2, buildInfo.WorkItems.Count);
        }

        [Fact]
        public void ToBuildInfo_ResetsInProgressItems()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 2);
            state.WorkItems[0].Status = WorkItemStatus.InProgress;
            state.WorkItems[0].LastAttempt = DateTime.Now;
            state.WorkItems[1].Status = WorkItemStatus.Pending;
            state.WorkItems[1].LastAttempt = DateTime.Now;

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.Equal(2, buildInfo.WorkItems.Count);
            // In-progress item should have reset LastAttempt
            Assert.Equal(DateTime.MinValue, buildInfo.WorkItems[0].LastAttempt);
            // Pending item should keep its LastAttempt
            Assert.NotEqual(DateTime.MinValue, buildInfo.WorkItems[1].LastAttempt);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public void GetCurrentStatistics_ReturnsCorrectCounts()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 5);
            state.WorkItems[0].Status = WorkItemStatus.Completed;
            state.WorkItems[1].Status = WorkItemStatus.Completed;
            state.WorkItems[2].Status = WorkItemStatus.InProgress;
            state.WorkItems[3].Status = WorkItemStatus.Failed;
            state.WorkItems[4].Status = WorkItemStatus.Pending;
            _manager.SaveActiveState(state);

            // Act
            var stats = _manager.GetCurrentStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(5, stats.TotalItems);
            Assert.Equal(2, stats.CompletedItems);
            Assert.Equal(1, stats.InProgressItems);
            Assert.Equal(1, stats.FailedItems);
            Assert.Equal(1, stats.PendingItems);
            Assert.Equal(40.0, stats.ProgressPercentage); // 2/5 = 40%
        }

        [Fact]
        public void GetCurrentStatistics_ReturnsNullWhenNoState()
        {
            // Act
            var stats = _manager.GetCurrentStatistics();

            // Assert
            Assert.Null(stats);
        }

        #endregion
    }
}
