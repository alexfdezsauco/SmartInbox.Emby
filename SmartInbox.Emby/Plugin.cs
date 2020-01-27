namespace SmartInbox.Emby
{
    using System;
    using System.IO;

    using MediaBrowser.Common.Configuration;
    using MediaBrowser.Common.Plugins;
    using MediaBrowser.Model.Drawing;
    using MediaBrowser.Model.Serialization;

    using SmartInbox.Emby.Configuration;

    public class Plugin : BasePlugin<PluginConfiguration>, /*IHasWebPages,*/ IHasThumbImage
    {
        private readonly Guid _id = new Guid("eae79bb3-d25e-4513-8ce4-31339cf74b6e");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public override Guid Id => this._id;

        public override string Name => "Smart Inbox for Emby";

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        /*
        public IEnumerable<PluginPageInfo> GetPages()
        {
            throw new System.NotImplementedException();
        }
        */
        public Stream GetThumbImage()
        {
            var type = this.GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Images.plugin.png");
        }
    }
}