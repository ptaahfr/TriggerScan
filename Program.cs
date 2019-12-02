using System.Windows.Forms;

namespace TriggerScan
{

    class Program
    {
        static void Main(string[] args)
        {
            var form = new MainForm();
            var server = new Server(form);
            form.BeforeClose += delegate { server.Dispose(); };
            Application.Run(form);
        }
    }
}
