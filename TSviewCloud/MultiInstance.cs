using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TSviewCloud
{
    static class MultiInstance
    {
        private const int HWND_BROADCAST = 0xffff;

        [DllImport("User32.dll", SetLastError = true)]
        public static extern int PostMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int RegisterWindowMessage(string lpString);


        static string MutexName = "TScloudViewMutex_" + TSviewCloudConfig.Config.ApplicationID;
        static string MemoryName = "TScloudViewMemory_" + TSviewCloudConfig.Config.ApplicationID;
        const int MemorySize = 4 * 1024 * 1024;
        static Mutex mutex;
        static int WM_LOCAL_TSVIEWCLOUD_INIT;
        static int WM_LOCAL_TSVIEWCLOUD_RELOAD;
        static MemoryMappedFile sharememory;

        private static uint _InstanceCount;
        public static uint InstanceCount
        {
            get { return _InstanceCount; }
        }

        static MultiInstance()
        {
            bool created;
            mutex = new Mutex(false, MutexName, out created);
            sharememory = MemoryMappedFile.CreateOrOpen(MemoryName, MemorySize);

            WM_LOCAL_TSVIEWCLOUD_INIT = RegisterWindowMessage("TSviewCloud_WindowMessage_Init");
            WM_LOCAL_TSVIEWCLOUD_RELOAD = RegisterWindowMessage("TSviewCloud_WindowMessage_Reload");

            PostMessage((IntPtr)HWND_BROADCAST, WM_LOCAL_TSVIEWCLOUD_INIT, 0, 1);
        }

        public static void Init()
        {
            mutex.WaitOne();
            try
            {
                using (var view = sharememory.CreateViewAccessor(0, 4))
                {
                    _InstanceCount = view.ReadUInt32(0);
                    _InstanceCount++;
                    view.Write(0, _InstanceCount);
                }
                if(InstanceCount == 1)
                {
                    using (var view = sharememory.CreateViewAccessor(4, 4))
                    {
                        view.Write(0, 0);
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public static void Finish()
        {
            mutex.WaitOne();
            try
            {
                using (var view = sharememory.CreateViewAccessor(0, 4))
                {
                    _InstanceCount = view.ReadUInt32(0);
                    _InstanceCount--;
                    view.Write(0, _InstanceCount);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public static void AddNewInstance()
        {
            mutex.WaitOne();
            try
            {
                using (var view = sharememory.CreateViewAccessor(0, 4))
                {
                    _InstanceCount = view.ReadUInt32(0);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public static void RemoveInstance()
        {
            mutex.WaitOne();
            try
            {
                using (var view = sharememory.CreateViewAccessor(0, 4))
                {
                    _InstanceCount = view.ReadUInt32(0);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public static void RegisterReload(string fullpath)
        {
            byte[] Buffer = Encoding.UTF8.GetBytes(fullpath);
            int position;
            mutex.WaitOne();
            try
            {
                using (var view = sharememory.CreateViewAccessor(4, 4))
                {
                    position = view.ReadInt32(0);
                    if (position + Buffer.Length >= MemorySize - 10)
                        position = 0;
                    view.Write(0, position + Buffer.Length);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            using(var view = sharememory.CreateViewAccessor(8, MemorySize - 8))
            {
                view.WriteArray(position, Buffer, 0, Buffer.Length);
            }

            PostMessage((IntPtr)HWND_BROADCAST, WM_LOCAL_TSVIEWCLOUD_RELOAD, position, Buffer.Length);
        }

        public static string GetReload(int offset, int length)
        {
            byte[] buffer = new byte[length];
            using (var view = sharememory.CreateViewAccessor(8, MemorySize - 8))
            {
                view.ReadArray(offset, buffer, 0, buffer.Length);
            }

            return Encoding.UTF8.GetString(buffer);
        }
    }
}
