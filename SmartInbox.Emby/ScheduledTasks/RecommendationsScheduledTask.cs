// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RecommendationsScheduledTask.cs" company="WildGums">
//   Copyright (c) 2008 - 2020 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmartInbox.Emby.ScheduledTasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Common.Configuration;
    using MediaBrowser.Controller.Configuration;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Controller.MediaEncoding;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.Tasks;

    using SQLite.Net;
    using SQLite.Net.Platform.Generic;

    public class RecommendationsScheduledTask : IScheduledTask
    {
        private readonly IApplicationPaths _appPaths;

        private readonly IFileSystem _fileSystem;

        private readonly IJsonSerializer _jsonSerializer;

        private readonly ILibraryManager _libraryManager;

        private readonly ILibraryMonitor _libraryMonitor;

        private readonly ILogger _logger;

        private readonly IMediaEncoder _mediaEncoder;

        private readonly Plugin _plugin;

        private readonly IServerConfigurationManager configurationManager;

        public RecommendationsScheduledTask(
            ILibraryManager libraryManager,
            ILogger logger,
            IMediaEncoder mediaEncoder,
            IJsonSerializer jsonSerializer,
            IFileSystem fileSystem,
            IApplicationPaths appPaths,
            ILibraryMonitor libraryMonitor,
            Plugin plugin)
        {
            this._libraryManager = libraryManager;
            this._logger = logger;
            this._mediaEncoder = mediaEncoder;
            this._jsonSerializer = jsonSerializer;
            this._fileSystem = fileSystem;
            this._appPaths = appPaths;
            this._libraryMonitor = libraryMonitor;
            this._plugin = plugin;
        }

        public string Category => "Smart Inbox";

        public string Description => "Get recommended movies based on the user playback actions";

        public string Key => Keys.Recommendations;

        public string Name => "Get Recomendations";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            this._logger.Info("Getting recommendations ....", null);
            var smartEmbyUrl = Environment.GetEnvironmentVariable("SMART_EMBY_SERVER_URL");
            var trainingIdFile = "/config/plugins/SmartInbox.Emby.tid";

            if (!File.Exists(trainingIdFile))
            {
                this._logger.Error("There is not available training id. Run Backup first.", null);
                return;
            }

            var fileStream = File.OpenRead(trainingIdFile);
            var streamReader = new StreamReader(fileStream);
            var readLineAsync = await streamReader.ReadLineAsync();
            var trainingId = Guid.Parse(readLineAsync);
            streamReader.Close();

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{smartEmbyUrl}/api/smartinbox/recommendations?id=" + trainingId);
            var recommendations =  this._jsonSerializer.DeserializeFromString<List<Recommendation>>(await response.Content.ReadAsStringAsync());

            var databaseFileName = "/config/data/smart-inbox-recommendations.db";
            var sqLiteConnection = new SQLiteConnection(new SQLitePlatformGeneric(), databaseFileName);
            var sqLiteCommand = sqLiteConnection.CreateCommand(
                @"CREATE TABLE IF NOT EXISTS [Recommendations] (
                            [Id] TEXT NOT NULL PRIMARY KEY,
                            [Name] TEXT NOT NULL,
                            [Recommendation] INT NOT NULL)");
            sqLiteCommand.ExecuteNonQuery();

            this._logger.Info("Deleting existing recommendations", null);
            var deleteCommand = sqLiteConnection.CreateCommand(
                @"DELETE FROM Recommendations");
            deleteCommand.ExecuteNonQuery();

            if (recommendations.Any())
            {
                foreach (var recommendation in recommendations)
                {
                    var insertCommand = sqLiteConnection.CreateCommand(
                        @"INSERT INTO Recommendations(Id, Name, Recommendation) Values(?, ?, ?)", recommendation.Id, recommendation.Title, recommendation.RecommendationType);
                    insertCommand.ExecuteNonQuery();
                }

                this._logger.Info("Saved recommendations", null);
            }
            else
            {
                this._logger.Info("There is no recommendations available for training id {0}", trainingId);
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
                                   MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks
                               }
                       };
        }
    }
}