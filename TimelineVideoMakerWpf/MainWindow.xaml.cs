using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using TimelineVideoMaker.Timeline;
using TimelineVideoMaker.Timeline.Twitter;
using TimelineVideoMaker.Timeline.WMF;

namespace TimelineVideoMakerWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private TwitterTimeline twitterTimeline;
        private IVideoMaker videoMaker;
        private IList<ITimelineItem> timelineItems;

        public ObservableCollection<ITimelineItem> ObTimelineItems { get; set; }

        public MainWindow()
        {
            twitterTimeline = new TwitterTimeline();
            videoMaker = new WMFVideoMaker();

            ObTimelineItems = new ObservableCollection<ITimelineItem>();

            // XXX それでいいのか
            DataContext = this;

            InitializeComponent();
        }

        private async void twitterAuthorize_ClickAsync(object sender, RoutedEventArgs e)
        {
            var key = twitterConsumerKey.Text;
            var secret = twitterConsumerSecret.Text;

            await twitterTimeline.AuthorizeAsync(key, secret);
            // XXX データバインド
            twitterAuthorizeUri.Text = twitterTimeline.AuthorizeUri.ToString();
        }

        private void twitterOpenAuthorizeUrl_Click(object sender, RoutedEventArgs e)
        {
            var uri = twitterAuthorizeUri.Text;

            Process.Start(uri);
        }

        private async void twitterGetTokens_ClickAsync(object sender, RoutedEventArgs e)
        {
            var pinCode = twitterPinCode.Text;

            await twitterTimeline.GetTokensAsync(pinCode);
        }

        private void twitterGetItems_Click(object sender, RoutedEventArgs e)
        {
            var start = getStartDatetime();
            var duration = durationMinutes.Value.Value;
            var query = twitterQuery.Text;

            twitterTimeline.Query = query;
            timelineItems = twitterTimeline.GetItems(start, duration).ToList();
        }
        private DateTimeOffset getStartDatetime()
        {
            return new DateTimeOffset(startDate.Value.Value);
        }

        private async void makeVideo_ClickAsync(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog();

            if (sfd.ShowDialog() == true)
            {
                //var start = getStartDatetime();
                var start = timelineItems.Min(x => x.Timestamp).AddSeconds(-3);

                var file = sfd.FileName;
                await videoMaker.ExportAsync(file, start, timelineItems);
            }
        }

        private async void saveTwitterItems_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog();

            if (sfd.ShowDialog() == true)
            {
                var file = sfd.FileName;
                await TwitterTimelineItem.SaveToFileAsync(file, timelineItems.Select(x => x as TwitterTimelineItem));
            }
        }

        private async void loadTwitterItems_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                var file = ofd.FileName;
                timelineItems = (await TwitterTimelineItem.ReadFromFileAsync(file))
                    .Select(x => x as ITimelineItem)
                    .ToList();

                ObTimelineItems.Clear();
                foreach (var item in timelineItems)
                {
                    ObTimelineItems.Add(item);
                }
            }
        }
    }
}
