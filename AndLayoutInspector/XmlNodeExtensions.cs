using System.Drawing;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AndLayoutInspector
{
    public static class XmlNodeExtensions
    {
        public static Rectangle? GetBounds(this XElement node)
        {
            var bounds = node.Attribute("bounds")?.Value;
            if (null == bounds)
                return null;
            bounds = Regex.Replace(bounds, "[^0-9]", " ").Trim();
            string[] numbers = Regex.Split(bounds, @"\D+");
            var rect = new Rectangle()
            {
                X = int.Parse(numbers[0]),
                Y = int.Parse(numbers[1]),
            };
            rect.Width = int.Parse(numbers[2]) - rect.X;
            rect.Height = int.Parse(numbers[3]) - rect.Y;
            return rect;
        }
    }
}
