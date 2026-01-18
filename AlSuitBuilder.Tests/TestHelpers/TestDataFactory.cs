using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using System;
using System.Collections.Generic;

namespace AlSuitBuilder.Tests.TestHelpers
{
    /// <summary>
    /// Factory for creating test data objects.
    /// </summary>
    public static class TestDataFactory
    {
        /// <summary>
        /// Creates a sample WorkItem for testing.
        /// </summary>
        public static WorkItem CreateWorkItem(int id = 1, string character = "TestCharacter")
        {
            return new WorkItem
            {
                Id = id,
                Character = character,
                ItemName = $"Test Item {id}",
                Requirements = new[] { 100, 200, 300 },
                MaterialId = 10,
                SetId = 5,
                Burden = 100,
                Value = 1000,
                LastAttempt = DateTime.MinValue
            };
        }

        /// <summary>
        /// Creates a sample PersistentWorkItem for testing.
        /// </summary>
        public static PersistentWorkItem CreatePersistentWorkItem(int id = 1, WorkItemStatus status = WorkItemStatus.Pending)
        {
            return new PersistentWorkItem
            {
                Id = id,
                Character = "TestCharacter",
                ItemName = $"Test Item {id}",
                Requirements = new[] { 100, 200, 300 },
                MaterialId = 10,
                SetId = 5,
                Burden = 100,
                Value = 1000,
                LastAttempt = DateTime.MinValue,
                Status = status,
                AttemptCount = 0
            };
        }

        /// <summary>
        /// Creates a sample PersistentBuildState for testing.
        /// </summary>
        public static PersistentBuildState CreatePersistentBuildState(
            string buildId = null,
            BuildStatus status = BuildStatus.Active,
            int workItemCount = 5)
        {
            var state = new PersistentBuildState
            {
                Version = 1,
                BuildId = buildId ?? Guid.NewGuid().ToString(),
                Name = "TestSuit.alb",
                DropCharacter = "MainCharacter",
                RelayCharacter = null,
                InitiatedId = 1,
                StartTime = DateTime.Now.AddMinutes(-10),
                Status = status,
                TotalItemCount = workItemCount,
                OriginalFilePath = "/path/to/TestSuit.alb"
            };

            for (int i = 1; i <= workItemCount; i++)
            {
                state.WorkItems.Add(CreatePersistentWorkItem(i));
            }

            return state;
        }

        /// <summary>
        /// Creates a sample BuildHistoryEntry for testing.
        /// </summary>
        public static BuildHistoryEntry CreateBuildHistoryEntry(
            BuildStatus status = BuildStatus.Completed,
            int totalItems = 10,
            int completedItems = 10)
        {
            return new BuildHistoryEntry
            {
                BuildId = Guid.NewGuid().ToString(),
                SuitName = "TestSuit.alb",
                DropCharacter = "MainCharacter",
                StartTime = DateTime.Now.AddHours(-1),
                EndTime = DateTime.Now,
                FinalStatus = status,
                TotalItems = totalItems,
                CompletedItems = completedItems,
                FailedItems = totalItems - completedItems,
                WasResumed = false,
                LogFilePath = "/path/to/log.txt"
            };
        }

        /// <summary>
        /// Creates a BuildInfo for testing with the server.
        /// </summary>
        public static AlSuitBuilder.Server.BuildInfo CreateBuildInfo(
            string buildId = null,
            int workItemCount = 5)
        {
            var buildInfo = new AlSuitBuilder.Server.BuildInfo
            {
                BuildId = buildId ?? Guid.NewGuid().ToString(),
                Name = "TestSuit.alb",
                DropCharacter = "MainCharacter",
                InitiatedId = 1,
                StartTime = DateTime.Now.AddMinutes(-10),
                WorkItems = new List<WorkItem>()
            };

            for (int i = 1; i <= workItemCount; i++)
            {
                buildInfo.WorkItems.Add(CreateWorkItem(i));
            }

            return buildInfo;
        }
    }
}
