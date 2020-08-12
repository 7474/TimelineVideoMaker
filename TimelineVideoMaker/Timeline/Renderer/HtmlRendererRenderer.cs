using System.Drawing;
using TheArtOfDev.HtmlRenderer.WinForms;

namespace TimelineVideoMaker.Timeline.Renderer
{
    public class HtmlRendererRenderer : IRenderer
    {
        public Bitmap RenderImage(ITimelineItem item, int width, int maxHeight)
        {
            var w = width;
            var h = maxHeight;
            var bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                // XXX 高さどう取るの？
                // XXX フォント読まれてない？
                HtmlRender.Render(g, item.HtmlDocument, 0, 0, w);
                //g.DrawString(item.HtmlDocument, font, System.Drawing.Brushes.Black, 4f, 4f);
            }

            return bitmap;
        }
    }
}
