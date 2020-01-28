// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Recommendation.cs" company="WildGums">
//   Copyright (c) 2008 - 2020 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmartInbox.Emby.ScheduledTasks
{
    public class Recommendation
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public RecommendationType RecommendationType { get; set; }
    }
}