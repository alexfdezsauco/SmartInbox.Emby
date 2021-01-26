namespace TVHeadEnd
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Controller.Channels;
    using MediaBrowser.Controller.Entities;
    using MediaBrowser.Controller.Entities.Movies;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Controller.Providers;
    using MediaBrowser.Model.Channels;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.MediaInfo;
    using MediaBrowser.Model.Tasks;

    using SmartInbox.Emby.ScheduledTasks;

    using SQLite.Net;
    using SQLite.Net.Platform.Generic;

    public class SmartInboxChannel : IChannel, IHasCacheKey, IHasChangeEvent
    {
        private readonly ILibraryManager _libraryManager;

        private readonly ITaskManager _taskManager;

        private readonly ILogger _logger;

        private Timer _updateTimer;

        public SmartInboxChannel(ILibraryManager libraryManager, ITaskManager taskManager, ILogManager logManager)
        {
            this._libraryManager = libraryManager;
            this._taskManager = taskManager;
            this._logger = logManager.GetLogger(this.GetType().Name);

            // var interval = TimeSpan.FromMinutes(2);
            // this._updateTimer = new Timer(this.OnUpdateTimerCallback, null, interval, interval);

            this._taskManager.TaskCompleted += this.OnTaskManagerTaskCompleted;
        }

        private void OnTaskManagerTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            if (e.Result.Key == Keys.Recommendations)
            {
                this.OnContentChanged();
            }
        }

        public event EventHandler ContentChanged;

        public string DataVersion => "12";

        public string Description => "Smart Inbox Recommendations";

        public string Name => "Smart Inbox";

        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;


        public string GetCacheKey(string userId)
        {
            return Guid.NewGuid().ToString("N");
        }

        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
                       {
                           ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.Movie },
                           MediaTypes = new List<ChannelMediaType> { ChannelMediaType.Video },
                           SupportsContentDownloading = true,
                           SupportsSortOrderToggle = true
                       };
        }

        public async Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            return new DynamicImageResponse { HasImage = false };
        }

        public async Task<ChannelItemResult> GetChannelItems(
            InternalChannelItemQuery query,
            CancellationToken cancellationToken)
        {
            this._logger.Info("GetChannelItems");

            var result = new ChannelItemResult { Items = new List<ChannelItemInfo>() };

            try
            {
                var databaseFileName = "/config/data/smart-inbox-recommendations.db";
                if (File.Exists(databaseFileName))
                {
                    var baseItem = this._libraryManager.GetUserRootFolder().GetRecursiveChildren()[1];
                    var internalItemsQuery = new InternalItemsQuery { MediaTypes = new[] { MediaType.Video } };

                    var moviesEnumeration = this._libraryManager.GetItemList(internalItemsQuery, new[] { baseItem }).OfType<Movie>();
                    Dictionary<string, Movie> movies = new Dictionary<string, Movie>();
                    foreach (var movie in moviesEnumeration)
                    {
                        var key = string.Empty;
                        foreach (var currentItemProviderId in movie.ProviderIds.OrderBy(pair => pair.Key))
                        {
                            key += currentItemProviderId.Key + "=" + currentItemProviderId.Value + "|";
                        }

                        key = key.TrimEnd('|');

                        if (!string.IsNullOrEmpty(key) && !movies.TryGetValue(key, out _))
                        {
                            movies[key] = movie;
                        }
                    }

                    var sqLiteConnection = new SQLiteConnection(new SQLitePlatformGeneric(), databaseFileName);
                    var sqLiteCommand = sqLiteConnection.CreateCommand(@"SELECT * FROM [Recommendations] WHERE [Recommendation] = 1");
                    var sqLiteCommandResult = sqLiteCommand.ExecuteDeferredQuery();

                    foreach (var dataTableRow in sqLiteCommandResult.Data)
                    {
                        var id = (string)dataTableRow["Id"];
                        this._logger.Info($"Processing recommendation {id}");
                        if (movies.TryGetValue(id, out var currentItem))
                        {
                            this._logger.Info($"Found recommendation {id}");
                            var channelItemInfo = new ChannelItemInfo
                                                      {
                                                          Id = currentItem.Id.ToString(),
                                                          ProviderIds = currentItem.ProviderIds,
                                                          Type = ChannelItemType.Media,
                                                          MediaSources =
                                                              new List<MediaSourceInfo>
                                                                  {
                                                                      new MediaSourceInfo
                                                                          {
                                                                              Path = currentItem.Path,
                                                                              Protocol = MediaProtocol.File
                                                                          }
                                                                  },
                                                          ContentType = ChannelMediaContentType.Movie,
                                                          MediaType = ChannelMediaType.Video,
                                                          Name = currentItem.Name,
                                                          ImageUrl = currentItem.PrimaryImagePath,
                                                          OriginalTitle = currentItem.OriginalTitle
                                                      };
                            result.Items.Add(channelItemInfo);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this._logger.ErrorException("Error updating channel content", e);
            }

            return result;
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType> { ImageType.Primary, ImageType.Thumb };
        }

        public bool IsEnabledFor(string userId)
        {
            return true;
        }

        private void OnUpdateTimerCallback(object state)
        {
            this.OnContentChanged();
        }

        protected virtual void OnContentChanged()
        {
            this.ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}