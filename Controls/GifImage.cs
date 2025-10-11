using GIFImage.WinUI.Core;
using GIFImage.WinUI.Utillits;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Frame = GIFImage.WinUI.Core.Frame;

namespace GIFImage.WinUI.Controls
{
    public sealed class GifImage : UserControl
    {
        #region dp
        /// <summary>
        /// Identifies the <c>Source</c> attached property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached(
              nameof(Source),
              typeof(string),
              typeof(GifImage),
              new PropertyMetadata(null, SourceChanged));

        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Identifies the <c>Stretch</c> attached property.
        /// </summary>
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.RegisterAttached(
              nameof(Stretch),
              typeof(Stretch),
              typeof(GifImage),
              new PropertyMetadata(Stretch.Uniform, StretchChanged));


        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        #endregion

        private static DependencyObject _rootUIElement = null;
        public int CurrnetFrameIndex { get; private set; } = 0;

        /// <summary>
        /// Is it visible in the current window
        /// </summary>
        public bool IsVisible { get; private set; }

        private CanvasAnimatedControl gifControlCore;
        private IEnumerator<Frame> _frames;
        private List<Frame> _decoderFrames;
        private Frame currentFrame;
        private Debouncer debouncer = new();
        private Rect _canvasRect = Rect.Empty;
        private Windows.Foundation.Size _canvasSize = Windows.Foundation.Size.Empty;
        private static object _lockObject = new object();
        private Stretch _stretch = Stretch.Uniform;

        public GifImage()
        {
            gifControlCore = new()
            {
                TargetElapsedTime = TimeSpan.FromMilliseconds(1000)
            };
            if (gifControlCore != null)
            {
                gifControlCore.Draw += CanvasAnimatedControl_Draw;
                gifControlCore.Update += CanvasAnimatedControl_Update;
                gifControlCore.CreateResources += CanvasAnimatedControl_CreateResources;
                gifControlCore.LayoutUpdated += GifControlCore_LayoutUpdated;
            }
            this.Content = gifControlCore;
            this.Loaded += GifImage_Loaded;
        }

        private void GifControlCore_LayoutUpdated(object sender, object e)
        {
            debouncer?.Debounce(() =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (gifControlCore is CanvasAnimatedControl control && _rootUIElement is UIElement root)
                    {
                        //Determine if it is visible within the window, if not, pause rendering
                        //Scenario 1:
                        //Scroll out of the window
                        //Scenario 2:
                        //Switch to another TabViewItem
                        var rootRect = new Rect(0, 0, root.ActualSize.X, root.ActualSize.Y);
                        var allVisualControls = VisualTreeHelper.FindElementsInHostCoordinates(rootRect, root);
                        var isContains = allVisualControls.Contains(control);
                        control.Paused = !isContains;
                        this.IsVisible = isContains;
                    }
                });
            }, 200);
        }

        private void GifImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is GifImage gif)
            {
                if (_rootUIElement == null)
                {
                    lock (_lockObject)
                    {
                        _rootUIElement ??= FindVisualRoot(gif);
                    }
                }
                _canvasSize = new Windows.Foundation.Size(this.ActualWidth, this.ActualHeight);
            }
        }

        private DependencyObject FindVisualRoot(DependencyObject parent)
        {
            var p = VisualTreeHelper.GetParent(parent);
            if (p != null)
                return FindVisualRoot(p);
            return parent;
        }


        private void CalculateRect()
        {
            if (_canvasRect == Rect.Empty && currentFrame != null)
            {
                var imageWidth = currentFrame.Width;
                var imageHeight = currentFrame.Height;
                var size = Utils.CalculateSize(imageWidth, imageHeight, _canvasSize.Width, _canvasSize.Height, _stretch);
                _canvasRect = _stretch switch
                {
                    Stretch.Fill => new Rect(0, 0, _canvasSize.Width, _canvasSize.Height),
                    Stretch.Uniform => new Rect(Math.Abs(_canvasSize.Width - size.width) / 2, Math.Abs(_canvasSize.Height - size.height) / 2, size.width, size.height),
                    Stretch.UniformToFill => new Rect(0, 0, size.width, size.height),
                    _ => new Rect(0, 0, _canvasSize.Width, _canvasSize.Height)
                };
            }
        }

        private static async void SourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var control = o as GifImage;
            if (control != null)
            {
                control.gifControlCore.Paused = true;
                await control.CreateResourcesAsync();
                control.gifControlCore.Paused = false;
            }
        }

        private static async void StretchChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var control = o as GifImage;
            if (control != null)
            {
                control._stretch = (Stretch)e.NewValue;
                control._canvasRect = Rect.Empty;
            }
        }

        private async void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            if (currentFrame != null)
            {
                using (var image = ConvertByteToCanvasBitmap(currentFrame.MetaData, currentFrame.Width, currentFrame.Height))
                {
                    CalculateRect();
                    args.DrawingSession.DrawImage(image, this._canvasRect);
                }
            }
        }

        private async void CanvasAnimatedControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            if (_frames != null)
            {
                if (_frames.MoveNext())
                {
                    currentFrame = _frames.Current;
                    _decoderFrames ??= new();
                    _decoderFrames.Add(currentFrame);
                }
                else
                {
                    _frames = null;
                }
            }
            else if (_decoderFrames.IsNotEmpty())
            {
                CurrnetFrameIndex = (CurrnetFrameIndex + 1) % _decoderFrames.Count;
                currentFrame = _decoderFrames[CurrnetFrameIndex];
            }
            SetTargetElapsedTime();
        }
        private async void CanvasAnimatedControl_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            await CreateResourcesAsync();
        }

        private async Task CreateResourcesAsync()
        {
            if (_frames == null)
            {
                _frames = DecodeGif(this.Source).GetEnumerator();
                CurrnetFrameIndex = 0;
            }
        }

        private void SetTargetElapsedTime()
        {
            if (currentFrame != null)
            {
                CheckFrameDelay(currentFrame);
                var delay = currentFrame.Delay;
                gifControlCore.TargetElapsedTime = TimeSpan.FromMilliseconds(delay);
            }
        }

        private async Task<CanvasBitmap> ConvertBitmapToCanvasBitmap(Bitmap bitmap)
        {
            using (var memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                var randomAccessStream = memoryStream.AsRandomAccessStream();
                return await CanvasBitmap.LoadAsync(gifControlCore, randomAccessStream);
            }
        }

        private CanvasBitmap ConvertByteToCanvasBitmap(byte[] bytes, int width, int height)
        {
            return CanvasBitmap.CreateFromBytes(gifControlCore, bytes, width, height, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
        }

        private IEnumerable<Frame> DecodeGif(string gifPath)
        {
            var decoder = new GifDecoder();
            return decoder.ReadAsync(gifPath);
        }

        private void CheckFrameDelay(Frame images)
        {
            if (images != null && images.Delay == 0)
                images.Delay = 100; // 0.1S per frame
        }

        ~GifImage()
        {
            if (gifControlCore != null)
            {
                gifControlCore.Draw -= CanvasAnimatedControl_Draw;
                gifControlCore.Update -= CanvasAnimatedControl_Update;
                gifControlCore.CreateResources -= CanvasAnimatedControl_CreateResources;
            }
            debouncer?.Abort();
            debouncer = null;
        }
    }
}
