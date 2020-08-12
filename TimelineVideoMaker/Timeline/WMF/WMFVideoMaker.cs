using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimelineVideoMaker.Timeline.Renderer;

namespace TimelineVideoMaker.Timeline.WMF
{
    public class WMFVideoMaker : IDisposable, IVideoMaker, INotifyPropertyChanged
    {
        public WMFVideoMaker()
        {
            // XXX Startup/Shutdown はライフサイクルがこのオブジェクトとは違う。
            // が、他所でWMF使うわけないので事実上問題はない。
            MediaManager.Startup();
            MediaFactory.Startup(MediaFactory.Version, 0);
        }

        // XXX To configurable
        private int videoWidth = 240;
        // XXX 縦長にしてスクロールアウトしていかせたいね
        private int videoHeight = 360;
        private int frameRate = 10;
        private long frameDuration => 10 * 1000 * 1000 / frameRate;
        private int bufferLength => (int)(videoWidth * videoHeight * 4);
        private int minDisplaySeconds = 5;
        // リアルタイムな動画にするならフラグを立てる
        private bool realTimeline = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ProgressStatus => $"{ProgressRate * 100}% ({ProgressCurrent}/{ProgressMax})";
        public int ProgressMax { get; private set; }
        public int ProgressCurrent { get; private set; }
        public double ProgressRate => ProgressMax > 0 ? (double)ProgressCurrent / ProgressMax : 0.0;

        private IRenderer renderer = new SyncfusionRdnderer();
        //private IRenderer renderer = new HtmlRendererRenderer();

        private void updateStatus(int current, int max)
        {
            ProgressCurrent = current;
            ProgressMax = max;

            if (PropertyChanged != null)
            {
                foreach (var name in new List<string>()
                {
                    "ProgressStatus","ProgressMax","ProgressCurrent","ProgressRate"
                })
                {
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
                }
            }
        }

        public async Task ExportAsync(string filePath, DateTimeOffset start, IEnumerable<ITimelineItem> items)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            using (var bs = new ByteStream(stream))
            {
                // https://github.com/sharpdx/SharpDX/issues/809
                // https://docs.microsoft.com/en-us/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video
                int streamIndex;
                SinkWriter sinkWriter = createWriter(bs, out streamIndex);

                sinkWriter.BeginWriting();

                long prevRecordingDuration = 0;
                long recordDuration = 0;
                int i = 0;
                ITimelineItem lastItem = new EmptyTimelineItem();
                // XXX 最大を知るには一旦走査しないといけない。。。
                var itemsList = items.ToList();
                foreach (var item in itemsList)
                {
                    i++;
                    updateStatus(i, itemsList.Count);

                    recordDuration = nextDuration(start, recordDuration, item);
                    writeItem(filePath, sinkWriter, streamIndex, prevRecordingDuration, recordDuration, i, lastItem);

                    prevRecordingDuration = recordDuration;
                    lastItem = item;
                }
                // 最後を書く
                // XXX 長さ指定してそこまで伸ばすようにするなど正規化。
                recordDuration = nextDuration(start, recordDuration, lastItem);
                writeItem(filePath, sinkWriter, streamIndex, prevRecordingDuration, recordDuration, ++i, lastItem);

                sinkWriter.Finalize();
            }
        }

        private SinkWriter createWriter(ByteStream bs, out int streamIndex)
        {
            MediaAttributes attributes = new MediaAttributes(10);
            attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1);
            SinkWriter sinkWriter = MediaFactory.CreateSinkWriterFromURL(@".mp4", bs, attributes);

            // Set up the output media type
            MediaType typeOut = new MediaType();
            MediaFactory.CreateMediaType(typeOut);
            typeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            typeOut.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            // XXX こんなにビットレート要らん気がする。
            typeOut.Set(MediaTypeAttributeKeys.AvgBitrate, 1 * 1000 * 1000);
            typeOut.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
            typeOut.Set(MediaTypeAttributeKeys.FrameSize, ((long)videoWidth << 32) | (long)videoHeight);
            typeOut.Set(MediaTypeAttributeKeys.FrameRate, ((long)frameRate << 32) | 1);
            typeOut.Set(MediaTypeAttributeKeys.PixelAspectRatio, ((long)1 << 32) | 1);
            sinkWriter.AddStream(typeOut, out streamIndex);

            // Set up the input media type
            MediaType typeIn = new MediaType();
            MediaFactory.CreateMediaType(typeIn);
            typeIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            typeIn.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
            typeIn.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
            typeIn.Set(MediaTypeAttributeKeys.FrameSize, ((long)videoWidth << 32) | (long)videoHeight);
            typeIn.Set(MediaTypeAttributeKeys.FrameRate, ((long)frameRate << 32) | 1);
            typeIn.Set(MediaTypeAttributeKeys.PixelAspectRatio, ((long)1 << 32) | 1);

            sinkWriter.SetInputMediaType(streamIndex, typeIn, null);    // Additional information: HRESULT: [0xC00D36B4], Module: [Unknown], ApiCode: [Unknown/Unknown], Message: Unknown

            return sinkWriter;
        }

        private long nextDuration(DateTimeOffset start, long recordDuration, ITimelineItem item)
        {
            // XXX Duration ずれてる
            var itemTimestampDuration = ((long)(item.Timestamp.Subtract(start)).TotalSeconds) * frameRate * frameDuration;
            // 最低5秒位進める（表示する）
            recordDuration = recordDuration + minDisplaySeconds * frameRate * frameDuration;
            // 要素のタイムスタンプの方が先なら進める
            if (realTimeline && itemTimestampDuration > recordDuration)
            {
                recordDuration = itemTimestampDuration;
            }

            return recordDuration;
        }

        private void writeItem(string filePath, SinkWriter sinkWriter, int streamIndex, long prevRecordingDuration, long recordDuration, int i, ITimelineItem item)
        {
            // https://stackoverflow.com/questions/44402898/mf-sinkwriter-write-sample-failed
            //https://stackoverflow.com/questions/47930340/sinkwriter-writesample-fails-with-e-invalidarg
            using (var itemImage = renderItem(item))
            using (var buffer = MediaFactory.CreateMemoryBuffer(bufferLength))
            using (var sample = MediaFactory.CreateSample())
            {
                // XXX Debug
                itemImage.Save(filePath + $".{i}.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
                File.WriteAllText(filePath + $".{i}.html", item.HtmlDocument);

                int maxRef, currentRef;
                var bufPointer = buffer.Lock(out maxRef, out currentRef);
                // XXX 何か上下反転してる。メモリ上の配置が逆なのか？
                itemImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var itemImageBits = itemImage.LockBits(
                    new Rectangle(0, 0, itemImage.Width, itemImage.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                MediaFactory.CopyImage(
                    bufPointer, itemImageBits.Stride,
                    itemImageBits.Scan0, itemImageBits.Stride,
                    itemImageBits.Width * 4, itemImageBits.Height);
                itemImage.UnlockBits(itemImageBits);
                buffer.Unlock();
                buffer.CurrentLength = bufferLength;

                sample.AddBuffer(buffer);
                sample.SampleTime = prevRecordingDuration;
                sample.SampleDuration = recordDuration - prevRecordingDuration;

                sinkWriter.WriteSample(streamIndex, sample);
            }
        }

        private Bitmap renderItem(ITimelineItem item)
        {
            var w = videoWidth;
            // XXX 今のところビデオのフレームと合わせておく
            //var h = w / 2;
            var h = videoHeight;

            return renderer.RenderImage(item, w, h);
        }

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
            MediaManager.Shutdown();
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~WMFVideoMaker()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
