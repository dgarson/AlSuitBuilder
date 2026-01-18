# Persistence/Resume Implementation Plan for AlSuitBuilder

## Problem Statement

Currently, if the server crashes or restarts:
1. Active builds are lost
2. No way to resume a partial build
3. No build history or logging to file
4. All state is RAM-only in `Program.BuildInfo`

---

## Solution Architecture

### New File Structure

```
AlSuitBuilder.Server/
├── Persistence/
│   ├── BuildPersistenceManager.cs      # Core persistence orchestration
│   ├── PersistentBuildState.cs         # Serializable wrapper for BuildInfo
│   ├── BuildHistoryEntry.cs            # Individual history record
│   ├── BuildEventLog.cs                # Event logging structure
│   └── WorkItemState.cs                # Extended WorkItem state for persistence
├── Actions/
│   ├── ResumeBuildAction.cs            # New action to handle resume
│   ├── SaveBuildStateAction.cs         # Action to persist current state
│   └── AbandonBuildAction.cs           # Action to discard crashed build
└── Data/
    └── builds/                         # Runtime directory for persistence files
        ├── active_build.json           # Current build state
        ├── build_history.json          # Build history log
        └── logs/
            └── build_YYYYMMDD_HHMMSS.log  # Per-build event logs

AlSuiteBuilder.Shared/
└── Messages/
    ├── Client/
    │   ├── ResumeBuildMessage.cs       # Client request to resume
    │   └── BuildStatusMessage.cs       # Client request for status
    └── Server/
        ├── ResumeBuildResponseMessage.cs  # Server response to resume
        └── BuildStatusResponseMessage.cs  # Server status response
```

---

## Data Models

### PersistentBuildState

```csharp
[DataContract]
public class PersistentBuildState
{
    [DataMember] public int Version { get; set; } = 1;
    [DataMember] public string BuildId { get; set; }           // GUID for unique identification
    [DataMember] public string Name { get; set; }              // Original suit file name
    [DataMember] public string DropCharacter { get; set; }
    [DataMember] public string RelayCharacter { get; set; }
    [DataMember] public int InitiatedId { get; set; }
    [DataMember] public DateTime StartTime { get; set; }
    [DataMember] public DateTime? EndTime { get; set; }
    [DataMember] public DateTime LastSaveTime { get; set; }
    [DataMember] public BuildStatus Status { get; set; }
    [DataMember] public List<PersistentWorkItem> WorkItems { get; set; }
    [DataMember] public List<int> CompletedWorkItemIds { get; set; }
    [DataMember] public int TotalItemCount { get; set; }       // For progress tracking
    [DataMember] public string OriginalFilePath { get; set; }  // For re-parsing if needed
}

public enum BuildStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2,
    Crashed = 3,      // Set on recovery detection
    Resuming = 4
}
```

### PersistentWorkItem

```csharp
[DataContract]
public class PersistentWorkItem
{
    [DataMember] public int Id { get; set; }
    [DataMember] public string Character { get; set; }
    [DataMember] public string ItemName { get; set; }
    [DataMember] public int[] Requirements { get; set; }
    [DataMember] public int MaterialId { get; set; }
    [DataMember] public int SetId { get; set; }
    [DataMember] public int Burden { get; set; }
    [DataMember] public int Value { get; set; }
    [DataMember] public DateTime LastAttempt { get; set; }
    [DataMember] public WorkItemStatus Status { get; set; }
    [DataMember] public int AttemptCount { get; set; }
    [DataMember] public string LastError { get; set; }
}

public enum WorkItemStatus
{
    Pending = 0,
    InProgress = 1,     // Was being processed when crash occurred
    Completed = 2,
    Failed = 3
}
```

### BuildHistoryEntry

