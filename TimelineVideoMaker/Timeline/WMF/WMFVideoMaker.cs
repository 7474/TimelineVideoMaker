using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TheArtOfDev.HtmlRenderer.WinForms;

namespace TimelineVideoMaker.Timeline.WMF
{
    public class WMFVideoMaker : IDisposable, IVideoMaker
    {
        private bool disposedValue;

        public WMFVideoMaker()
        {
            // XXX Startup/Shutdown はライフサイクルがこのオブジェクトとは違う。
            MediaManager.Startup();
            MediaFactory.Startup(MediaFactory.Version, 0);
        }

        public async Task ExportAsync(string filePath, DateTimeOffset start, IEnumerable<ITimelineItem> items)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            using (var bs = new ByteStream(stream))
            {
                // https://github.com/sharpdx/SharpDX/issues/809
                // https://docs.microsoft.com/en-us/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video
                var bufferLength = (int)(videoWidth * videoHeight * 4);
                var frameRate = 10;
                var frameDuration = 10 * 1000 * 1000 / frameRate;

                MediaAttributes attributes = new MediaAttributes(10);
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1);
                var sinkWriter = MediaFactory.CreateSinkWriterFromURL(@".mp4", bs, attributes);

                // Set up the output media type
                MediaType typeOut = new MediaType();
                MediaFactory.CreateMediaType(typeOut);
                typeOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                typeOut.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
                typeOut.Set(MediaTypeAttributeKeys.AvgBitrate, 1 * 1000 * 1000);
                typeOut.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
                typeOut.Set(MediaTypeAttributeKeys.FrameSize, ((long)videoWidth << 32) | (long)videoHeight);
                typeOut.Set(MediaTypeAttributeKeys.FrameRate, ((long)frameRate << 32) | 1);
                typeOut.Set(MediaTypeAttributeKeys.PixelAspectRatio, ((long)1 << 32) | 1);

                int streamIndex;
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

                sinkWriter.BeginWriting();

                long prevRecordingDuration = 0;
                long recordDuration = 0;
                int i = 0;
                foreach (var item in items)
                {
                    if (++i > 3) break;

                    // XXX Duration ずれてる
                    recordDuration = ((long)(item.Timestamp.Subtract(start)).TotalSeconds) * frameRate * frameDuration;

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
                    prevRecordingDuration = recordDuration;
                }
                sinkWriter.Finalize();
            }
        }

        //private Font font = new Font(System.Drawing.FontFamily.GenericSansSerif, 1.0f);
        private int videoWidth = 240;
        private int videoHeight = 640;
        private Bitmap renderItem(ITimelineItem item)
        {
            var w = videoWidth;
            // XXX 今のところビデオのフレームと合わせておく
            //var h = w / 2;
            var h = videoHeight;
            var bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                HtmlRender.Render(g, item.HtmlDocument, 0, 0, w);
                //g.DrawString(item.HtmlDocument, font, System.Drawing.Brushes.Black, 4f, 4f);
            }

            return bitmap;
        }

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
    }
}
