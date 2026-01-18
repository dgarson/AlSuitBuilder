using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Tests.TestHelpers;
using System;
using Xunit;

namespace AlSuitBuilder.Tests.Actions
{
    /// <summary>
    /// Unit tests for resume build functionality.
    /// Note: Full integration tests would require mocking the Program class.
    /// These tests focus on the data layer and conversion logic.
    /// </summary>
    public class ResumeBuildActionTests
    {
        #region State Conversion for Resume

        [Fact]
        public void ToBuildInfo_FiltersCompletedItems()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 5);
            state.WorkItems[0].Status = WorkItemStatus.Completed;
            state.WorkItems[1].Status = WorkItemStatus.Completed;
            state.WorkItems[2].Status = WorkItemStatus.Pending;
            state.WorkItems[3].Status = WorkItemStatus.InProgress;
            state.WorkItems[4].Status = WorkItemStatus.Failed;

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            // Should only include Pending, InProgress, and Failed items (not Completed)
            Assert.Equal(3, buildInfo.WorkItems.Count);
        }

        [Fact]
        public void ToBuildInfo_ResetsInProgressLastAttempt()
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
            // In-progress items should have LastAttempt reset to allow immediate retry
            var inProgressItem = buildInfo.WorkItems.Find(w => w.Id == state.WorkItems[0].Id);
            var pendingItem = buildInfo.WorkItems.Find(w => w.Id == state.WorkItems[1].Id);

            Assert.Equal(DateTime.MinValue, inProgressItem.LastAttempt);
            Assert.NotEqual(DateTime.MinValue, pendingItem.LastAttempt);
        }

        [Fact]
        public void ToBuildInfo_PreservesBuildMetadata()
        {
            // Arrange
            var buildId = Guid.NewGuid().ToString();
            var state = new PersistentBuildState
            {
                BuildId = buildId,
                Name = "TestSuit.alb",
                DropCharacter = "MainChar",
                RelayCharacter = "RelayChar",
                InitiatedId = 42,
                StartTime = DateTime.Now.AddHours(-1)
            };

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.Equal(buildId, buildInfo.BuildId);
            Assert.Equal("TestSuit.alb", buildInfo.Name);
            Assert.Equal("MainChar", buildInfo.DropCharacter);
            Assert.Equal("RelayChar", buildInfo.RelayCharacter);
            Assert.Equal(42, buildInfo.InitiatedId);
        }

        #endregion

        #region Crash Detection Scenarios

        [Fact]
        public void CrashedBuild_HasActiveStatus()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(status: BuildStatus.Active);

            // Act & Assert
            // A build with Active status found on startup indicates a crash
            Assert.Equal(BuildStatus.Active, state.Status);
        }

        [Fact]
        public void CrashedBuild_MarkAsRecoverable()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(status: BuildStatus.Active);

            // Act
            state.Status = BuildStatus.Crashed;

            // Assert
            Assert.Equal(BuildStatus.Crashed, state.Status);
        }

        [Fact]
        public void ResumedBuild_StatusIsActive()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(status: BuildStatus.Crashed);

            // Act (simulating resume)
            state.Status = BuildStatus.Active;

            // Assert
            Assert.Equal(BuildStatus.Active, state.Status);
        }

        #endregion

        #region Work Item Recovery

        [Fact]
        public void InProgressItems_IncrementAttemptCountOnRecovery()
        {
            // Arrange
            var item = TestDataFactory.CreatePersistentWorkItem(status: WorkItemStatus.InProgress);
            item.AttemptCount = 2;

            // Act (simulating recovery)
            item.Status = WorkItemStatus.Pending;
            item.AttemptCount++;

            // Assert
            Assert.Equal(WorkItemStatus.Pending, item.Status);
            Assert.Equal(3, item.AttemptCount);
        }

        [Fact]
        public void FailedItems_IncludedInResume()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 3);
            state.WorkItems[0].Status = WorkItemStatus.Completed;
            state.WorkItems[1].Status = WorkItemStatus.Failed;
            state.WorkItems[2].Status = WorkItemStatus.Pending;

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            // Failed items should be included so they can be retried
            Assert.Equal(2, buildInfo.WorkItems.Count);
            Assert.Contains(buildInfo.WorkItems, w => w.Id == state.WorkItems[1].Id);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void EmptyWorkItems_ReturnsEmptyBuildInfo()
        {
            // Arrange
            var state = new PersistentBuildState
            {
                BuildId = Guid.NewGuid().ToString(),
                Name = "Empty.alb"
            };

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.NotNull(buildInfo.WorkItems);
            Assert.Empty(buildInfo.WorkItems);
        }

        [Fact]
        public void AllItemsCompleted_ReturnsEmptyWorkItems()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 3);
            foreach (var item in state.WorkItems)
            {
                item.Status = WorkItemStatus.Completed;
            }

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.Empty(buildInfo.WorkItems);
        }

        [Fact]
        public void WorkItemRequirements_PreservedOnConversion()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 1);
            state.WorkItems[0].Requirements = new[] { 101, 202, 303, 404 };
            state.WorkItems[0].MaterialId = 99;
            state.WorkItems[0].SetId = 15;

            // Act
            var buildInfo = BuildPersistenceManager.ToBuildInfo(state);

            // Assert
            Assert.Single(buildInfo.WorkItems);
            Assert.Equal(4, buildInfo.WorkItems[0].Requirements.Length);
            Assert.Equal(99, buildInfo.WorkItems[0].MaterialId);
            Assert.Equal(15, buildInfo.WorkItems[0].SetId);
        }

        #endregion
    }
}
