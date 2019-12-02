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

        public void Log(string message)
        {
            syncContext_.Post(delegate
            {
                while (logListBox_.Items.Count > 256)
                    logListBox_.Items.RemoveAt(0);

                logListBox_.SelectedIndex = logListBox_.Items.Add(message);
            }, null);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
        }

        public event EventHandler BeforeClose;

        private void closeButton__Click(object sender, EventArgs e)
        {
            closeButton_.Enabled = false;
            BeforeClose?.Invoke(this, e);
            this.Dispose();
        }
    }
}
