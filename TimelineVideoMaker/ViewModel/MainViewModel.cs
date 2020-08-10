using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimelineVideoMaker.Timeline;
using TimelineVideoMaker.Timeline.WMF;

namespace TimelineVideoMakerWpf.ViewModel
{
    class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ITimelineItem> ObTimelineItems { get; set; }
        // XXX インタフェース整理しない。
        private WMFVideoMaker VideoMakeProgress;

        public string ProgressStatus => VideoMakeProgress.ProgressStatus;

        public MainViewModel(ObservableCollection<ITimelineItem> obTimelineItems, WMFVideoMaker videoMaker)
        {
            ObTimelineItems = obTimelineItems;
            VideoMakeProgress = videoMaker;

            VideoMakeProgress.PropertyChanged += VideoMakeProgress_PropertyChanged;
        }

        private void VideoMakeProgress_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("ProgressStatus");
        }

        public void SetItems(IEnumerable<ITimelineItem> items)
        {
            ObTimelineItems.Clear();
            foreach (var item in items)
            {
                ObTimelineItems.Add(item);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string info)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
