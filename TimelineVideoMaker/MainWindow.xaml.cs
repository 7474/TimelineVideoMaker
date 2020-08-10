using CoreTweet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TimelineVideoMaker.Timeline;
using TimelineVideoMaker.Timeline.Twitter;
using TimelineVideoMaker.Timeline.WMF;
using TimelineVideoMakerWpf.ViewModel;

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

        private MainViewModel viewModel;


        public MainWindow()
        {
            // XXX VM作る気なかった。
            var wmfVideoMaker = new WMFVideoMaker();
            videoMaker = wmfVideoMaker;
            twitterTimeline = new TwitterTimeline();

            viewModel = new MainViewModel(new ObservableCollection<ITimelineItem>(), wmfVideoMaker);

            DataContext = viewModel;

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
                var start = timelineItems.Min(x => x.Timestamp);

                var file = sfd.FileName;
                new Task(async () =>
                {
                    // XXX これ実体が非同期じゃないので戻ってきたタスクをStart出来ない
                    await videoMaker.ExportAsync(file, start, timelineItems);
                }).Start();
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

                viewModel.SetItems(timelineItems);
            }
        }
    }
}
