using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Serialization;
using SmartInbox.Emby.Configuration;
using System;
using System.IO;

namespace SmartInbox.Emby
{
    public class Plugin : BasePlugin<PluginConfiguration>, /*IHasWebPages,*/ IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            // Instance = this;
        }

        public override string Name => "Smart Inbox for Emby";

        /*
        public IEnumerable<PluginPageInfo> GetPages()
        {
            throw new System.NotImplementedException();
        }
        */

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Images.plugin.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }

        private Guid _id = new Guid("eae79bb3-d25e-4513-8ce4-31339cf74b6e");

        public override Guid Id
        {
            get { return _id; }
        }
    }
}