```csharp
[DataContract]
public class BuildHistoryEntry
{
    [DataMember] public string BuildId { get; set; }
    [DataMember] public string SuitName { get; set; }
    [DataMember] public string DropCharacter { get; set; }
    [DataMember] public DateTime StartTime { get; set; }
    [DataMember] public DateTime? EndTime { get; set; }
    [DataMember] public BuildStatus FinalStatus { get; set; }
    [DataMember] public int TotalItems { get; set; }
    [DataMember] public int CompletedItems { get; set; }
    [DataMember] public int FailedItems { get; set; }
    [DataMember] public bool WasResumed { get; set; }
    [DataMember] public string LogFilePath { get; set; }
}
```

### BuildEventLog

```csharp
[DataContract]
public class BuildEventLog
{
    [DataMember] public DateTime Timestamp { get; set; }
    [DataMember] public BuildEventType EventType { get; set; }
    [DataMember] public string Message { get; set; }
    [DataMember] public int? WorkItemId { get; set; }
    [DataMember] public string CharacterName { get; set; }
    [DataMember] public string Details { get; set; }
}

public enum BuildEventType
{
    BuildStarted,
    BuildResumed,
    BuildCompleted,
    BuildCancelled,
    BuildCrashDetected,
    WorkItemAssigned,
    WorkItemCompleted,
    WorkItemFailed,
    WorkItemRetry,
    ClientConnected,
    ClientDisconnected,
    CharacterSwitch,
    Error
}
```

---

## Core Implementation: BuildPersistenceManager

The `BuildPersistenceManager` class handles all persistence operations:

### Key Methods

| Method | Purpose |
|--------|---------|
| `SaveActiveState(state)` | Atomically save current build state to JSON |
| `LoadActiveState()` | Load persisted state (returns null if none) |
| `ClearActiveState()` | Delete active state file |
| `HasActiveState()` | Check if persisted state exists |
| `AddHistoryEntry(entry)` | Append to build history (keeps last 100) |
| `LoadHistory()` | Get all history entries |
| `StartBuildLog(buildId)` | Create new log file for build |
| `LogEvent(event)` | Write event to current log |
| `CloseCurrentLog()` | Finalize and close log file |

### Serialization Strategy

- **JSON** via `DataContractJsonSerializer` (built into .NET 4.7.2)
- **Atomic writes**: Write to temp file, then rename
- **Human-readable**: Allows manual inspection and debugging
- **Version field**: Enables future schema migrations

---

## Integration Points

### 1. Program.cs Modifications

**Add persistence manager initialization:**
```csharp
public static BuildPersistenceManager PersistenceManager;

// In Main(), after loading SpellData:
PersistenceManager = new BuildPersistenceManager(BuildDirectory);
CheckForCrashedBuild();
```

**Add crash detection on startup:**
```csharp
private static void CheckForCrashedBuild()
{
    if (PersistenceManager.HasActiveState())
    {
        var state = PersistenceManager.LoadActiveState();
        if (state != null && state.Status == BuildStatus.Active)
        {
            state.Status = BuildStatus.Crashed;
            PersistenceManager.SaveActiveState(state);
            Console.WriteLine($"[RECOVERY] Detected crashed build: {state.Name}");
            Console.WriteLine($"[RECOVERY] Use /alb resume to continue or /alb abandon to discard");
        }
    }
}
```

**Add message handler for resume:**
```csharp
nc.AddMessageHandler<ResumeBuildMessage>(ResumeBuildMessageHandler);
```

**Modify WorkResultMessageHandler to persist state:**
```csharp
// After removing work item:
_actionQueue.Enqueue(new SaveBuildStateAction(work.Id, message.Success));
```

### 2. InitiateSuitAction.cs Modifications

**After creating BuildInfo:**
```csharp
Program.BuildInfo = new BuildInfo()
{
    BuildId = Guid.NewGuid().ToString(),  // NEW
    // ... existing fields ...
};

// Initialize persistence
var persistentState = BuildPersistenceManager.FromBuildInfo(Program.BuildInfo, filename);
Program.PersistenceManager.SaveActiveState(persistentState);
Program.PersistenceManager.StartBuildLog(persistentState.BuildId);
Program.PersistenceManager.LogEvent(new BuildEventLog
{
    Timestamp = DateTime.Now,
    EventType = BuildEventType.BuildStarted,
    Message = $"Build started: {_suitName} with {workItems.Count} items"
});
```

