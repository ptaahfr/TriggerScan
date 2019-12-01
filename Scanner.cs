using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TriggerScan
{
    class Scanner
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


        public static string Go(string deviceName, string destPath, double dpi, double widthMM, double heightMM)
        {
            string status = "";
            var log = new Action<string>((str) =>
            {
                status += str + "\r\n";
                Console.WriteLine(str);
            });

            try
            {
                WIA.DeviceManager manager = (WIA.DeviceManager)Activator.CreateInstance(Type.GetTypeFromProgID("WIA.DeviceManager"));
                WIA.Device device = null;

                for (int i = 1; i <= manager.DeviceInfos.Count; ++i)
                {
                    if (deviceName == manager.DeviceInfos[i].Properties["Name"].get_Value())
                    {
                        device = manager.DeviceInfos[i].Connect();
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

                    var image = ((WIA.ImageFile)device.Items[1].Transfer(FormatID_BMP));
                    log($"Scanned to {destFileName}");

                    var imgProc = (WIA.ImageProcess)Activator.CreateInstance(Type.GetTypeFromProgID("WIA.ImageProcess"));
                    imgProc.Filters.Add(imgProc.FilterInfos["Convert"].FilterID);
                    imgProc.Filters[1].Properties["FormatID"].set_Value(FormatID_JPEG);
                    imgProc.Filters[1].Properties["Quality"].set_Value(90);
                    imgProc.Apply(image).SaveFile(destFileName);
                }
            }
            catch (Exception e)
            {
                log($"ERROR: {e.Message}");
#if DEBUG
                log($"{e.StackTrace}");
#endif
            }
            return status;
        }
    }
}
