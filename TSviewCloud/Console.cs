using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using TSviewCloudPlugin;

namespace TSviewCloud
{
    class ConsoleFunc
    {
        public static bool IsOutputRedirected = false;

        public static int MainFunc(string[] args)
        {
            bool inputRedirected = IsRedirected(GetStdHandle(StandardHandle.Input));
            if (inputRedirected)
            {
                TSviewCloudConfig.Config.MasterPassword = Console.ReadLine();
            }

            bool outputRedirected = IsRedirected(GetStdHandle(StandardHandle.Output));
            Stream initialOut = null;
            if (outputRedirected)
            {
                initialOut = Console.OpenStandardOutput();
                IsOutputRedirected = true;
            }

            bool errorRedirected = IsRedirected(GetStdHandle(StandardHandle.Error));
            Stream initialError = null;
            if (errorRedirected)
            {
                initialError = Console.OpenStandardError();
            }

            if (!AttachConsole(-1))
                AllocConsole();

            int codepage = GetConsoleOutputCP();
            if (outputRedirected)
            {
                Console.SetOut(new StreamWriter(initialOut, Encoding.GetEncoding(codepage)));
            }
            else
            {
                Console.OutputEncoding = Encoding.GetEncoding(codepage);
            }

            if (errorRedirected)
            {
                Console.SetError(new StreamWriter(initialError, Encoding.GetEncoding(codepage)));
            }
            else
            {
                SetStdHandle(StandardHandle.Error, GetStdHandle(StandardHandle.Output));
            }

            Console.Error.WriteLine("");

            Console.CancelKeyPress += new ConsoleCancelEventHandler(CtrlC_Handler);

            var paramArgsList = new List<string>();
            var targetArgsList = new List<string>();
            bool skipparam = false;
            foreach (var p in args)
            {
                if (skipparam) targetArgsList.Add(p);
                else
                {
                    if (p == "-")
                    {
                        skipparam = true;
                        continue;
                    }
                    if (p.StartsWith("--")) paramArgsList.Add(p.Substring(2));
                    else targetArgsList.Add(p);
                }
            }
            if (targetArgsList.Count == 0)
                targetArgsList.Add("help");

            var paramArgs = paramArgsList.ToArray();
            var targetArgs = targetArgsList.ToArray();

            foreach (var p in paramArgs)
            {
                switch (p)
                {
                    case "debug":
                        Console.Error.WriteLine("(--debug: debug output mode)");
                        TSviewCloudConfig.Config.debug = true;
                        break;
                }
            }

            switch (targetArgs[0])
            {
                case "help":
                    Console.WriteLine("usage");
                    Console.WriteLine("\thelp                                      : show help");
                    Console.WriteLine("\tlist (REMOTE_PATH)                        : list item");
                    Console.WriteLine("\t\t--recursive: recursive mode");
                    Console.WriteLine("\t\t--hash: show hash");
                    break;
                case "list":
                    return RunList(targetArgs, paramArgs);
                case "download":
                    return RunDownload(targetArgs, paramArgs);
            }
            return 0;
        }



        static int RunList(string[] targetArgs, string[] paramArgs)
        {
            var job = JobControler.CreateNewJob(JobClass.ControlMaster);
            job.DisplayName = "ListItem";
            JobControler.Run(job, (j) =>
            {
                string remotepath = null;
                IEnumerable<IRemoteItem> target = null;

                if (targetArgs.Length > 1)
                {
                    remotepath = targetArgs[1];
                    remotepath = remotepath.Replace('\\', '/');
                }

                bool recursive = false;
                bool showhash = false;
                foreach (var p in paramArgs)
                {
                    switch (p)
                    {
                        case "recursive":
                            Console.Error.WriteLine("(--recursive: recursive mode)");
                            recursive = true;
                            break;
                        case "hash":
                            Console.Error.WriteLine("(--hash: show hash)");
                            showhash = true;
                            break;
                    }
                }

                try
                {
                    var j2 = InitServer(j);
                    j2.Wait(ct: j.Ct);

                    target = FindItems(remotepath, recursive: recursive, ct: j.Ct);

                    if (target.Count() < 1)
                    {
                        j.Result = 2;
                        return;
                    }

                    if (remotepath.Contains("**"))
                        recursive = true;

                    Console.Error.WriteLine("Found : " + target.Count());
                    foreach (var item in target.OrderBy(x => x.FullPath))
                    {
                        string detail = "";
                        if (showhash) detail = "\t" + item.Hash;

                        if (recursive)
                            Console.WriteLine(item.FullPath + detail);
                        else
                        {
                            if (item.IsRoot)
                                Console.WriteLine(item.FullPath + detail);
                            else
                                Console.WriteLine(item.Name + ((item.ItemType == RemoteItemType.Folder) ? "/" : "") + detail);
                        }
                    }

                    job.Result = 0;
                }
                catch (OperationCanceledException)
                {
                    job.Result = -1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.ToString());
                    job.Result = 1;
                }
            });
            try
            {
                job.Wait(ct: job.Ct);
            }
            catch (OperationCanceledException)
            {
            }
            TSviewCloudConfig.Config.ApplicationExit = true;
            Console.Out.Flush();
            return (job.Result as int?) ?? -1;
        }



        private static int RunDownload(string [] targetArgs, string[] paramArgs)
        {
            string remotePath;
            string localPath;
            if(targetArgs.Length < 1)
            {

            }
 
            return 0;
        }

        class RemoteItemEqualityComparer : IEqualityComparer<IRemoteItem>
        {
            public bool Equals(IRemoteItem x, IRemoteItem y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                return x.FullPath == y.FullPath;
            }

            public int GetHashCode(IRemoteItem p)
              => p.FullPath.GetHashCode();
        }

