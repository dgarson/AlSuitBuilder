using AlSuitBuilder.Server.Data;
using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared.Messages.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Server.Parsers;

namespace AlSuitBuilder.Server.Actions
{
    internal class InitiateSuitAction : IServerAction
    {
        private int _clientId;
        private string _suitName;
        private List<IBuildFileParser> _parsers = new List<IBuildFileParser>() {
            new VGISuitParser(),
            new VGIGameParser(),
            new MagParser(),
        };

        public InitiateSuitAction(int clientId, string suitName)
        {
            _clientId = clientId;
            _suitName = suitName;
        }

        public void Execute()
        {

            var clientInfo = Program.GetClientInfo(_clientId);
            if (clientInfo == null)
                return;

            var success = false;
            var responseMessage = string.Empty;

            if (Program.BuildInfo != null)
            {
                responseMessage = "Build already in progress";
            }
            else
            {


                var filename = Path.Combine(Program.BuildDirectory, _suitName.Replace(".alb", "") + ".alb");

                if (!File.Exists(filename))
                {
                    responseMessage = "Suit not found " + filename;
                }
                else
                {


                    var workItems = new List<WorkItem>();

                    var fileLines = File.ReadAllLines(filename);

                    var id = 0;
                    foreach (var line in fileLines)
                    {
                        foreach (var parser in _parsers)
                        {
                            var parseMsg = string.Empty;

                            if (parser.IsValid(line, out parseMsg))
                            {
                                var workItem = parser.Process(line);
                                if (workItem != null)
                                {
                                    id++;
                                    workItem.Id = id;
                                    workItems.Add(workItem);
                                    break;
                                }
                                
                            }
                        }
                    }
                  

                    if (!workItems.Any())
                    {
                        responseMessage = "Suit file was found but no valid items were found. Please make sure the format is correct";
                    }
                    else
                    {
                        foreach (var workItem in workItems)
                        {
                            Utils.WriteWorkItemToLog($"Parsed successfully from suit", workItem, true);
                        }

                        // Detect duplicate items in the parsed list
                        var duplicates = DetectDuplicates(workItems);
                        var duplicateCount = duplicates.Count;

                        if (duplicates.Any())
                        {
                            Console.WriteLine($"[DUPLICATES] Detected {duplicateCount} duplicate item(s) in suit file:");
                            foreach (var dup in duplicates)
                            {
                                Utils.WriteWorkItemToLog($"DUPLICATE DETECTED - removing duplicate entry", dup, true);
                                Console.WriteLine($"  - {dup.ItemName} on {dup.Character} (Material: {dup.MaterialId}, Set: {dup.SetId})");
                                workItems.Remove(dup);
                            }
                        }

                        var characters = new List<string>();
                        foreach (var clientId in Program.GetClientIds())
                        {
                            var client = Program.GetClientInfo(clientId);
                            if (client == null)
                                continue;

                            characters.Add(client.CharacterName);
                            if (client.OtherCharacters != null && client.OtherCharacters.Any())
                            characters.AddRange(client.OtherCharacters);
                        }

                        // Track items already on dropoff character for user feedback
                        var itemsOnDropoffChar = workItems.Where(o => o.Character == clientInfo.CharacterName).ToList();
                        var dropoffSkippedCount = itemsOnDropoffChar.Count;

                        if (dropoffSkippedCount > 0)
                        {
                            Console.WriteLine($"[DROPOFF] {dropoffSkippedCount} item(s) already on dropoff character '{clientInfo.CharacterName}':");
                            foreach (var itemOnChar in itemsOnDropoffChar)
                            {
                                Utils.WriteWorkItemToLog($"SKIPPED - Item already on dropoff character ({clientInfo.CharacterName})", itemOnChar, true);
                                Console.WriteLine($"  - {itemOnChar.ItemName} (Material: {itemOnChar.MaterialId}, Set: {itemOnChar.SetId})");
                            }
                        }

                        workItems.RemoveAll(o => o.Character == clientInfo.CharacterName);
                        var missing = workItems.Select(o => o.Character).Except(characters);

                        if (missing.Any())
                        {
                            responseMessage = $"No client(s) running for {string.Join(",", missing)}";
                        }
                        else
                        {
                            success = true;

                            // Build detailed response message with progress info
                            var totalParsed = workItems.Count + dropoffSkippedCount + duplicateCount;
                            var msgParts = new List<string>();
                            msgParts.Add($"Starting Build [{_suitName}]");
                            msgParts.Add($"Processing {workItems.Count} item(s)");

                            if (dropoffSkippedCount > 0)
                                msgParts.Add($"{dropoffSkippedCount} already on dropoff char");
                            if (duplicateCount > 0)
                                msgParts.Add($"{duplicateCount} duplicate(s) removed");

                            responseMessage = string.Join(" | ", msgParts);

                            var buildId = Guid.NewGuid().ToString();

                            Program.BuildInfo = new BuildInfo()
                            {
                                BuildId = buildId,
                                InitiatedId = _clientId,
                                DropCharacter = clientInfo.CharacterName,
                                StartTime = DateTime.Now,
                                Name = _suitName,
                                WorkItems = workItems
                            };

                            // Initialize persistence for this build
                            if (Program.PersistenceManager != null)
                            {
                                try
                                {
                                    var persistentState = BuildPersistenceManager.FromBuildInfo(Program.BuildInfo, filename);
                                    Program.PersistenceManager.SaveActiveState(persistentState);
                                    Program.PersistenceManager.StartBuildLog(buildId);
                                    Program.PersistenceManager.LogEvent(new BuildEventLog
                                    {
                                        Timestamp = DateTime.Now,
                                        EventType = BuildEventType.BuildStarted,
                                        Message = $"Build started: {_suitName} with {workItems.Count} items",
                                        CharacterName = clientInfo.CharacterName
                                    });

                                    // Log duplicate items that were removed
                                    foreach (var dup in duplicates)
                                    {
                                        Program.PersistenceManager.LogEvent(new BuildEventLog
                                        {
                                            Timestamp = DateTime.Now,
                                            EventType = BuildEventType.DuplicateItemDetected,
                                            Message = $"Duplicate item removed: {dup.ItemName}",
                                            WorkItemId = dup.Id,
                                            CharacterName = dup.Character,
                                            Details = $"Material: {dup.MaterialId}, Set: {dup.SetId}"
                                        });
                                    }

                                    // Log items skipped because they're on the dropoff character
                                    foreach (var skipped in itemsOnDropoffChar)
                                    {
                                        Program.PersistenceManager.LogEvent(new BuildEventLog
                                        {
                                            Timestamp = DateTime.Now,
                                            EventType = BuildEventType.WorkItemSkippedOnDropoff,
                                            Message = $"Item skipped (already on dropoff char): {skipped.ItemName}",
                                            WorkItemId = skipped.Id,
                                            CharacterName = clientInfo.CharacterName,
                                            Details = $"Material: {skipped.MaterialId}, Set: {skipped.SetId}"
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Utils.LogException(ex);
                                    // Don't fail the build if persistence fails
                                }
                            }
                        }

                    }
                }
            }

            Program.SendMessageToClient(_clientId, new InitiateBuildResponseMessage()
            {
                Accepted = success,
                Message = responseMessage
            });

        }

        /// <summary>
        /// Detects duplicate work items in the list. Two items are considered duplicates if they have
        /// the same character, item name, material ID, set ID, and requirements (spells).
        /// Returns the duplicate entries (keeping the first occurrence of each unique item).
        /// </summary>
        private List<WorkItem> DetectDuplicates(List<WorkItem> workItems)
        {
            var duplicates = new List<WorkItem>();
            var seen = new HashSet<string>();

            foreach (var item in workItems)
            {
                // Create a unique key based on the item's identifying properties
                var requirementsKey = item.Requirements != null
                    ? string.Join(",", item.Requirements.OrderBy(r => r))
                    : "";
                var key = $"{item.Character}|{item.ItemName}|{item.MaterialId}|{item.SetId}|{requirementsKey}";

                if (seen.Contains(key))
                {
                    duplicates.Add(item);
                }
                else
                {
                    seen.Add(key);
                }
            }

            return duplicates;
        }
    }
}