### 3. BuildInfo.cs Modifications

**Add BuildId property:**
```csharp
public string BuildId { get; set; }
```

**In completion handling:**
```csharp
if (WorkItems.Count == 0)
{
    // Log completion
    Program.PersistenceManager?.LogEvent(new BuildEventLog { ... });

    // Add history entry
    Program.PersistenceManager?.AddHistoryEntry(new BuildHistoryEntry { ... });

    // Clean up
    Program.PersistenceManager?.ClearActiveState();
    Program.PersistenceManager?.CloseCurrentLog();
}
```

### 4. TerminateSuitAction.cs Modifications

**Log cancellation and update history:**
```csharp
Program.PersistenceManager?.LogEvent(new BuildEventLog
{
    EventType = BuildEventType.BuildCancelled,
    Message = "Build cancelled by user"
});

Program.PersistenceManager?.AddHistoryEntry(new BuildHistoryEntry
{
    FinalStatus = BuildStatus.Cancelled,
    // ... other fields ...
});

Program.PersistenceManager?.ClearActiveState();
Program.PersistenceManager?.CloseCurrentLog();
```

---

## New Commands

### Client-Side Commands (SuitBuilderPlugin.cs)

| Command | Description |
|---------|-------------|
| `/alb resume` | Resume a crashed/interrupted build |
| `/alb status` | Show current build progress |
| `/alb history` | Show recent build history |
| `/alb abandon` | Discard a crashed build without resuming |

---

## Recovery Flows

### Crash Detection Flow

```
Server Startup
     │
     ▼
Check for active_build.json
     │
     ├── Does not exist ──► Normal startup
     │
     └── Exists
           │
           ▼
     Check Status field
           │
           ├── Status == Active ──► Mark as Crashed, notify user
           │
           ├── Status == Crashed ──► Already marked, notify user
           │
           └── Status == Completed/Cancelled ──► Clear stale file
```

### Resume Flow

```
/alb resume command received
     │
     ▼
Load active_build.json
     │
     ├── Does not exist ──► "No build to resume"
     │
     └── Exists
           │
           ▼
     Check connected clients
           │
           ├── Missing required characters ──► "Missing clients for X, Y"
           │
           └── All characters available
                  │
                  ▼
           Reset in-flight items to Pending
                  │
                  ▼
           Create BuildInfo from PersistentBuildState
                  │
                  ▼
           Update state to Active, save
                  │
                  ▼
           Resume build loop (existing Tick logic)
```

### In-Flight Work Item Recovery

When server crashes while a work item is being processed:

1. **On Recovery**: Items with `Status == InProgress` are detected
2. **On Resume**: These items are reset to `Pending` with `LastAttempt = DateTime.MinValue`
3. **Retry Logic**: `AttemptCount` is incremented to track retry attempts
4. **Max Retries**: After 3 attempts, mark as `Failed` and skip

---

## Error Handling

| Scenario | Handling |
|----------|----------|
| Crash during save | Atomic file write (temp + rename) prevents corruption |
| Duplicate work item delivery | Client already has retry logic; server tracks CompletedWorkItemIds |
| Client reconnects mid-build | Existing ReadyForWorkMessage handler resets LastAttempt |
| Original .alb file deleted | Use persisted items; don't re-parse |
| State file corrupted | Try-catch with fallback to "cannot resume" message |

---

## Implementation Phases

### Phase 1: Core Infrastructure
1. Create `Persistence/` directory structure
2. Implement `PersistentBuildState.cs`, `PersistentWorkItem.cs`
3. Implement `BuildPersistenceManager.cs` with save/load/convert methods
4. Add basic JSON serialization tests

