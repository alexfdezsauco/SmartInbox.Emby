namespace SmartInbox.Emby.ScheduledTasks
{
    public class Recommendation
    {
        public string Id { get; set; }

        public RecommendationType RecommendationType { get; set; }

        public string Title { get; set; }
    }
}