using Syncfusion.HtmlConverter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimelineVideoMaker.Timeline;

namespace TimelineVideoMaker.Timeline.Renderer
{
    // https://www.syncfusion.com/kb/9612/how-to-convert-html-to-image-using-c-and-vb-net
    public class SyncfusionRdnderer : IRenderer
    {
        public Bitmap RenderImage(ITimelineItem item, int width, int maxHeight)
        {
            var htmlConverter = new HtmlToPdfConverter(HtmlRenderingEngine.WebKit);
            var settings = new WebKitConverterSettings()
            {
                //WebKitPath = @"/QtBinaries/",
                WebKitViewPort = new Size(width, maxHeight),
            };
            htmlConverter.ConverterSettings = settings;

            var images = htmlConverter.ConvertToImage(item.HtmlDocument, "");

            return new Bitmap(images.First());
        }
    }
}
