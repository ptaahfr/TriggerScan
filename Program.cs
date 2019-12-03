using System.Linq;
using System.Windows.Forms;

namespace TriggerScan
{

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "/SCAN")
            {
                Scanner.ProcessGo(args.Skip(1).ToArray());
            }
            else
            {
                var form = new MainForm();
                var server = new Server(form);
                form.Disposed += (s, e) => server.Dispose();
                Application.Run(form);
            }
        }
    }
}
