using AlSuitBuilder.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AlSuitBuilder.Server.Parsers
{
    internal class MagParser : RegexParser
    {
        // Extended character classes to support numbers, underscores, and special characters in names
        public MagParser() : base(new List<Regex>() {
            new Regex(@"(?<character>[A-Za-z0-9\-'_ ]+), (?<item>[A-Za-z0-9 ']+,) ?(?<set>[A-Za-z0-9' ]* Set, ?)?(AL (?<armorlevel>[0-9]*), ?)?(?<cantrips>[A-Za-z0-9 ,]+,) (Wield Lvl (?<wieldreq>[0-9]*),)? ?([A-Za-z0-9 ]+ to Activate, ?)?(Diff (?<diff>[0-9]+), ?)?(Craft (?<craft>[0-9]+), ?)?(Value (?<value>[0-9,]+),)? ?(BU (?<burden>[0-9]+),?)? ?\[?(?<rating>[A-Z0-9]+)?\]?", RegexOptions.Compiled)
        })
        {
        }

        public override WorkItem Process(string line)
        {
            var exp = SupportedRegex.FirstOrDefault(r => r.IsMatch(line));
            if (exp == null) return null;

            var groups = exp.Match(line).Groups;

            if (groups.Count == 0)
                return null;

            var workItem = new WorkItem();

            workItem.ItemName = groups["item"].Value.Replace(",","").Trim();
            workItem.Character = groups["character"].Value.Trim();


            workItem.Value = 0;


            var requirements = groups["cantrips"].Value;

            var setName = groups["set"].Value;
            if (!string.IsNullOrWhiteSpace(setName))
            {
                requirements += "," + setName + " Set";
            }

            ProcessRequirements(workItem, requirements);

            return workItem;
        }
    }
}
