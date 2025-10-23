using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFImage.WinUI.Common
{
    public class GifImageEventArgs : EventArgs
    {
        public object Source { get; set; }

        public GifImageEventArgs()
        {
        }
    }
}
