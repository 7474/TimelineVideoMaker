using System;

namespace TimelineVideoMaker.Timeline.WMF
{
    internal class EmptyTimelineItem : ITimelineItem
    {
        public DateTimeOffset Timestamp => throw new NotImplementedException();

        public string HtmlDocument => "";
    }
}