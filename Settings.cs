using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.Renamer
{
    public class Settings : IPluginSettings
    {
        public string Prefix { get; set; } = "";
        public bool ApplyPrefix { get; set; } = true;
    }
}