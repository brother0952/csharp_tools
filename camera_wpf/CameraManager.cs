using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using DirectShowLib;

namespace CameraTool
{
    public class CameraManager : IDisposable
    {
        private IFilterGraph2 graphBuilder;
        private IMediaControl mediaControl;
        private IBaseFilter videoDevice;
        private ISampleGrabber sampleGrabber;
        private IBaseFilter grabberFilter;
        private IPin pinOut;
        private List<DsDevice> videoDevices;
        private VideoInfoHeader videoInfo;
        private int frameCount;
        private DateTime lastFPSTime = DateTime.Now;
        private float currentFPS;
        private bool isInitialized = false;

        public event Action<BitmapSource> NewFrame;
        public event Action<float> FPSUpdated;

        public CameraManager()
        {
            try
            {
                InitializeGraph();
                EnumerateVideoDevices();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化摄像头管理器失败: {ex.Message}");
                throw;
            }
        }

        private void InitializeGraph()
        {
            try
            {
                graphBuilder = (IFilterGraph2)new FilterGraph();
                mediaControl = (IMediaControl)graphBuilder;
                sampleGrabber = (ISampleGrabber)new SampleGrabber();
                grabberFilter = (IBaseFilter)sampleGrabber;

                var mt = new AMMediaType
                {
                    majorType = MediaType.Video,
                    subType = MediaSubType.RGB24,
                    formatType = FormatType.VideoInfo
                };

                int hr = sampleGrabber.SetMediaType(mt);
                DsError.ThrowExceptionForHR(hr);

                hr = sampleGrabber.SetBufferSamples(true);
                DsError.ThrowExceptionForHR(hr);

                hr = sampleGrabber.SetCallback(new SampleGrabberCallback(this), 1);
                DsError.ThrowExceptionForHR(hr);

                DsUtils.FreeAMMediaType(mt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化图形失败: {ex.Message}");
                throw;
            }
        }

        public List<string> GetDeviceNames()
        {
            var names = new List<string>();
            foreach (var device in videoDevices)
            {
                names.Add(device.Name);
            }
            return names;
        }

        private void EnumerateVideoDevices()
        {
            videoDevices = new List<DsDevice>(DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice));
        }

        public void SelectDevice(int index)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("摄像头管理器未正确初始化");
            }

            if (index < 0 || index >= videoDevices.Count)
            {
                throw new ArgumentException("无效的设备索引");
            }

