namespace SmartInbox.Emby.ScheduledTasks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
    using MediaBrowser.Controller.Entities.Movies;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Controller.MediaEncoding;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Querying;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.Tasks;

    using SQLite.Net;
    using SQLite.Net.Platform.Generic;

    public class TrainModelScheduledTask : IScheduledTask
    {
        private readonly IApplicationPaths _appPaths;

        private readonly IUserDataManager _dataManager;

        private readonly IFileSystem _fileSystem;

        private readonly IJsonSerializer _jsonSerializer;

        private readonly ILibraryManager _libraryManager;

        private readonly ILibraryMonitor _libraryMonitor;

        private readonly ILogger _logger;

        private readonly IMediaEncoder _mediaEncoder;

        private readonly Plugin _plugin;

        private readonly IUserManager _userManager;

        private readonly IServerConfigurationManager configurationManager;

        public TrainModelScheduledTask(
            ILibraryManager libraryManager, ILogManager logManager, IMediaEncoder mediaEncoder, IJsonSerializer jsonSerializer,
            IFileSystem fileSystem, IApplicationPaths appPaths, ILibraryMonitor libraryMonitor, IUserManager userManager)
        {
            this._libraryManager = libraryManager;
            this._logger = logManager.GetLogger(this.GetType().Name);
            this._mediaEncoder = mediaEncoder;
            this._jsonSerializer = jsonSerializer;
            this._fileSystem = fileSystem;
            this._appPaths = appPaths;
            this._libraryMonitor = libraryMonitor;
            this._userManager = userManager;
        }

        public string Category => "Smart Inbox";

        public string Description => "Create data set & train a model based on user playback action";

        public string Key => Keys.Backup;

        public string Name => "Model Training";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var databaseFileName = "/config/data/smart-inbox.db";
            this._logger.Info("Creating Database '{0}'...", databaseFileName);
            var sqLiteConnection = new SQLiteConnection(new SQLitePlatformGeneric(), databaseFileName);
            var createTableCommand = sqLiteConnection.CreateCommand(
                @"CREATE TABLE IF NOT EXISTS [Movies] (
                            [Id] TEXT NOT NULL PRIMARY KEY,
                            [Name] TEXT NOT NULL,
                            [CommunityRating] FLOAT NULL,
                            [IsPlayed] BIT NOT NULL,
                            [IsDeleted] BIT NOT NULL,
                            [DateCreated] DATE NOT NULL,
                            [DateSynched] DATE NOT NULL
                        )");
            createTableCommand.ExecuteNonQuery();

            try
            {
                var createIndexCommand = sqLiteConnection.CreateCommand("CREATE INDEX \"Id_Idx\" ON \"Movies\" (\"Id\" ASC)");
                createIndexCommand.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                this._logger.Warn("Error creating index: {0}", e.Message);
            }

            var recursiveChildren = this._libraryManager.RootFolder.GetRecursiveChildren();
            if (recursiveChildren.Length == 0)
            {
                this._logger.Warn("UserRootFolder has no recursive children");

                return;
            }

            var baseItem = recursiveChildren[1];
            var query = new InternalItemsQuery
                            {
                                MediaTypes = new[]
                                                 {
                                                     MediaType.Video,
                                                 },
                            };

            var items = this._libraryManager.GetItemList(
                query,
                new[]
                    {
                        baseItem,
                    }).OfType<Movie>().ToList();

            this._logger.Info("Computing genres...");
            var regex = new Regex("(\\s+|-)", RegexOptions.Compiled);
            var genres = new SortedDictionary<string, string>();
            foreach (var currentItem in items)
            {
                foreach (var genre in currentItem.Genres)
                {
                    var trimmedGenre = genre.Trim();
                    var trimmedGenreLower = trimmedGenre.ToLowerInvariant();
                    if (!genres.ContainsKey(trimmedGenreLower))
                    {
                        var fieldName = "Is" + regex.Replace(trimmedGenre, string.Empty);
                        genres[trimmedGenreLower] = fieldName;
                    }
                }
            }

            this._logger.Info("Computed genres");

            var schemaQueryCommand = sqLiteConnection.CreateCommand("SELECT * FROM [Movies] LIMIT 1");
            var commandResult = schemaQueryCommand.ExecuteDeferredQuery();
            var columnNames = commandResult.ColumnNames.ToList();

            this._logger.Info("Updating table '{0}' schema ...", "Movies");

            foreach (var genre in genres)
            {
                var fieldName = genre.Value;
                if (!columnNames.Contains(fieldName, StringComparer.InvariantCultureIgnoreCase))
                {
                    var alterTableCommand = sqLiteConnection.CreateCommand(
                        $@"ALTER TABLE [Movies]
                    ADD [{fieldName}] BIT NOT NULL default 0");
                    try
                    {
                        alterTableCommand.ExecuteNonQuery();
                    }
                    catch (SQLiteException e)
                    {
                        this._logger.ErrorException("Error altering [Movies] table", e);
                    }
                }
            }

            this._logger.Info("Updated table '{0}' schema", "Movies");

            var user = this._userManager.GetUserList(
                new UserQuery
                    {
                        IsDisabled = false,
                    }).FirstOrDefault();


            this._logger.Info("Updating table '{0}'...", "Movies");

            var beginCommand = sqLiteConnection.CreateCommand("BEGIN");
            beginCommand.ExecuteNonQuery();

            var datedSynched = DateTime.Now;
            for (var index = 0; index < items.Count; index++)
            {
                progress.Report(index * 100.0d / (2 * items.Count));

                var currentItem = items[index];
                var key = string.Empty;
                foreach (var currentItemProviderId in currentItem.ProviderIds.OrderBy(pair => pair.Key))
                {
                    key += currentItemProviderId.Key + "=" + currentItemProviderId.Value + "|";
                }

                key = key.TrimEnd('|');

                if (!string.IsNullOrWhiteSpace(key))
                {
                    if (currentItem.CommunityRating != null)
                    {
                        var genreColumns = string.Empty;
                        var genreValues = string.Empty;

                        genreColumns = genres.Aggregate(genreColumns, (current, genre) => current + genre.Value + ", ")
                            .TrimEnd(',', ' ');
                        genreValues = genres.Aggregate(
                            genreValues,
                            (current, genre) =>
                                current + (Array.FindIndex(
                                               currentItem.Genres,
                                               g => g.Trim().ToLowerInvariant() == genre.Key) != -1
                                               ? "1"
                                               : "0") + ", ").TrimEnd(',', ' ');

                        var dateCreated = currentItem.DateCreated.DateTime;
                        if (currentItem.DateCreated < currentItem.DateModified)
                        {
                            dateCreated = currentItem.DateModified.DateTime;
                        }

                        var updateCommandText =
                            $"INSERT OR REPLACE INTO Movies(Id, Name, CommunityRating, IsPlayed, IsDeleted, DateCreated, DateSynched, {genreColumns}) Values(?, ?, ?, ?, ?, ?, ?, {genreValues})";

                        var updateCommand = sqLiteConnection.CreateCommand(
                            updateCommandText,
                            key,
                            currentItem.Name,
                            currentItem.CommunityRating,
                            currentItem.IsPlayed(user),
                            false,
                            dateCreated.ToString("yyyy-MM-dd HH:mm:ss"),
                            datedSynched.ToString("yyyy-MM-dd HH:mm:ss"));

                        updateCommand.ExecuteNonQuery();
                    }
                }
            }

            var endCommand = sqLiteConnection.CreateCommand("END");
            endCommand.ExecuteNonQuery();


            this._logger.Info("Synchronizing deleted items ...", null);
            var updateDeletedCommand = sqLiteConnection.CreateCommand(
                @"UPDATE Movies SET IsDeleted = true WHERE DateSynched <> ?",
                datedSynched.ToString("yyyy-MM-dd HH:mm:ss"));
            updateDeletedCommand.ExecuteNonQuery();

            var smartEmbyUrl = Environment.GetEnvironmentVariable("SMART_EMBY_SERVER_URL");
            this._logger.Info("Uploading File in Smart Emby server '{0}'....", smartEmbyUrl);
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(File.ReadAllBytes(databaseFileName));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
            form.Add(fileContent, "file", Path.GetFileName(databaseFileName));
            var httpClient = new HttpClient();
            var maxEpoch = Environment.GetEnvironmentVariable("SMART_EMBY_MAX_EPOCHS");
            if (!int.TryParse(maxEpoch, out _))
            {
                maxEpoch = "500";
            }

            var maxEpochWithNoImprovement = Environment.GetEnvironmentVariable("SMART_EMBY_MAX_EPOCHS_WITH_NO_IMPROVEMENT");
            if (!int.TryParse(maxEpochWithNoImprovement, out _))
            {
                maxEpochWithNoImprovement = "20";
            }

            var newMoviesCount = Environment.GetEnvironmentVariable("SMART_EMBY_NEW_MOVIES_COUNT");
            if (!int.TryParse(newMoviesCount, out _))
            {
                newMoviesCount = "50";
            }
            
            var oldMoviesToTreatAsNew = Environment.GetEnvironmentVariable("SMART_EMBY_OLD_MOVIES_TO_TREAT_AS_NEW");
            if (!int.TryParse(oldMoviesToTreatAsNew, out var oldMoviesToTreatAsNewInt))
            {
                oldMoviesToTreatAsNew = Math.Truncate(oldMoviesToTreatAsNewInt * 0.10f).ToString(CultureInfo.InvariantCulture);
            }

            var uri = $"{smartEmbyUrl}/api/smartinbox/train?maxEpochs={maxEpoch}&maxEpochsWithNoImprovement={maxEpochWithNoImprovement}&newMoviesCount={newMoviesCount}&oldMoviesToTreatAsNew={oldMoviesToTreatAsNew}";
            var response = await httpClient.PostAsync(uri, form, cancellationToken);
            var trainingId = this._jsonSerializer.DeserializeFromString<Guid>(await response.Content.ReadAsStringAsync());

            var streamWriter = new StreamWriter(File.OpenWrite("/config/plugins/SmartInbox.Emby.tid"));
            await streamWriter.WriteLineAsync(trainingId.ToString());
            await streamWriter.FlushAsync();
            streamWriter.Close();

            this._logger.Info("Saved Training Id '{trainingId}'", trainingId);

            progress.Report(75);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var recommendationsResponse = await httpClient.GetAsync($"{smartEmbyUrl}/api/smartinbox/recommendations?id=" + trainingId, cancellationToken);
                    recommendationsResponse.EnsureSuccessStatusCode();
                    progress.Report(100);
                    return;
                }
                catch (Exception ex)
                {
                    this._logger.Warn("Recommendations for training '{trainingId}' are not available yet.", ex.Message);
                }
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
                       {
                           new TaskTriggerInfo
                               {
                                   Type = TaskTriggerInfo.TriggerDaily,
                                   TimeOfDayTicks = TimeSpan.FromHours(5).Ticks,
                                   MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks,
                               },
                       };
        }
    }
}