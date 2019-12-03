using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TriggerScan
{
    public partial class MainForm : Form, ILogger
    {
        SynchronizationContext syncContext_;

        public MainForm()
        {
            InitializeComponent();
            syncContext_ = SynchronizationContext.Current;
        }

        static readonly int WM_QUERYENDSESSION = 0x11;
        bool systemShutdown_ = false;

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_QUERYENDSESSION)
            {
                systemShutdown_ = true;
            }

            base.WndProc(ref m);

        }

        public void Log(string message)
        {
            syncContext_.Post(delegate
            {
                foreach (var line in message.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    while (logListBox_.Items.Count > 256)
                        logListBox_.Items.RemoveAt(0);

                    logListBox_.SelectedIndex = logListBox_.Items.Add(line);
                }
            }, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (systemShutdown_)
            {
                systemShutdown_ = false;
                return;
            }

            if (MessageBox.Show(null, $"Are you sure you want to close {Application.ProductName} ?", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }
    }
}
