using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSviewCloud
{
    static class Program
    {
        public static Form MainForm;
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static int Main(string[] args)
       {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Trace.Listeners.Add(new TextWriterTraceListener(System.IO.Path.Combine(TSviewCloudConfig.Config.Config_BasePath, System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".err.log")));
            Trace.AutoFlush = true;

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Trace.WriteLine(e.Exception);
            };


            if (args.Length == 0)
            {
                MainForm = new Form1();
                Application.Run(MainForm);
                return 0;
            }
            else
            {
                var ret = ConsoleFunc.MainFunc(args);
                Console.Error.WriteLine(ret.ToString());
                return ret;
            }
        }
    }
}
