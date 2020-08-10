using CoreTweet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace TimelineVideoMaker.Timeline.Twitter
{
    public class TwitterTimeline : ITimeline
    {
        private OAuth.OAuthSession _session;
        private Tokens _tokens;

        public string Query { get; set; }

        public async Task AuthorizeAsync(string consumerKey, string consumerSecret)
        {
            _session = await OAuth.AuthorizeAsync(consumerKey, consumerSecret);
        }
        public Uri AuthorizeUri => _session?.AuthorizeUri;

        public async Task GetTokensAsync(string pinCode)
        {
            _tokens = await OAuth.GetTokensAsync(_session, pinCode);
        }

        public IEnumerable<ITimelineItem> GetItems(DateTimeOffset start, TimeSpan duration)
        {
            var end = start.Add(duration);
            var statuses = new List<Status>();
            // XXX until指定
            SearchResult res = _tokens.Search.Tweets(Query);

            while (res.SearchMetadata.Count > 0 && res.Any(x => x.CreatedAt >= start))
            {
                statuses.AddRange(res);
                var nextResults = HttpUtility.ParseQueryString(res.SearchMetadata.NextResults);
                IDictionary<string, object> nextResultsDict = nextResults.Keys
                    .Cast<string>()
                    .ToDictionary(x => x, x => nextResults.Get(x) as object);

                // XXX Wait...
                Task.Delay(100).Wait();
                res = _tokens.Search.Tweets(nextResultsDict);
            }

            return statuses
                .Where(x => x.CreatedAt >= start)
                .Where(x => x.CreatedAt <= end)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new TwitterTimelineItem(x));
        }
    }

    public class TwitterTimelineItem : ITimelineItem
    {
        public TwitterTimelineItem(Status status)
        {
            Status = status;
        }

        public Status Status { get; private set; }
        public DateTimeOffset Timestamp => Status.CreatedAt;

        private string _htmlDocument;
        public string HtmlDocument
        {
            get
            {
                if (_htmlDocument == null)
                {
                    _htmlDocument = renderHtml();
                }
                return _htmlDocument;
            }
        }

        private string renderHtml()
        {
            return $@"
<html>
<head>
<meta charset=""UTF-8"">
<style type=""text/css"">
body {{ 
    margin: 0;
    background-color: #fff;
    color: #000;
    font-family: ""Meiryo UI"";
    font-size: 1em;
}}
.icon {{
    width: 32px;
    height: 32px;
    float: left;
    margin: 4px;
}}
.user-name {{
    font-weight: bold;
}}
.timestamp {{
    color: #666;
    font-size: 0.8em;
}}
.tweet-header {{
}}
.tweet-text {{
    clear: both;
}}
</style>
</head>
<body>
<div class=""item"">
    <img class=""icon"" src=""{Status.User.ProfileImageUrlHttps}"">
    <div class=""tweet-header"">
        <span class=""user-name"">@{Status.User.ScreenName}</span><br>
        <span class=""timestamp"">{Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}</span>
    </div>
    <div class=""tweet-text"">{Status.Text}</div>
</div>
</body>
</html>";
        }

        public static async Task SaveToFileAsync(string filePath, IEnumerable<TwitterTimelineItem> items)
        {
            using (var writer = new StreamWriter(filePath))
            {
                foreach (var item in items)
                {
                    await writer.WriteLineAsync(JsonConvert.SerializeObject(item.Status));
                }
            }
        }

        public static async Task<IEnumerable<TwitterTimelineItem>> ReadFromFileAsync(string filePath)
        {
            var items = new List<TwitterTimelineItem>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    items.Add(new TwitterTimelineItem(JsonConvert.DeserializeObject<Status>(line)));
                }
            }
            return items;
        }
    }
}
