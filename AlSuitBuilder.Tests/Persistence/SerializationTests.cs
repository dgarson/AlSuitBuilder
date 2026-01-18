using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Tests.TestHelpers;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Xunit;

namespace AlSuitBuilder.Tests.Persistence
{
    /// <summary>
    /// Tests for JSON serialization/deserialization of persistence models.
    /// </summary>
    public class SerializationTests
    {
        #region PersistentBuildState Serialization

        [Fact]
        public void PersistentBuildState_CanBeSerializedAndDeserialized()
        {
            // Arrange
            var original = TestDataFactory.CreatePersistentBuildState(workItemCount: 3);
            original.WorkItems[0].Status = WorkItemStatus.Completed;
            original.CompletedWorkItemIds.Add(1);

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.BuildId, deserialized.BuildId);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Status, deserialized.Status);
            Assert.Equal(3, deserialized.WorkItems.Count);
            Assert.Single(deserialized.CompletedWorkItemIds);
        }

        [Fact]
        public void PersistentBuildState_PreservesAllProperties()
        {
            // Arrange
            var original = new PersistentBuildState
            {
                Version = 1,
                BuildId = "test-id-123",
                Name = "TestBuild.alb",
                DropCharacter = "MainChar",
                RelayCharacter = "RelayChar",
                InitiatedId = 42,
                StartTime = new DateTime(2024, 1, 15, 10, 30, 0),
                EndTime = new DateTime(2024, 1, 15, 11, 45, 0),
                LastSaveTime = new DateTime(2024, 1, 15, 11, 30, 0),
                Status = BuildStatus.Completed,
                TotalItemCount = 25,
                OriginalFilePath = "/path/to/file.alb"
            };

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.Equal(original.Version, deserialized.Version);
            Assert.Equal(original.BuildId, deserialized.BuildId);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.DropCharacter, deserialized.DropCharacter);
            Assert.Equal(original.RelayCharacter, deserialized.RelayCharacter);
            Assert.Equal(original.InitiatedId, deserialized.InitiatedId);
            Assert.Equal(original.TotalItemCount, deserialized.TotalItemCount);
            Assert.Equal(original.OriginalFilePath, deserialized.OriginalFilePath);
        }

        [Fact]
        public void PersistentBuildState_PreservesWorkItems()
        {
            // Arrange
            var original = TestDataFactory.CreatePersistentBuildState(workItemCount: 2);
            original.WorkItems[0].ItemName = "Special Sword";
            original.WorkItems[0].Requirements = new[] { 100, 200 };
            original.WorkItems[1].Status = WorkItemStatus.Failed;
            original.WorkItems[1].LastError = "Test error";

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.Equal(2, deserialized.WorkItems.Count);
            Assert.Equal("Special Sword", deserialized.WorkItems[0].ItemName);
            Assert.Equal(2, deserialized.WorkItems[0].Requirements.Length);
            Assert.Equal(WorkItemStatus.Failed, deserialized.WorkItems[1].Status);
            Assert.Equal("Test error", deserialized.WorkItems[1].LastError);
        }

        #endregion

        #region BuildHistoryEntry Serialization

        [Fact]
        public void BuildHistoryEntry_CanBeSerializedAndDeserialized()
        {
            // Arrange
            var original = TestDataFactory.CreateBuildHistoryEntry(
                status: BuildStatus.Cancelled,
                totalItems: 50,
                completedItems: 25);

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<BuildHistoryEntry>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.BuildId, deserialized.BuildId);
            Assert.Equal(original.SuitName, deserialized.SuitName);
            Assert.Equal(BuildStatus.Cancelled, deserialized.FinalStatus);
            Assert.Equal(50, deserialized.TotalItems);
            Assert.Equal(25, deserialized.CompletedItems);
        }

        [Fact]
        public void BuildHistoryEntry_HandleNullEndTime()
        {
            // Arrange
            var original = new BuildHistoryEntry
            {
                BuildId = "test-123",
                SuitName = "InProgress.alb",
                StartTime = DateTime.Now,
                EndTime = null,
                FinalStatus = BuildStatus.Active
            };

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<BuildHistoryEntry>(json);

            // Assert
            Assert.Null(deserialized.EndTime);
        }

        #endregion

        #region BuildEventLog Serialization

        [Fact]
        public void BuildEventLog_CanBeSerializedAndDeserialized()
        {
            // Arrange
            var original = new BuildEventLog
            {
                Timestamp = new DateTime(2024, 1, 15, 12, 0, 0),
                EventType = BuildEventType.WorkItemCompleted,
                Message = "Item delivered successfully",
                WorkItemId = 42,
                CharacterName = "TestChar",
                Details = "Delivered to PlayerX"
            };

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<BuildEventLog>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(BuildEventType.WorkItemCompleted, deserialized.EventType);
            Assert.Equal("Item delivered successfully", deserialized.Message);
            Assert.Equal(42, deserialized.WorkItemId);
            Assert.Equal("TestChar", deserialized.CharacterName);
            Assert.Equal("Delivered to PlayerX", deserialized.Details);
        }

        [Fact]
        public void BuildEventLog_HandlesNullOptionalFields()
        {
            // Arrange
            var original = new BuildEventLog
            {
                Timestamp = DateTime.Now,
                EventType = BuildEventType.BuildStarted,
                Message = "Build started",
                WorkItemId = null,
                CharacterName = null,
                Details = null
            };

            // Act
            var json = Serialize(original);
            var deserialized = Deserialize<BuildEventLog>(json);

            // Assert
            Assert.Null(deserialized.WorkItemId);
            Assert.Null(deserialized.CharacterName);
            Assert.Null(deserialized.Details);
        }

        #endregion

        #region Enums Serialization

        [Theory]
        [InlineData(BuildStatus.Active)]
        [InlineData(BuildStatus.Completed)]
        [InlineData(BuildStatus.Cancelled)]
        [InlineData(BuildStatus.Crashed)]
        [InlineData(BuildStatus.Resuming)]
        public void BuildStatus_AllValuesSerializeCorrectly(BuildStatus status)
        {
            // Arrange
            var state = new PersistentBuildState { Status = status };

            // Act
            var json = Serialize(state);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.Equal(status, deserialized.Status);
        }

        [Theory]
        [InlineData(WorkItemStatus.Pending)]
        [InlineData(WorkItemStatus.InProgress)]
        [InlineData(WorkItemStatus.Completed)]
        [InlineData(WorkItemStatus.Failed)]
        public void WorkItemStatus_AllValuesSerializeCorrectly(WorkItemStatus status)
        {
            // Arrange
            var item = new PersistentWorkItem { Status = status };

            // Act
            var json = Serialize(item);
            var deserialized = Deserialize<PersistentWorkItem>(json);

            // Assert
            Assert.Equal(status, deserialized.Status);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void EmptyWorkItemsList_SerializesCorrectly()
        {
            // Arrange
            var state = new PersistentBuildState();

            // Act
            var json = Serialize(state);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.NotNull(deserialized.WorkItems);
            Assert.Empty(deserialized.WorkItems);
        }

        [Fact]
        public void LargeWorkItemsList_SerializesCorrectly()
        {
            // Arrange
            var state = TestDataFactory.CreatePersistentBuildState(workItemCount: 100);

            // Act
            var json = Serialize(state);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.Equal(100, deserialized.WorkItems.Count);
        }

        [Fact]
        public void SpecialCharactersInStrings_SerializeCorrectly()
        {
            // Arrange
            var state = new PersistentBuildState
            {
                Name = "Test\"Suit'With<Special>&Characters.alb",
                DropCharacter = "Player\tWith\nNewlines"
            };

            // Act
            var json = Serialize(state);
            var deserialized = Deserialize<PersistentBuildState>(json);

            // Assert
            Assert.Equal(state.Name, deserialized.Name);
            Assert.Equal(state.DropCharacter, deserialized.DropCharacter);
        }

        #endregion

        #region Helper Methods

        private string Serialize<T>(T obj)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, obj);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        #endregion
    }
}
