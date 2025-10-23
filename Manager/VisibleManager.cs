using GIFImage.WinUI.Common;
using GIFImage.WinUI.Controls;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.Foundation;
using WinRT.Interop;

namespace GIFImage.WinUI.Manager
{
    internal class VisibleManager
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool IsIconic(IntPtr hWnd);

        private static VisibleManager instance;
        private static object _instanceLock = new object();
        private static object _loopLock = new object();
        private CancellationTokenSource cts = null;
        private UIElement _rootElement;
        private Rect _rootRect;
        private HashSet<UIElement> _queue;
        private nint? _handle = null;

        public int Interval { get; set; } = 100;
        private UIElement RootElement
        {
            get => _rootElement;
            set
            {
                _rootElement = value;
                _rootRect = new Rect(0, 0, _rootElement.ActualSize.X, _rootElement.ActualSize.Y);
            }
        }

        private VisibleManager()
        {
        }

        public static VisibleManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (_instanceLock)
                    {
                        instance ??= new VisibleManager();
                    }
                }
                return instance;
            }
        }

        private IEnumerable<UIElement> FindElementsInHostCoordinates()
        {
            try
            {
                if (RootElement is UIElement root)
                {
                    return VisualTreeHelper.FindElementsInHostCoordinates(_rootRect, root);
                }
            }
            catch { }
            return null;
        }
        private DependencyObject FindVisualRoot(DependencyObject parent)
        {
            var p = VisualTreeHelper.GetParent(parent);
            if (p != null)
                return FindVisualRoot(p);
            return parent;
        }

        public void Add(UIElement element)
        {
            _queue ??= new();
            if (RootElement == null)
            {
                lock (_instanceLock)
                {
                    RootElement ??= FindVisualRoot(element) as UIElement;
                }
            }
            _queue.Add(element);
            var allVisualControls = FindElementsInHostCoordinates();
            CheckVisible(allVisualControls, element);
        }

        public bool TryRemove(UIElement element)
        {
            bool? res = null;
            res = _queue?.Remove(element);
            return res.HasValue && res.Value;
        }

        public void StartMonitor(DispatcherQueue dispatcherQueue)
        {
            if (cts == null)
            {
                lock (_loopLock)
                {
                    cts = new CancellationTokenSource();
                    _handle ??= Process.GetCurrentProcess().MainWindowHandle;
                    Task.Run(async () =>
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            dispatcherQueue.TryEnqueue(async () =>
                            {
                                var allVisualControls = FindElementsInHostCoordinates();
                                if (cts.Token.IsCancellationRequested)
                                    return;
                                var queue = _queue?.ToList();
                                if (queue != null)
                                {
                                    if (!IsIconic(_handle.Value))
                                    {
                                        foreach (var item in queue)
                                        {
                                            if (item != null)
                                            {
                                                CheckVisible(allVisualControls, item);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (var item in queue)
                                        {
                                            if (item is GifImage gif)
                                                gif.IsVisible = false;
                                        }
                                    }
                                }
                            });
                            await Task.Delay(Interval);
                        }
                    });
                }
            }
        }

        private void CheckVisible(IEnumerable<UIElement> allVisualControls, UIElement item)
        {
            //Determine if it is visible within the window, if not, pause rendering
            //Scenario 1:
            //Scroll out of the window
            //Scenario 2:
            //Switch to another TabViewItem
            if (item is GifImage gif && allVisualControls != null)
            {
                var isContains = allVisualControls.Contains(item);
                gif.IsVisible = isContains;
            }
        }

        public void Stop()
        {
            cts?.Cancel();
            cts = null;
        }
    }
}
