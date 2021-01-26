namespace SmartInbox.Emby.ScheduledTasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Common.Configuration;
    using MediaBrowser.Controller.Configuration;
    using MediaBrowser.Controller.Entities;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Controller.MediaEncoding;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.Tasks;

    using SQLite.Net;
    using SQLite.Net.Platform.Generic;

    //public class BackupScheduledTask2 : IScheduledTask
    //{
    //    private readonly IApplicationPaths _appPaths;

    //    private readonly IFileSystem _fileSystem;

    //    private readonly ILibraryManager _libraryManager;

    //    private readonly ILibraryMonitor _libraryMonitor;

    //    private readonly IUserDataManager _dataManager;

    //    private readonly Plugin _plugin;

    //    private readonly IServerConfigurationManager configurationManager;

    //    private readonly ILogger _logger;

    //    private readonly IMediaEncoder _mediaEncoder;

    //    private readonly IJsonSerializer _jsonSerializer;

    //    private IUserManager _userManager;

    //    public BackupScheduledTask2(
    //        ILibraryManager libraryManager,
    //        ILogger logger,
    //        IMediaEncoder mediaEncoder,
    //        IJsonSerializer jsonSerializer,
    //        IFileSystem fileSystem,
    //        IApplicationPaths appPaths,
    //        ILibraryMonitor libraryMonitor,
    //        IUserManager userManager
    //        )
    //    {
    //        this._libraryManager = libraryManager;
    //        this._logger = logger;
    //        this._mediaEncoder = mediaEncoder;
    //        this._jsonSerializer = jsonSerializer;
    //        this._fileSystem = fileSystem;
    //        this._appPaths = appPaths;
    //        this._libraryMonitor = libraryMonitor;
    //        this._userManager = userManager;
    //    }

    //    public string Category => "Smart.Emby";

    //    public string Description => "Backup Data";

    //    public string Key => "Backup Data";

    //    public string Name => "Backup Data";

    //    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    //    {
    //        var databaseFileName = "/config/data/smart-inbox.db";
    //        this._logger.Info("Creating Database '{0}'...", databaseFileName);
    //        var sqLiteConnection = new SQLiteConnection(new SQLitePlatformGeneric(), databaseFileName);
    //        var sqLiteCommand = sqLiteConnection.CreateCommand(
    //            @"CREATE TABLE IF NOT EXISTS [Movies] (
    //                        [Id] TEXT NOT NULL PRIMARY KEY,
    //                        [Name] TEXT NOT NULL,
    //                        [CommunityRating] FLOAT NULL,
    //                        [IsPlayed] BIT NOT NULL,
    //                        [IsDeleted] BIT NOT NULL,
    //                        [DateCreated] DATE NOT NULL,
    //                        [DateSynched] DATE NOT NULL
    //                    )");
    //        sqLiteCommand.ExecuteNonQuery();

    //        var baseItem = this._libraryManager.GetUserRootFolder().Children[1];
    //        var query = new InternalItemsQuery { MediaTypes = new[] { MediaType.Video } };

    //        var items = this._libraryManager.GetItemList(query, new[] { baseItem }).OfType<Video>().ToList();

    //        this._logger.Info("Updating table '{0}' ...", "Movies");
    //        var regex = new Regex("(\\s+|-)", RegexOptions.Compiled);
    //        var genres = new SortedDictionary<string, string>();
    //        foreach (var currentItem in items)
    //        {
    //            foreach (var genre in currentItem.Genres)
    //            {
    //                if (!genres.ContainsKey(genre))
    //                {
    //                    var fieldName = "Is" + regex.Replace(genre, string.Empty);
    //                    genres[genre] = fieldName;
    //                }
    //            }
    //        }

    //        foreach (var genre in genres)
    //        {
    //            var fieldName = genre.Value;
    //            var liteCommand = sqLiteConnection.CreateCommand(
    //                $@"ALTER TABLE [Movies]
    //                ADD [{fieldName}] BIT NOT NULL default 0");
    //            try
    //            {
    //                liteCommand.ExecuteNonQuery();
    //            }
    //            catch (SQLiteException e)
    //            {
    //                _logger.ErrorException("Error altering [Movies] table", e);
    //            }
    //        }

    //        var datedSynched = DateTime.Now;
    //        foreach (var currentItem in items)
    //        {
    //            string key = null;
    //            foreach (var currentItemProviderId in currentItem.ProviderIds)
    //            {
    //                key = currentItemProviderId.Key + "=" + currentItemProviderId.Value + "|";
    //            }

    //            var user = this._userManager.Users[0];

    //            if (!string.IsNullOrWhiteSpace(key))
    //            {
    //                key = key.TrimEnd('|');
    //                if (currentItem.CommunityRating != null)
    //                {
    //                    var genreColumns = string.Empty;
    //                    var genreValues = string.Empty;

    //                    genreColumns = genres.Aggregate(
    //                            genreColumns,
    //                            (current, genre) => current + genre.Value + ", ")
    //                        .TrimEnd(',', ' ');
    //                    genreValues = genres.Aggregate(
    //                            genreValues,
    //                            (current, genre) => current + (currentItem.Genres.Contains(genre.Key) ? "1" : "0") + ", ")
    //                        .TrimEnd(',', ' ');

    //                    var updateCommandText =
    //                        $"INSERT OR REPLACE INTO Movies(Id, Name, CommunityRating, IsPlayed, IsDeleted, DateCreated, DateSynched, {genreColumns}) Values(?, ?, ?, ?, ?, ?, ?, {genreValues})";
    //                    var updateCommand = sqLiteConnection.CreateCommand(
    //                        updateCommandText,
    //                        key,
    //                        currentItem.Name,
    //                        currentItem.CommunityRating,
    //                        currentItem.IsPlayed(user),
    //                        false,
    //                        currentItem.DateCreated.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
    //                        datedSynched.ToString("yyyy-MM-dd HH:mm:ss"));

    //                    updateCommand.ExecuteNonQuery();
    //                }
    //            }
    //        }

    //        this._logger.Info("Synchronizing deleted items ...", null);
    //        var updateDeletedCommand = sqLiteConnection.CreateCommand(@"UPDATE Movies SET IsDeleted = true WHERE DateSynched <> ?", datedSynched.ToString("yyyy-MM-dd HH:mm:ss"));
    //        updateDeletedCommand.ExecuteNonQuery();

    //        var smartEmbyUrl = Environment.GetEnvironmentVariable("SMART_EMBY_SERVER_URL");
    //        this._logger.Info("Uploading File in Smart Emby server '{0}'....", smartEmbyUrl);
    //        var form = new MultipartFormDataContent();
    //        var fileContent = new ByteArrayContent(File.ReadAllBytes(databaseFileName));
    //        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
    //        form.Add(fileContent, "file", Path.GetFileName(databaseFileName));
    //        var httpClient = new HttpClient();
    //        var maxEpoch = Environment.GetEnvironmentVariable("SMART_EMBY_MAX_EPOCHS");
    //        if (!int.TryParse(maxEpoch, out _))
    //        {
    //            maxEpoch = "500";
    //        }

    //        var maxEpochWithNoImprovement = Environment.GetEnvironmentVariable("SMART_EMBY_MAX_EPOCHS_WITH_NO_IMPROVEMENT");
    //        if (!int.TryParse(maxEpochWithNoImprovement, out _))
    //        {
    //            maxEpochWithNoImprovement = "20";
    //        }

    //        var newMoviesCount = Environment.GetEnvironmentVariable("SMART_EMBY_NEW_MOVIES_COUNT");
    //        if (!int.TryParse(newMoviesCount, out _))
    //        {
    //            newMoviesCount = "50";
    //        }

    //        var response = await httpClient.PostAsync($"{smartEmbyUrl}/api/smartinbox/train?maxEpochs={maxEpoch}&maxEpochsWithNoImprovement={maxEpochWithNoImprovement}&newMoviesCount={newMoviesCount}", form);
    //        var trainingId = _jsonSerializer.DeserializeFromString<Guid>(await response.Content.ReadAsStringAsync());

    //        var streamWriter = new StreamWriter(File.OpenWrite("/config/plugins/SmartInbox.Emby.tid"));
    //        await streamWriter.WriteLineAsync(trainingId.ToString());
    //        streamWriter.Flush();
    //        streamWriter.Close();

    //        this._logger.Info("Saved Training Id '{trainingId}'", trainingId);
    //    }

    //    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    //    {
    //        return new[]
    //                   {
    //                       new TaskTriggerInfo
    //                           {
    //                               Type = TaskTriggerInfo.TriggerDaily,
    //                               TimeOfDayTicks = TimeSpan.FromHours(5).Ticks,
    //                               MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks
    //                           }
    //                   };
    //    }
    //}
}