using System;
using System.Collections.Generic;

namespace TimelineVideoMaker.Timeline
{
    public interface ITimeline
    {
        /// <summary>
        /// タイムラインの要素を古い時刻から新しい時刻の順に返す。
        /// </summary>
        /// <param name="start"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        IEnumerable<ITimelineItem> GetItems(DateTimeOffset start, TimeSpan duration);
    }

    public interface ITimelineItem
    {
        DateTimeOffset Timestamp { get; }
        string HtmlDocument { get; }
    }
}
