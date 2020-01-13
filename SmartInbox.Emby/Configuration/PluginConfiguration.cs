using MediaBrowser.Model.Plugins;
using System;

namespace SmartInbox.Emby.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public TimeSpan TimeToConsiderAMovieAsUnwatched { get; set; }

        public TimeSpan TimeToConsiderAMovieAsNew { get; set; }
    }
}