### Phase 2: State Persistence
1. Modify `Program.cs` to initialize `PersistenceManager`
2. Modify `InitiateSuitAction.cs` to save initial state
3. Modify `Program.WorkResultMessageHandler` to trigger state saves
4. Add `SaveBuildStateAction.cs`
5. Test: Start build, verify `active_build.json` created and updated

### Phase 3: Resume Functionality
1. Add `ResumeBuildMessage.cs` and `ResumeBuildResponseMessage.cs`
2. Implement `ResumeBuildAction.cs`
3. Modify `Program.cs` startup to detect crashed builds
4. Add `/alb resume` command to plugin
5. Test: Kill server mid-build, restart, resume

### Phase 4: History & Logging
1. Implement `BuildHistoryEntry.cs` and `BuildEventLog.cs`
2. Add logging calls throughout build lifecycle
3. Modify `TerminateSuitAction.cs` for cancellation logging
4. Add `/alb status` and `/alb history` commands
5. Test: Complete build, verify history entry and log file

### Phase 5: Polish & Edge Cases
1. Add max retry handling for failed items
2. Implement `/alb abandon` command to discard crashed build
3. Add console output for recovery status
4. Documentation and code cleanup

---

## Testing Checklist

| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Clean build persistence | Start build, check files | `active_build.json` created |
| State updates on completion | Complete 2 items, check file | Items marked completed |
| Crash detection | Kill process, restart | "Detected crashed build" message |
| Resume success | Resume with all clients | Build continues from last state |
| Resume missing clients | Resume with missing client | Error with missing character names |
| Build completion cleanup | Complete build | `active_build.json` deleted, history entry added |
| Cancellation | Cancel mid-build | State cleared, history shows cancelled |
| History persistence | Complete 3 builds | History shows all 3 entries |
| Log file creation | Start and complete build | Log file created in logs/ directory |

---

## Files to Modify

| File | Changes |
|------|---------|
| `AlSuitBuilder.Server/Program.cs` | Add PersistenceManager, startup recovery, message handlers |
| `AlSuitBuilder.Server/BuildInfo.cs` | Add BuildId property, persistence triggers |
| `AlSuitBuilder.Server/Actions/InitiateSuitAction.cs` | Save initial state on build start |
| `AlSuitBuilder.Server/Actions/TerminateSuitAction.cs` | Log cancellation, update history |
| `AlSuitBuilder.Plugin/SuitBuilderPlugin.cs` | Add resume/status/history/abandon commands |

---

## New Files to Create

| File | Purpose |
|------|---------|
| `AlSuitBuilder.Server/Persistence/BuildPersistenceManager.cs` | Core persistence logic |
| `AlSuitBuilder.Server/Persistence/PersistentBuildState.cs` | Serializable build state |
| `AlSuitBuilder.Server/Persistence/PersistentWorkItem.cs` | Serializable work item |
| `AlSuitBuilder.Server/Persistence/BuildHistoryEntry.cs` | History record model |
| `AlSuitBuilder.Server/Persistence/BuildEventLog.cs` | Event log model |
| `AlSuitBuilder.Server/Actions/ResumeBuildAction.cs` | Resume command handler |
| `AlSuitBuilder.Server/Actions/SaveBuildStateAction.cs` | State save action |
| `AlSuitBuilder.Server/Actions/AbandonBuildAction.cs` | Abandon command handler |
| `AlSuiteBuilder.Shared/Messages/Client/ResumeBuildMessage.cs` | Resume request message |
| `AlSuiteBuilder.Shared/Messages/Server/ResumeBuildResponseMessage.cs` | Resume response message |

---

## Backwards Compatibility

- **Version field**: `PersistentBuildState.Version` enables future schema migrations
- **No data migration needed**: This is a new feature
- **Existing builds**: Not affected (no `active_build.json` exists initially)
- **Rollback path**: Delete `builds/active_build.json`, revert code, restart
