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
                    Console.WriteLine("\tdownload (remotepath) (localpath)         : download item(s)");
                    break;
                case "list":
                    return RunList(targetArgs, paramArgs);
                case "download":
                    return RunDownload(targetArgs, paramArgs);
                case "upload":
                    return RunUpload(targetArgs, paramArgs);
            }
            return 0;
        }



        static int RunList(string[] targetArgs, string[] paramArgs)
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

            var job = JobControler.CreateNewJob(JobClass.ControlMaster);
            job.DisplayName = "ListItem";
            JobControler.Run(job, (j) =>
            {
                try
                {
                    var j2 = InitServer(j);
                    j2.Wait(ct: j.Ct);

                    target = FindItems(remotepath, recursive: recursive, ct: j.Ct);

                    if (target.Count() < 1)
                    {
                        j.ResultAsObject = 2;
                        return;
                    }

                    if (remotepath?.Contains("**") ?? true)
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

                    job.ResultAsObject = 0;
                }
                catch (OperationCanceledException)
                {
                    job.ResultAsObject = -1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.ToString());
                    job.ResultAsObject = 1;
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
            return (job.ResultAsObject as int?) ?? -1;
        }



        private static int RunDownload(string [] targetArgs, string[] paramArgs)
        {
            string remotePath;
            string localPath;
            if(targetArgs.Length < 3)
            {
                Console.Error.WriteLine("download needs more 2 arguments.");
                Console.Error.WriteLine("download (remotepath) (localpath)");
                return 0;
            }

            remotePath = targetArgs[1];
            remotePath = remotePath.Replace('\\', '/');
            localPath = targetArgs[2];
            if(!localPath.Contains(':') && !localPath.StartsWith(@"\\"))
            {
                localPath = Path.GetFullPath(localPath);
            }
            if (!localPath.StartsWith(@"\\"))
            {
                localPath = ItemControl.GetLongFilename(localPath);
            }

            Console.Error.WriteLine("download");
            Console.Error.WriteLine("remote: "+ remotePath);
            Console.Error.WriteLine("local: "+  ItemControl.GetOrgFilename(localPath));

            var job = JobControler.CreateNewJob(JobClass.ControlMaster);
            job.DisplayName = "Download";
            JobControler.Run(job, (j) =>
            {
                try
                {
                    var j2 = InitServer(j);
                    j2.Wait(ct: j.Ct);

                    var target = FindItems(remotePath, ct: j.Ct);

                    if (target.Count() < 1)
                    {
                        j.ResultAsObject = 2;
                        return;
                    }

                    string remotePathBase = null;
                    if (remotePath.IndexOfAny(new[] { '*', '?' }) < 0)
                    {
                        remotePathBase = ItemControl.GetCommonPath(target);
                    }
                    else
                    {
                        remotePathBase = GetBasePath(remotePath);
                    }

                    target = RemoveDup(target);

                    ConsoleJobDisp.Run();

                    var j3 = target
                    .Where(x => x.ItemType == RemoteItemType.File)
                    .Select(x => RemoteServerFactory.PathToItem(x.FullPath, ReloadType.Reload).Result)
                    .Select(x => ItemControl.DownloadFile(Path.Combine(localPath, ItemControl.GetLocalFullPath(x.FullPath, remotePathBase)), x, j, true));
                    var j4 = target
                    .Where(x => x.ItemType == RemoteItemType.Folder)
                    .Select(x => RemoteServerFactory.PathToItem(x.FullPath, ReloadType.Reload).Result)
                    .Select(x => ItemControl.DownloadFolder(Path.GetDirectoryName(Path.Combine(localPath, ItemControl.GetLocalFullPath(x.FullPath, remotePathBase))), new[] { x }, j, true));

                    foreach (var jx in j3.Concat(j4).ToArray())
                    {
                        j.Ct.ThrowIfCancellationRequested();
                        jx.Wait(ct: j.Ct);
                    }

                    job.ResultAsObject = 0;
                }
                catch (OperationCanceledException)
                {
                    job.ResultAsObject = -1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.ToString());
                    job.ResultAsObject = 1;
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
            return (job.ResultAsObject as int?) ?? -1;
        }



        private static int RunUpload(string[] targetArgs, string[] paramArgs)
        {
            string remotePath;
            string localPath;
            if (targetArgs.Length < 3)
            {
                Console.Error.WriteLine("upload needs more 2 arguments.");
                Console.Error.WriteLine("upload (localpath) (remotetarget)");
                return 0;
            }

            remotePath = targetArgs[2];
            remotePath = remotePath.Replace('\\', '/');
            localPath = targetArgs[1];
            if (!localPath.Contains(':') && !localPath.StartsWith(@"\\"))
            {
                localPath = Path.GetFullPath(localPath);
            }
            if (!localPath.StartsWith(@"\\"))
            {
                localPath = ItemControl.GetLongFilename(localPath);
            }

            Console.Error.WriteLine("upload");
            Console.Error.WriteLine("remote: " + remotePath);
            Console.Error.WriteLine("local: " + ItemControl.GetOrgFilename(localPath));

            var job = JobControler.CreateNewJob(JobClass.ControlMaster);
            job.DisplayName = "Upload";
            JobControler.Run(job, (j) =>
            {
                try
                {
                    var j2 = InitServer(j);
                    j2.Wait(ct: j.Ct);

                    var target = FindItems(remotePath, ct: j.Ct);

                    if (target.Count() != 1)
                    {
                        Console.Error.WriteLine("upload needs 1 remote target item.");
                        j.ResultAsObject = 2;
                        return;
                    }
                    var remote = target.First();


                    ConsoleJobDisp.Run();

                    if (File.Exists(localPath))
                    {
                        ItemControl.UploadFiles(remote, new[] { localPath }, true, j);
                    }
                    else if (Directory.Exists(localPath))
                    {
                        ItemControl.UploadFolder(remote, localPath, true, j);
                    }
                    else
                    {
                        Console.Error.WriteLine("upload localitem not found.");
                        j.ResultAsObject = 2;
                        return;
                    }

                    while(JobControler.JobTypeCount(JobClass.Upload) > 0)
                    {
                        JobControler.JobList().Where(x => x.JobType == JobClass.Upload).FirstOrDefault()?.Wait(ct: j.Ct);
                    }

                    job.ResultAsObject = 0;
                }
                catch (OperationCanceledException)
                {
                    job.ResultAsObject = -1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.ToString());
                    job.ResultAsObject = 1;
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
            return (job.ResultAsObject as int?) ?? -1;
        }


        public static int CountChar(string s, char c)
        {
            return s.Length - s.Replace(c.ToString(), "").Length;
        }

        public static bool IsAncestor(IRemoteItem childitem, IRemoteItem parentitem)
        {
            var p = childitem.Parents.Where(x => x.FullPath != childitem.FullPath);
            return p.Any(x => parentitem.FullPath == x.FullPath) || p.Any(x => IsAncestor(x, parentitem));
        }

        static IEnumerable<IRemoteItem> RemoveDup(IEnumerable<IRemoteItem> items)
        {
            var ret = new List<IRemoteItem>();
            foreach (var i in items.OrderBy(x => CountChar(x.FullPath, '/')))
            {
                if (i.IsRoot) continue;
                if (ret.All(x => !IsAncestor(i, x)))
                {
                    ret.Add(i);
                }
            }
            return ret;
        }

        static string GetBasePath(string path)
        {
            string result = "";
            var m = Regex.Match(path, @"^(?<server>[^:]+)(://)(?<path>.*)$");
            if (m.Success)
            {
                var servername = m.Groups["server"].Value;
                if (servername.IndexOfAny(new[] { '?', '*' }) >= 0)
                {
                    return "";
                }
                path = m.Groups["path"].Value;
                result = servername + "://";
            }

            while (!string.IsNullOrEmpty(path))
            {
                m = Regex.Match(path, @"^(?<current>[^/\\]*)(/|\\)?(?<next>.*)$");
                path = m.Groups["next"].Value;

                var itemname = m.Groups["current"].Value;
                if (itemname == "**")
                {
                    return result;
                }
                else if (itemname.IndexOfAny(new[] { '?', '*' }) >= 0)
                {
                    return result;
                }
                else
                {
                    result += itemname + ((path == "") ? "" : "/");
                }
            }
            return result;
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
                    root = RemoteServerFactory.PathToItem(root.FullPath, ReloadType.Reload).Result;
                    if (root == null) return ret;
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

                root = RemoteServerFactory.PathToItem(root.FullPath, ReloadType.Reload).Result;
                if (root == null) return ret;
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
