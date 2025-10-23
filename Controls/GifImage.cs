using GIFImage.WinUI.Common;
using GIFImage.WinUI.Core;
using GIFImage.WinUI.Manager;
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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.Core;
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

        /// <summary>
        /// path as "C:/xxA/xxB/test.gif"
        /// </summary>
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

        /// <summary>
        /// Identifies the <c>ClearDuration</c> attached property.
        /// </summary>
        public static readonly DependencyProperty ClearDurationProperty =
            DependencyProperty.RegisterAttached(
              nameof(ClearDuration),
              typeof(int),
              typeof(GifImage),
              new PropertyMetadata(DEFAULT_CLEAR_DURATION, ClearDurationChanged));

        /// <summary>
        /// The time interval for automatically clearing memory when not playing
        /// </summary>
        public int ClearDuration
        {
            get => (int)GetValue(ClearDurationProperty);
            set => SetValue(ClearDurationProperty, value);
        }

        #endregion

        public static readonly int DEFAULT_CLEAR_DURATION = 10 * 1000;
        public int CurrnetFrameIndex { get; private set; } = 0;

        /// <summary>
        /// Is it visible in the current window
        /// </summary>
        public bool IsVisible
        {
            get => _visible;
            internal set
            {
                if (_visible != value)
                {
                    _visible = value;
                    GifImage_VisibleChanged();
                }
            }
        }

        public event TypedEventHandler<ICanvasAnimatedControl, GifImageEventArgs> PausedChanged;

        private CanvasAnimatedControl gifControlCore;
        private IEnumerator<Frame> _frames;
        private List<Frame> _decoderFrames;
        private Frame currentFrame;
        private Debouncer debouncer = new();
        private Rect _canvasRect = Rect.Empty;
        private Windows.Foundation.Size _canvasSize = Windows.Foundation.Size.Empty;
        private int _clearDuration = DEFAULT_CLEAR_DURATION;
        private Stretch _stretch = Stretch.Uniform;
        private bool _visible;
        private TaskCompletionSource<bool> tscWaitForClear = null;

        public GifImage()
        {
            this.InitCoreControl();
            this.Content = gifControlCore;
            this.Loaded += GifImage_Loaded;
        }

        private void InitCoreControl()
        {
            gifControlCore = new()
            {
                TargetElapsedTime = TimeSpan.FromMilliseconds(1000)
            };
            gifControlCore.Draw += CanvasAnimatedControl_Draw;
            gifControlCore.Update += CanvasAnimatedControl_Update;
            gifControlCore.CreateResources += CanvasAnimatedControl_CreateResources;
        }

        private void GifImage_VisibleChanged()
        {
            if (gifControlCore is CanvasAnimatedControl control && gifControlCore?.Parent != null)
            {
                control.Paused = !IsVisible;
                PausedChanged?.Invoke(gifControlCore, new GifImageEventArgs() { Source = this });
                this.GifImage_PausedChanged(!IsVisible);
            }
        }

        private void GifImage_PausedChanged(bool paused)
        {
            if (paused)
            {
                StartToWaitClearEventAsync();
            }
            else
            {
                NotifyNotClear();
            }
        }

        /// <summary>
        /// If the GIF is not displayed for more than <see cref="ClearDuration"/> seconds, release the decoded memory directly
        /// </summary>
        /// <returns></returns>
        private async Task StartToWaitClearEventAsync()
        {
            if (tscWaitForClear == null || tscWaitForClear.Task.IsCompleted)
            {
                using (var cts = new CancellationTokenSource(_clearDuration))
                {
                    tscWaitForClear = new TaskCompletionSource<bool>();
                    cts.Token.Register(() => tscWaitForClear.TrySetCanceled(), false);
                    try
                    {
                        await tscWaitForClear.Task;
                    }
                    catch (TaskCanceledException ex)
                    {
                        this.Release();
                    }
                }
            }
        }

        private void NotifyNotClear()
        {
            tscWaitForClear?.TrySetResult(true);
            if (_frames == null && _decoderFrames == null)
            {
                this.CreateResourcesAsync();
            }
        }

        private void GifImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is GifImage gif)
            {
                VisibleManager.Instance.Add(gif);
                VisibleManager.Instance.StartMonitor(DispatcherQueue);
                _canvasSize = new Windows.Foundation.Size(this.ActualWidth, this.ActualHeight);

            }
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

        private static void SourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var control = o as GifImage;
            if (control != null)
            {
                control.gifControlCore.Paused = true;
                control.CreateResourcesAsync();
                control.gifControlCore.Paused = false;
            }
        }

        private static void StretchChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var control = o as GifImage;
            if (control != null)
            {
                control._stretch = (Stretch)e.NewValue;
                control._canvasRect = Rect.Empty;
            }
        }

        private static void ClearDurationChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var control = o as GifImage;
            if (control != null)
            {
                control._clearDuration = (int)e.NewValue;
            }
        }

        private void CanvasAnimatedControl_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
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

        private void CanvasAnimatedControl_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
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
        private void CanvasAnimatedControl_CreateResources(CanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            CreateResourcesAsync();
        }

        private void CreateResourcesAsync()
        {
            if (_frames == null && gifControlCore?.Parent != null && this._visible)
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

        private void Release()
        {
            this._frames?.Dispose();
            this._frames = null;
            this._decoderFrames?.Clear();
            this._decoderFrames = null;
            currentFrame = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
