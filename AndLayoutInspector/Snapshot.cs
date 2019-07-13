using System.Drawing;
using System.Xml.Linq;

namespace AndLayoutInspector
{
    public class Snapshot
    {
        public XDocument Tree { get; set; }
        public Image Image { get; set; }
        public bool IsLandscape { get => Image.Height < Image.Width; }
    }
}
