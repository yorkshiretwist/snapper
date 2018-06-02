using snapper.core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace snapper.app
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Start();
        }

        private void Start()
        {
            WindowState = FormWindowState.Minimized;
            Hide();

            var config = new ProcessorConfig
            {
                PauseSeconds = int.Parse(ConfigurationManager.AppSettings["PauseSeconds"]),
                RootFolderPath = ConfigurationManager.AppSettings["RootFolderPath"],
                UsernamePattern = ConfigurationManager.AppSettings["UsernamePattern"],
                KeystrokeDelayMilliseconds = int.Parse(ConfigurationManager.AppSettings["KeystrokeDelayMilliseconds"])
            };
            var processor = new Processor();
            Task.Run(() => processor.Start(config));

            var rand = new Random();
            while(true)
            {
                while (progressBar1.Value < 100)
                {
                    var newValue = rand.Next(progressBar1.Value, progressBar1.Value + 20);
                    if (newValue > 100)
                    {
                        newValue = 100;
                    }
                    progressBar1.Increment(newValue);
                    Thread.Sleep(150);
                }
                Thread.Sleep(rand.Next(4, 10) * 1000);
            }
        }

        private void Form1_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
