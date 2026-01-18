using AlSuitBuilder.Server.Data;
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
                    var failedLines = new List<Tuple<int, string>>();

                    var fileLines = File.ReadAllLines(filename);

                    var id = 0;
                    for (int lineNum = 0; lineNum < fileLines.Length; lineNum++)
                    {
                        var line = fileLines[lineNum];

                        // Skip empty lines and comments
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith("//"))
                            continue;

                        bool parsed = false;
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
                                    parsed = true;
                                    break;
                                }
                            }
                        }

                        if (!parsed)
                        {
                            failedLines.Add(Tuple.Create(lineNum + 1, line.Length > 50 ? line.Substring(0, 50) + "..." : line));
                            Console.WriteLine($"Parse failure at line {lineNum + 1}: {line}");
                        }
                    }

                    // Report parse failures
                    if (failedLines.Any())
                    {
                        Console.WriteLine($"Warning: {failedLines.Count} line(s) could not be parsed:");
                        foreach (var failure in failedLines.Take(5))
                        {
                            Console.WriteLine($"  Line {failure.Item1}: {failure.Item2}");
                        }
                        if (failedLines.Count > 5)
                        {
                            Console.WriteLine($"  ... and {failedLines.Count - 5} more");
                        }
                    }

                    if (!workItems.Any())
                    {
                        var failureDetails = failedLines.Any()
                            ? $" Failed to parse {failedLines.Count} line(s). First failure at line {failedLines[0].Item1}."
                            : "";
                        responseMessage = $"Suit file was found but no valid items were found. Please make sure the format is correct.{failureDetails}";
                    }
                    else
                    {                       
                        foreach (var workItem in workItems)
                        {
                            Utils.WriteWorkItemToLog($"Parsed successfully from suit", workItem, true);
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

                        var itemsOnBuilderChar = workItems.Select(o => o.Character == clientInfo.CharacterName ? o : null).Where(o => o != null);
                        if (itemsOnBuilderChar.Count() > 0)
                        {
                            foreach (var itemOnChar in itemsOnBuilderChar)
                                Utils.WriteWorkItemToLog($"Excluding due to being on current character ({clientInfo.CharacterName})", itemOnChar, true);
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
                            var warningMsg = failedLines.Any()
                                ? $" Warning: {failedLines.Count} line(s) could not be parsed."
                                : "";
                            responseMessage = $"Starting Build [{_suitName}] Will attempt processing {workItems.Count} items.{warningMsg}";

                            Program.BuildInfo = new BuildInfo()
                            {
                                InitiatedId = _clientId,
                                DropCharacter = clientInfo.CharacterName,
                                StartTime = DateTime.Now,
                                Name = _suitName,
                                WorkItems = workItems
                            };
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
    }
}
