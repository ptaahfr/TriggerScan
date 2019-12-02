using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TriggerScan
{
    class Scanner : IDisposable
    {
        static readonly string FormatID_BMP = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
        static readonly string FormatID_JPEG = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
        static readonly int COLOR_RGB = 1;
        static readonly int WIA_IPA_DEPTH = 4104;
        static readonly int WIA_IPS_CUR_INTENT = 6146;
        static readonly int WIA_IPS_XRES = 6147;
        static readonly int WIA_IPS_YRES = 6148;
        static readonly int WIA_IPS_XPOS = 6149;
        static readonly int WIA_IPS_YPOS = 6150;
        static readonly int WIA_IPS_XEXTENT = 6151;
        static readonly int WIA_IPS_YEXTENT = 6152;

        public class LogEventArgs : EventArgs
        {
            public string Message { get; private set; }
            public LogEventArgs(string message)
            {
                Message = message;
                if (Message.EndsWith("\n") == false)
                    Message += "\r\n";
            }
        }

        public event EventHandler<LogEventArgs> LogMessage;
        public event EventHandler Finished;

        private void Log(string str)
        {
            LogMessage?.Invoke(this, new LogEventArgs(str));
        }

        object lastProductionFileNameLock_ = new object();

        string lastProductionFileName_ = string.Empty;
        public string LastProductionFileName
        {
            get
            {
                lock (lastProductionFileNameLock_) // Arbitrary private member that won't change
                {
                    return lastProductionFileName_;
                }
            }
            set
            {
                lock (lastProductionFileNameLock_)
                {
                    lastProductionFileName_ = value;
                }
            }
        }

        Semaphore semaScan_ = new Semaphore(1, 1);

        static Type WIADeviceManagerType = Type.GetTypeFromProgID("WIA.DeviceManager");
        static Type WIAImageProcessType = Type.GetTypeFromProgID("WIA.ImageProcess");

        public bool Go(string deviceName, string destPath, double dpi, double widthMM, double heightMM)
        {
            if (semaScan_.WaitOne(0) == false)
                return false;

            Task.Factory.StartNew(() =>
            {
                WIA.DeviceManager deviceManager = null;
                WIA.Device device = null;
                WIA.ImageFile imageFile = null;
                WIA.ImageFile imageFileJpeg = null;
                WIA.ImageProcess imageProcess = null;

                try
                {
                    deviceManager = (WIA.DeviceManager)Activator.CreateInstance(WIADeviceManagerType);
                    for (int i = 1; i <= deviceManager.DeviceInfos.Count; ++i)
                    {
                        if (deviceName == deviceManager.DeviceInfos[i].Properties["Name"].get_Value())
                        {
                            device = deviceManager.DeviceInfos[i].Connect();
                            break;
                        }
                    }

                    if (device != null)
                    {
                        var destFileName = Path.Combine(destPath, DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg");

                        var scanItem = device.Items[1];
                        var setProp = new Action<int, object>((name, value) => scanItem.Properties[name.ToString()].set_Value(value));

                        setProp(WIA_IPS_CUR_INTENT, COLOR_RGB);
                        setProp(WIA_IPA_DEPTH, 24);
                        setProp(WIA_IPS_XRES, dpi);
                        setProp(WIA_IPS_YRES, dpi);

                        var ratio = widthMM / heightMM;

                        if (widthMM * heightMM > 0)
                        {
                            var widthPixel = widthMM / 25.4 * dpi;
                            var heightPixel = heightMM / 25.4 * dpi;

                            if (widthPixel >= scanItem.Properties[WIA_IPS_XEXTENT.ToString()].SubTypeMax)
                            {
                                widthPixel = scanItem.Properties[WIA_IPS_XEXTENT.ToString()].SubTypeMax;
                                heightPixel = widthPixel / ratio;
                            }

                            if (heightPixel >= scanItem.Properties[WIA_IPS_YEXTENT.ToString()].SubTypeMax)
                            {
                                heightPixel = scanItem.Properties[WIA_IPS_YEXTENT.ToString()].SubTypeMax;
                                widthPixel = heightPixel * ratio;
                            }

                            setProp(WIA_IPS_XEXTENT, widthPixel);
                            setProp(WIA_IPS_YEXTENT, heightPixel);
                            setProp(WIA_IPS_XPOS, 0);
                            setProp(WIA_IPS_YPOS, 0);
                        }

                        Log($"Scanning...");

                        imageFile = ((WIA.ImageFile)device.Items[1].Transfer(FormatID_BMP));

                        imageProcess = (WIA.ImageProcess)Activator.CreateInstance(WIAImageProcessType);
                        imageProcess.Filters.Add(imageProcess.FilterInfos["Convert"].FilterID);
                        imageProcess.Filters[1].Properties["FormatID"].set_Value(FormatID_JPEG);
                        imageProcess.Filters[1].Properties["Quality"].set_Value(90);
                        imageFileJpeg = imageProcess.Apply(imageFile);
                        imageFileJpeg.SaveFile(destFileName);

                        LastProductionFileName = destFileName;

                        Log($"Scanned to {destFileName}");
                    }
                    else
                    {
                        Log($"No valid device found");
                    }
                }
                catch (Exception e)
                {
                    Log($"ERROR: {e.Message}");
#if DEBUG
                    Log($"{e.StackTrace}");
#endif
                }
                finally
                {
                    if (deviceManager != null) Marshal.FinalReleaseComObject(deviceManager);
                    if (device != null) Marshal.FinalReleaseComObject(device);
                    if (imageFile != null) Marshal.FinalReleaseComObject(imageFile);
                    if (imageProcess != null) Marshal.FinalReleaseComObject(imageProcess);
                    if (imageFileJpeg != null) Marshal.FinalReleaseComObject(imageFileJpeg);
                    semaScan_.Release();

                    Finished?.Invoke(this, EventArgs.Empty);
                }
            });

            return true;
        }

        public void Dispose()
        {
            semaScan_.WaitOne();
        }
    }
}
