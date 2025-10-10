using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GIFImage.WinUI.Utillits
{
    public class Debouncer
    {
        private Timer? _timer;
        private Action? _action;
        private bool _abort = false;

        public Debouncer() { }

        public void Debounce(Action action, int delay)
        {
            _abort = false;
            _action = action;
            _timer?.Dispose();
            _timer = new Timer(_ => run(), null, TimeSpan.FromMilliseconds(delay), Timeout.InfiniteTimeSpan);
        }

        private void run()
        {
            _action?.Invoke();
            _timer?.Dispose();
        }

        public void Abort()
        {
            _abort = true;
            _timer?.Dispose();
        }
    }
}
