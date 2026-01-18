using AlSuitBuilder.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AlSuitBuilder.Server.Parsers
{
    public abstract class RegexParser : IBuildFileParser
    {

        protected List<Regex> SupportedRegex;


        public RegexParser(List<Regex> supportedRegex)
        {
            SupportedRegex = supportedRegex;
        }

        public virtual bool IsValid(string line, out string msg)
        {
            msg = String.Empty;

            if (string.IsNullOrWhiteSpace(line))
                return false;

            return SupportedRegex.Any(x => x.IsMatch(line));

        }

        public abstract WorkItem Process(string line);
       

        public virtual void ProcessRequirements(WorkItem workItem, string requirements)
        {
            workItem.Requirements = requirements.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Program.SpellData.SpellIdByName(x.Trim())).Where(p => p != -1).ToArray();

            // Fix 3.3: Only strip material prefix if it's followed by a space (complete word match)
            // e.g., "Gold Ring" matches "Gold" + " Ring", but "Golden Ring" should not match "Gold"
            foreach (var info in Shared.Dictionaries.MaterialInfo)
            {
                var materialPrefix = info.Value + " ";
                if (workItem.ItemName.StartsWith(materialPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    workItem.MaterialId = info.Key;
                    workItem.ItemName = workItem.ItemName.Substring(materialPrefix.Length);
                    break;
                }
            }

            // Fix 3.4: Match set names as complete phrases, not substrings
            // Sort by length descending to match longer set names first (prevents partial matches)
            var sortedSetInfo = Shared.Dictionaries.SetInfo.OrderByDescending(s => s.Value.Length);
            foreach (var info in sortedSetInfo)
            {
                // Look for the set name as a complete phrase (case-insensitive)
                // Check that it's bounded by commas, start/end of string, or " Set" suffix
                var setName = info.Value;
                var searchPatterns = new[]
                {
                    setName + " Set",           // "Weave of Alchemy Set"
                    setName,                     // Just the set name
                };

                foreach (var pattern in searchPatterns)
                {
                    var index = requirements.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        // Verify it's a complete match (not part of a larger word)
                        var isStartBounded = index == 0 || requirements[index - 1] == ',' || requirements[index - 1] == ' ';
                        var endIndex = index + pattern.Length;
                        var isEndBounded = endIndex >= requirements.Length ||
                                          requirements[endIndex] == ',' ||
                                          requirements[endIndex] == ' ' ||
                                          (endIndex + 4 <= requirements.Length && requirements.Substring(endIndex, 4).Equals(" Set", StringComparison.OrdinalIgnoreCase));

                        if (isStartBounded && isEndBounded)
                        {
                            workItem.SetId = info.Key;
                            return; // Found a match, exit
                        }
                    }
                }
            }
        }
    }
}
