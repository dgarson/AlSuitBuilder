using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace AlSuitBuilder.Tests.Persistence
{
    /// <summary>
    /// Unit tests for PersistentBuildState model.
    /// </summary>
    public class PersistentBuildStateTests
    {
        [Fact]
        public void Constructor_InitializesCollections()
        {
            // Act
            var state = new PersistentBuildState();

            // Assert
            Assert.NotNull(state.WorkItems);
            Assert.NotNull(state.CompletedWorkItemIds);
            Assert.Empty(state.WorkItems);
            Assert.Empty(state.CompletedWorkItemIds);
        }

        [Fact]
        public void DefaultVersion_IsOne()
        {
            // Act
            var state = new PersistentBuildState();

            // Assert
            Assert.Equal(1, state.Version);
        }

        [Fact]
        public void WorkItems_CanBeAdded()
        {
            // Arrange
            var state = new PersistentBuildState();
            var item1 = TestDataFactory.CreatePersistentWorkItem(1);
            var item2 = TestDataFactory.CreatePersistentWorkItem(2);

            // Act
            state.WorkItems.Add(item1);
            state.WorkItems.Add(item2);

            // Assert
            Assert.Equal(2, state.WorkItems.Count);
        }

        [Fact]
        public void CompletedWorkItemIds_CanBeAdded()
        {
            // Arrange
            var state = new PersistentBuildState();

            // Act
            state.CompletedWorkItemIds.Add(1);
            state.CompletedWorkItemIds.Add(5);
            state.CompletedWorkItemIds.Add(10);

            // Assert
            Assert.Equal(3, state.CompletedWorkItemIds.Count);
            Assert.Contains(5, state.CompletedWorkItemIds);
        }

        [Fact]
        public void AllPropertiesCanBeSet()
        {
            // Arrange
            var now = DateTime.Now;
            var buildId = Guid.NewGuid().ToString();

            // Act
            var state = new PersistentBuildState
            {
                Version = 2,
                BuildId = buildId,
                Name = "TestSuit.alb",
                DropCharacter = "MainChar",
                RelayCharacter = "RelayChar",
                InitiatedId = 42,
                StartTime = now,
                EndTime = now.AddHours(1),
                LastSaveTime = now.AddMinutes(30),
                Status = BuildStatus.Completed,
                TotalItemCount = 100,
                OriginalFilePath = "/path/to/file.alb"
            };

            // Assert
            Assert.Equal(2, state.Version);
            Assert.Equal(buildId, state.BuildId);
            Assert.Equal("TestSuit.alb", state.Name);
            Assert.Equal("MainChar", state.DropCharacter);
            Assert.Equal("RelayChar", state.RelayCharacter);
            Assert.Equal(42, state.InitiatedId);
            Assert.Equal(now, state.StartTime);
            Assert.Equal(now.AddHours(1), state.EndTime);
            Assert.Equal(now.AddMinutes(30), state.LastSaveTime);
            Assert.Equal(BuildStatus.Completed, state.Status);
            Assert.Equal(100, state.TotalItemCount);
            Assert.Equal("/path/to/file.alb", state.OriginalFilePath);
        }
    }

    /// <summary>
    /// Unit tests for PersistentWorkItem model.
    /// </summary>
    public class PersistentWorkItemTests
    {
        [Fact]
        public void AllPropertiesCanBeSet()
        {
            // Arrange
            var now = DateTime.Now;

            // Act
            var item = new PersistentWorkItem
            {
                Id = 123,
                Character = "TestChar",
                ItemName = "Test Sword",
                Requirements = new[] { 1, 2, 3 },
                MaterialId = 50,
                SetId = 10,
                Burden = 500,
                Value = 10000,
                LastAttempt = now,
                Status = WorkItemStatus.Completed,
                AttemptCount = 3,
                LastError = "Some error"
            };

            // Assert
            Assert.Equal(123, item.Id);
            Assert.Equal("TestChar", item.Character);
            Assert.Equal("Test Sword", item.ItemName);
            Assert.Equal(3, item.Requirements.Length);
            Assert.Equal(50, item.MaterialId);
            Assert.Equal(10, item.SetId);
            Assert.Equal(500, item.Burden);
            Assert.Equal(10000, item.Value);
            Assert.Equal(now, item.LastAttempt);
            Assert.Equal(WorkItemStatus.Completed, item.Status);
            Assert.Equal(3, item.AttemptCount);
            Assert.Equal("Some error", item.LastError);
        }

        [Fact]
        public void DefaultStatus_IsPending()
        {
            // Act
            var item = new PersistentWorkItem();

            // Assert
            Assert.Equal(WorkItemStatus.Pending, item.Status);
        }
    }

    /// <summary>
    /// Unit tests for BuildHistoryEntry model.
    /// </summary>
    public class BuildHistoryEntryTests
    {
        [Fact]
        public void AllPropertiesCanBeSet()
        {
            // Arrange
            var start = DateTime.Now.AddHours(-1);
            var end = DateTime.Now;

            // Act
            var entry = new BuildHistoryEntry
            {
                BuildId = "abc-123",
                SuitName = "MySuit.alb",
                DropCharacter = "MainChar",
                StartTime = start,
                EndTime = end,
                FinalStatus = BuildStatus.Completed,
                TotalItems = 50,
                CompletedItems = 48,
                FailedItems = 2,
                WasResumed = true,
                LogFilePath = "/logs/build.log"
            };

            // Assert
            Assert.Equal("abc-123", entry.BuildId);
            Assert.Equal("MySuit.alb", entry.SuitName);
            Assert.Equal("MainChar", entry.DropCharacter);
            Assert.Equal(start, entry.StartTime);
            Assert.Equal(end, entry.EndTime);
            Assert.Equal(BuildStatus.Completed, entry.FinalStatus);
            Assert.Equal(50, entry.TotalItems);
            Assert.Equal(48, entry.CompletedItems);
            Assert.Equal(2, entry.FailedItems);
            Assert.True(entry.WasResumed);
            Assert.Equal("/logs/build.log", entry.LogFilePath);
        }
    }

    /// <summary>
    /// Unit tests for BuildEventLog model.
    /// </summary>
    public class BuildEventLogTests
    {
        [Fact]
        public void DefaultConstructor_SetsTimestamp()
        {
            // Arrange
            var before = DateTime.Now;

            // Act
            var log = new BuildEventLog();
            var after = DateTime.Now;

            // Assert
            Assert.True(log.Timestamp >= before && log.Timestamp <= after);
        }

        [Fact]
        public void ParameterizedConstructor_SetsValues()
        {
            // Act
            var log = new BuildEventLog(BuildEventType.BuildCompleted, "Build finished successfully");

            // Assert
            Assert.Equal(BuildEventType.BuildCompleted, log.EventType);
            Assert.Equal("Build finished successfully", log.Message);
        }

        [Fact]
        public void AllPropertiesCanBeSet()
        {
            // Arrange
            var now = DateTime.Now;

            // Act
            var log = new BuildEventLog
            {
                Timestamp = now,
                EventType = BuildEventType.WorkItemFailed,
                Message = "Item delivery failed",
                WorkItemId = 42,
                CharacterName = "FailChar",
                Details = "Player not nearby"
            };

            // Assert
            Assert.Equal(now, log.Timestamp);
            Assert.Equal(BuildEventType.WorkItemFailed, log.EventType);
            Assert.Equal("Item delivery failed", log.Message);
            Assert.Equal(42, log.WorkItemId);
            Assert.Equal("FailChar", log.CharacterName);
            Assert.Equal("Player not nearby", log.Details);
        }
    }
}