            try
            {
                StopCamera();

                var device = videoDevices[index];
                videoDevice = CreateFilter(device);

                if (videoDevice == null)
                {
                    throw new InvalidOperationException("无法创建视频设备过滤器");
                }

                ConnectFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择设备失败: {ex.Message}");
                throw;
            }
        }

        private IBaseFilter CreateFilter(DsDevice device)
        {
            var filterGraph = graphBuilder as IFilterGraph2;
            int hr = filterGraph.AddSourceFilterForMoniker(device.Mon, null, device.Name, out IBaseFilter filter);
            DsError.ThrowExceptionForHR(hr);
            return filter;
        }

        private void ConnectFilters()
        {
            try
            {
                // 清理现有连接
                if (mediaControl != null)
                {
                    mediaControl.Stop();
                }

                // 移除所有现有过滤器
                RemoveAllFilters();

                // 添加过滤器
                int hr = graphBuilder.AddFilter(videoDevice, "Video Capture");
                DsError.ThrowExceptionForHR(hr);

                hr = graphBuilder.AddFilter(grabberFilter, "Grabber");
                DsError.ThrowExceptionForHR(hr);

                // 获取并连接引脚
                pinOut = DsFindPin.ByDirection(videoDevice, PinDirection.Output, 0);
                var pinIn = DsFindPin.ByDirection(grabberFilter, PinDirection.Input, 0);

                if (pinOut == null || pinIn == null)
                {
                    throw new InvalidOperationException("无法获取视频设备引脚");
                }

                hr = graphBuilder.Connect(pinOut, pinIn);
                DsError.ThrowExceptionForHR(hr);

                // 获取视频格式
                var mt = new AMMediaType();
                hr = pinOut.ConnectionMediaType(mt);
                DsError.ThrowExceptionForHR(hr);

                if (mt.formatType == FormatType.VideoInfo)
                {
                    videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(mt.formatPtr, typeof(VideoInfoHeader));
                    System.Diagnostics.Debug.WriteLine($"视频大小: {videoInfo.BmiHeader.Width}x{videoInfo.BmiHeader.Height}");
                }
                DsUtils.FreeAMMediaType(mt);

                System.Diagnostics.Debug.WriteLine("摄像头连接成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"连接摄像头时出错: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void RemoveAllFilters()
        {
            IEnumFilters enumFilters;
            graphBuilder.EnumFilters(out enumFilters);
            
            var filters = new IBaseFilter[1];
            IntPtr fetched = IntPtr.Zero;
            while (enumFilters.Next(1, filters, fetched) == 0)
            {
                graphBuilder.RemoveFilter(filters[0]);
                Marshal.ReleaseComObject(filters[0]);
            }
            Marshal.ReleaseComObject(enumFilters);
        }

        public void StartCamera()
        {
            try
            {
                if (mediaControl != null)
                {
                    int hr = mediaControl.Run();
                    DsError.ThrowExceptionForHR(hr);
                    System.Diagnostics.Debug.WriteLine("摄像头已启动");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动摄像头失败: {ex.Message}");
                throw;
            }
        }

        public void StopCamera()
        {
            try
            {
                if (mediaControl != null)
                {
                    mediaControl.Stop();
                    System.Diagnostics.Debug.WriteLine("摄像头已停止");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止摄像头失败: {ex.Message}");
            }
        }

        public void SetParameter(CameraParameter parameter, int value)
        {
            if (videoDevice == null) return;

            var properties = videoDevice as IAMVideoProcAmp;
            if (properties != null)
            {
                properties.Set((VideoProcAmpProperty)parameter, value, VideoProcAmpFlags.Manual);
            }
        }

        private class SampleGrabberCallback : ISampleGrabberCB
        {
            private readonly CameraManager parent;
            private readonly object lockObj = new object();

            public SampleGrabberCallback(CameraManager parent)
            {
                this.parent = parent;
            }

            public int SampleCB(double sampleTime, IMediaSample pSample)
            {
                return 0;
            }

            public int BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
            {
                lock (lockObj)
                {
                    try
                    {
                        if (parent.videoInfo == null)
                        {
                            System.Diagnostics.Debug.WriteLine("videoInfo 为空");
                            return 0;
                        }

                        parent.frameCount++;
                        var now = DateTime.Now;
                        var timeDiff = (now - parent.lastFPSTime).TotalSeconds;
                        if (timeDiff >= 1)
                        {
                            parent.currentFPS = parent.frameCount / (float)timeDiff;
                            parent.frameCount = 0;
                            parent.lastFPSTime = now;
                            parent.FPSUpdated?.Invoke(parent.currentFPS);
                        }

                        int width = parent.videoInfo.BmiHeader.Width;
                        int height = parent.videoInfo.BmiHeader.Height;
                        int stride = ((width * 3 + 3) / 4) * 4; // 确保每行是4字节对齐

                        using (var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                        {
                            var bitmapData = bitmap.LockBits(
                                new Rectangle(0, 0, width, height),
                                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                            try
                            {
                                // 逐行复制图像数据，并进行垂直翻转
                                byte[] buffer = new byte[stride];
                                for (int y = 0; y < height; y++)
                                {
                                    // 源数据是从下到上的，需要翻转
                                    IntPtr srcPtr = pBuffer + (height - 1 - y) * stride;
                                    IntPtr dstPtr = bitmapData.Scan0 + y * bitmapData.Stride;
                                    
                                    // 直接复制内存数据
                                    Marshal.Copy(srcPtr, buffer, 0, stride);
                                    Marshal.Copy(buffer, 0, dstPtr, stride);
                                }
                            }
                            finally
                            {
                                bitmap.UnlockBits(bitmapData);
                            }

                            // 转换为 WPF 图像
                            IntPtr hBitmap = bitmap.GetHbitmap();
                            try
                            {
                                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                bitmapSource.Freeze();

                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    parent.NewFrame?.Invoke(bitmapSource);
                                }));
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"处理帧: {width}x{height}, 步长: {stride}, 缓冲区大小: {bufferLen}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"处理图像时出错: {ex.Message}\n{ex.StackTrace}");
                    }
                    return 0;
                }
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public void Dispose()
        {
            StopCamera();

            if (mediaControl != null)
            {
                Marshal.ReleaseComObject(mediaControl);
                mediaControl = null;
            }

            if (graphBuilder != null)
            {
                Marshal.ReleaseComObject(graphBuilder);
                graphBuilder = null;
            }

            if (videoDevice != null)
            {
                Marshal.ReleaseComObject(videoDevice);
                videoDevice = null;
            }

            if (sampleGrabber != null)
            {
                Marshal.ReleaseComObject(sampleGrabber);
                sampleGrabber = null;
            }

            if (grabberFilter != null)
            {
                Marshal.ReleaseComObject(grabberFilter);
                grabberFilter = null;
            }

            if (pinOut != null)
            {
                Marshal.ReleaseComObject(pinOut);
                pinOut = null;
            }
        }
    }

    public enum CameraParameter
    {
        Brightness = 0,
        Contrast = 1,
        Saturation = 2
    }
} 