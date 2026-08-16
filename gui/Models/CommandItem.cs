using System;

namespace gui.Models
{
    public class CommandItem
    {
        public string Name { get; set; }
        public string Level { get; set; }

        private static readonly string[] _options = new string[] { "Regular", "Moderator", "Administrator", "Host", "Disabled" };

        public string[] Options { get { return _options; } }
    }
}
