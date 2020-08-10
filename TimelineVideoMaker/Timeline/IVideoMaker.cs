using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TimelineVideoMaker.Timeline
{
    public interface IVideoMaker
    {
        Task ExportAsync(string filePath, DateTimeOffset start, IEnumerable<ITimelineItem> items);
    }
}
