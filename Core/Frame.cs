using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Media.Casting;
using WinRT;

namespace GIFImage.WinUI.Core
{
    public class Frame
    {
        private Bitmap bitmap;

        public int Delay { get; set; }

        public Bitmap Bitmap
        {
            get => bitmap;
            set => bitmap = value;
        }

        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public byte[] MetaData { get; internal set; }
    }
}