        static IEnumerable<IRemoteItem> FindItems(string path_str, bool recursive = false, IRemoteItem root = null, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();
            List<IRemoteItem> ret = new List<IRemoteItem>();
            if (string.IsNullOrEmpty(path_str))
            {
                if (root == null)
                {
                    if (recursive)
                    {
                        ret.AddRange(RemoteServerFactory.ServerList.Values.Select(x => x[""]).Select(x => FindItems("", true, x, ct)).SelectMany(x => x));
                        return ret;
                    }
                    else
                    {
                        ret.AddRange(RemoteServerFactory.ServerList.Values.Select(x => x[""]));
                        return ret;
                    }
                }
                else
                {
                    root = RemoteServerFactory.PathToItem(root.FullPath, ReloadType.Reload);
                    ret.Add(root);
                    var children = root.Children;
                    if (recursive)
                    {
                        ret.AddRange(children.Where(x => x.ItemType == RemoteItemType.File));
                        ret.AddRange(children.Where(x => x.ItemType == RemoteItemType.Folder).Select(x => FindItems("", true, x, ct)).SelectMany(x => x));
                    }
                    return ret;
                }
            }

            var m = Regex.Match(path_str, @"^(?<server>[^:]+)(://)(?<path>.*)$");
            if (m.Success)
            {
                var servername = m.Groups["server"].Value;
                if (servername.IndexOfAny(new[] { '?', '*' }) < 0)
                {
                    var server = RemoteServerFactory.ServerList[servername];
                    return FindItems(m.Groups["path"].Value, recursive, server[""], ct);
                }
                else
                {
                    var servers = RemoteServerFactory.ServerList.Keys;
                    return servers.Where(x => Regex.IsMatch(x, "^" + Regex.Escape(servername).Replace("\\*", ".*").Replace("\\?", ".") + "$"))
                        .Select(x => FindItems(m.Groups["path"].Value, recursive, RemoteServerFactory.ServerList[x][""], ct))
                        .SelectMany(x => x);
                }
            }
            else
            {
                if (root == null) return ret;

                m = Regex.Match(path_str, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                path_str = m.Groups["next"].Value;

                root = RemoteServerFactory.PathToItem(root.FullPath, ReloadType.Reload);
                var children = root.Children;

                var itemname = m.Groups["current"].Value;
                if(itemname == "**")
                {
                    ret.AddRange(FindItems(path_str, true, root, ct));
                    ret.AddRange(children
                        .Select(x => FindItems(path_str, true, x, ct))
                        .SelectMany(x => x));
                    ret.AddRange(children
                        .Select(x => FindItems("**/"+path_str, true, x, ct))
                        .SelectMany(x => x));
                    return ret.Distinct(new RemoteItemEqualityComparer());
                }
                else if(itemname.IndexOfAny(new[] { '?', '*' }) < 0)
                {
                    return children.Where(x => x.Name == itemname)
                        .Select(x => FindItems(path_str, recursive, x, ct))
                        .SelectMany(x => x);
                }
                else
                {
                    return children.Where(x => Regex.IsMatch(x.Name, "^"+Regex.Escape(itemname).Replace("\\*", ".*").Replace("\\?", ".")+"$"))
                        .Select(x => FindItems(path_str, recursive, x, ct))
                        .SelectMany(x => x);
                }
            }
        }

        private static Job InitServer(Job master)
        {
            var loadJob = JobControler.CreateNewJob(JobClass.Normal, depends: master);
            loadJob.WeekDepend = true;
            loadJob.DisplayName = "Load server list";
            JobControler.Run(loadJob, (j) =>
            {
                j.Progress = -1;
                j.ProgressStr = "Loading...";
                RemoteServerFactory.Restore();

                while (!RemoteServerFactory.ServerList.Values.All(x => x.IsReady))
                {
                    loadJob.Ct.ThrowIfCancellationRequested();
                    Task.Delay(500).Wait(loadJob.Ct);
                }

                j.Progress = 1;
                j.ProgressStr = "Done.";
            });
            return loadJob;
        }

        static void CheckMasterPassword()
        {
            if (!TSviewCloudConfig.Config.IsMasterPasswordCorrect)
            {
                Thread t = new Thread(new ThreadStart(() =>
                {
                    using (var f = new FormMasterPass())
                    {
                        f.ShowDialog();
                    }
                }));
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                t.Join();

                if (!TSviewCloudConfig.Config.IsMasterPasswordCorrect)
                {
                    Console.Error.Write("Master Password Incorrect.");
                    Environment.Exit(1);
                }
            }
        }

        async protected static void CtrlC_Handler(object sender, ConsoleCancelEventArgs args)
        {
            Console.Error.WriteLine("");
            Console.Error.WriteLine("Cancel...");
            TSviewCloudPlugin.JobControler.CancelAll();
            args.Cancel = true;
            await Task.Run(() =>
            {
                while (!TSviewCloudPlugin.JobControler.IsEmpty)
                    Thread.Sleep(100);
            }).ConfigureAwait(false);
        }


        ///////////////////////////////////////////////////////////////////////////////////
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetConsoleOutputCP();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(StandardHandle nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetStdHandle(StandardHandle nStdHandle, IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern FileType GetFileType(IntPtr handle);

        private enum StandardHandle : uint
        {
            Input = unchecked((uint)-10),
            Output = unchecked((uint)-11),
            Error = unchecked((uint)-12)
        }

        private enum FileType : uint
        {
            Unknown = 0x0000,
            Disk = 0x0001,
            Char = 0x0002,
            Pipe = 0x0003
        }

        private static bool IsRedirected(IntPtr handle)
        {
            FileType fileType = GetFileType(handle);

            return (fileType == FileType.Disk) || (fileType == FileType.Pipe);
        }
    }
}
