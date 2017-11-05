using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using TSviewCloudPlugin;

namespace LibLocalSystem
{
    class LocalFileStream : FileStream
    {
        public LocalFileStream(string path, FileMode mode) : base(path, mode)
        {
        }

        public LocalFileStream(SafeFileHandle handle, FileAccess access) : base(handle, access)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileAccess access) : base(path, mode, access)
        {
        }

        public LocalFileStream(SafeFileHandle handle, FileAccess access, int bufferSize) : base(handle, access, bufferSize)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileAccess access, FileShare share) : base(path, mode, access, share)
        {
        }

        public LocalFileStream(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) : base(handle, access, bufferSize, isAsync)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) : base(path, mode, access, share, bufferSize)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) : base(path, mode, access, share, bufferSize, options)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) : base(path, mode, access, share, bufferSize, useAsync)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options) : base(path, mode, rights, share, bufferSize, options)
        {
        }

        public LocalFileStream(string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options, FileSecurity fileSecurity) : base(path, mode, rights, share, bufferSize, options, fileSecurity)
        {
        }

        private Job _masterJob;

        public Job MasterJob { get => _masterJob; set => _masterJob = value; }
        public override int ReadTimeout
        {
            get => 0;
            set
            {
                if (value == 0)
                    MasterJob?.Cancel();
            }
        }
    }
}
