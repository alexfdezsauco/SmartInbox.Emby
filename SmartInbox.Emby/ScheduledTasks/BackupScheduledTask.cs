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
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using SQLite.Net;
using SQLite.Net.Platform.Generic;

namespace SmartInbox.Emby.ScheduledTasks
{
    public class BackupScheduledTask : IScheduledTask
    {
        private readonly IApplicationPaths _appPaths;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly ILogger _logger;
        private readonly IMediaEncoder _mediaEncoder;

        public BackupScheduledTask(ILibraryManager libraryManager, ILogger logger, IMediaEncoder mediaEncoder,
            IFileSystem fileSystem, IApplicationPaths appPaths, ILibraryMonitor libraryMonitor)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _fileSystem = fileSystem;
            _appPaths = appPaths;
            _libraryMonitor = libraryMonitor;
        }

        public string Category => "Smart.Emby";

        public string Key => "Backup Data";

        public string Description => "Backup Data";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Console.WriteLine("Creating Database....");

            var databaseFileName = "/config/data/smart-inbox.sqlite";
            var sqLiteConnection = new SQLiteConnection(new SQLitePlatformGeneric(), databaseFileName);
            var sqLiteCommand = sqLiteConnection.CreateCommand(@"CREATE TABLE IF NOT EXISTS [Movies] (
                            [Id] TEXT NOT NULL PRIMARY KEY,
                            [Name] TEXT NOT NULL,
                            [CommunityRating] FLOAT NULL,
                            [IsPlayed] BIT NOT NULL,
                            [IsNew] BIT NOT NULL
                        )");
            sqLiteCommand.ExecuteNonQuery();

            var baseItem = _libraryManager.GetUserRootFolder().Children[1];
            var query = new InternalItemsQuery
            {
                MediaTypes = new[] {MediaType.Video}
            };

            var items = _libraryManager.GetItemList(query, new[] {baseItem}).OfType<Video>().ToList();

            Console.WriteLine("Updating Schema....");
            var genres = new List<string>();
            foreach (var currentItem in items)
            {
                foreach (var genre in currentItem.Genres)
                {
                    if (!genres.Contains(genre))
                    {
                        genres.Add(genre);
                    }
                }
            }

            var regex = new Regex("\\s+", RegexOptions.Compiled);
            foreach (var genre in genres)
            {
                var fieldName = "Is" + regex.Replace(genre, "");
                var liteCommand = sqLiteConnection.CreateCommand($@"ALTER TABLE [Movies]
                            ADD [{fieldName}] BIT NOT NULL default 0");
                try
                {
                    liteCommand.ExecuteNonQuery();
                }
                catch (SQLiteException)
                {
                }
            }

            foreach (var currentItem in items)
            {
                string key = null;
                foreach (var currentItemProviderId in currentItem.ProviderIds)
                {
                    key = currentItemProviderId.Key + "=" + currentItemProviderId.Value + "|";
                }

                if (!string.IsNullOrWhiteSpace(key))
                {
                    key = key.TrimEnd('|');
                    bool isNew = !(currentItem.DateCreated.DateTime < DateTime.Now.Subtract(TimeSpan.FromDays(30)));
                    if (currentItem.CommunityRating != null)
                    {
                        var genreColumns = string.Empty;
                        var genreValues = string.Empty;

                        genreColumns = genres.Aggregate(genreColumns, (current, genre) => current + "Is" + regex.Replace(genre, "") + ", ").TrimEnd(',', ' ');
                        genreValues = genres.Aggregate(genreValues, (current, genre) => current + (currentItem.Genres.Contains(genre) ? "1" : "0") + ", ").TrimEnd(',', ' ');

                        var updateCommandText = $"INSERT OR REPLACE INTO Movies (Id, Name, CommunityRating, IsPlayed, IsNew, {genreColumns}) VALUES(?, ?, ?, ?, ?, {genreValues})";
                        var updateCommand = sqLiteConnection.CreateCommand(updateCommandText, key, currentItem.Name, currentItem.CommunityRating, currentItem.Played, isNew);
                        Console.WriteLine(updateCommand.ToString());
                        updateCommand.ExecuteNonQuery();
                    }
                }
            }


            Console.WriteLine("Uploading File....");
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(File.ReadAllBytes(databaseFileName));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
            form.Add(fileContent, "file", Path.GetFileName(databaseFileName));
            var httpClient = new HttpClient();
            var response = await httpClient.PostAsync($"http://10.0.75.1:8080/api/smartinbox/upload", form);
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

        public string Name => "Backup Data";
    }
}