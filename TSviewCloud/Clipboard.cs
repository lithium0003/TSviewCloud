using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TSviewCloudPlugin;

namespace NativeTypes
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3D8B0590-F691-11d2-8EA9-006097DF5BD4")]
    public interface IDataObjectAsyncCapability
    {
        void SetAsyncMode([MarshalAs(UnmanagedType.VariantBool)] bool fDoOpAsync);
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool GetAsyncMode();
        void StartOperation(IBindCtx pbcReserved);
        [return: MarshalAs(UnmanagedType.VariantBool)]
        bool InOperation();
        void EndOperation(uint hResult, IBindCtx pbcReserved, uint dwEffects);
    };
}
namespace TSviewCloud
{
    public class IStreamWrapper : Stream
    {
        private IStream m_stream;

        private void CheckDisposed()
        {
            if (m_stream == null)
            {
                throw new ObjectDisposedException("StreamWrapper");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (m_stream != null)
            {
                Marshal.ReleaseComObject(m_stream);
                m_stream = null;
            }
        }

        public IStreamWrapper(IStream stream)
        {
            m_stream = stream ?? throw new ArgumentNullException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get
            {
                CheckDisposed();

                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                m_stream.Stat(out stat, 1); //STATFLAG_NONAME

                return stat.cbSize;
            }
        }

        public override long Position
        {
            get
            {
                return Seek(0, SeekOrigin.Current);
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte[] localBuffer = buffer;

            if (offset > 0)
            {
                localBuffer = new byte[count];
            }

            IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(int));

            try
            {
                m_stream.Read(localBuffer, count, bytesReadPtr);
                int bytesRead = Marshal.ReadInt32(bytesReadPtr);

                if (offset > 0)
                {
                    Array.Copy(localBuffer, 0, buffer, offset, bytesRead);
                }

                return bytesRead;
            }
            finally
            {
                Marshal.FreeCoTaskMem(bytesReadPtr);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            int dwOrigin;

            switch (origin)
            {
                case SeekOrigin.Begin:

                    dwOrigin = 0;   // STREAM_SEEK_SET
                    break;

                case SeekOrigin.Current:

                    dwOrigin = 1;   // STREAM_SEEK_CUR
                    break;

                case SeekOrigin.End:

                    dwOrigin = 2;   // STREAM_SEEK_END
                    break;

                default:

                    throw new ArgumentOutOfRangeException();

            }

            IntPtr posPtr = Marshal.AllocCoTaskMem(sizeof(long));

            try
            {
                m_stream.Seek(offset, dwOrigin, posPtr);
                return Marshal.ReadInt64(posPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(posPtr);
            }
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }


    class AsyncDataObject : NativeTypes.IDataObjectAsyncCapability
    {
        protected int inOperationCount = 0;

        virtual public void EndOperation(uint hResult, IBindCtx pbcReserved, uint dwEffects)
        {
            Interlocked.Decrement(ref inOperationCount);
        }

        [return: MarshalAs(UnmanagedType.VariantBool)]
        public bool GetAsyncMode()
        {
            return true;
        }

        [return: MarshalAs(UnmanagedType.VariantBool)]
        public bool InOperation()
        {
            return inOperationCount > 0;
        }

        public void SetAsyncMode([MarshalAs(UnmanagedType.VariantBool)] bool fDoOpAsync)
        {
            if(fDoOpAsync == false)
            {
                throw new ArgumentException();
            }
        }

        public void StartOperation(IBindCtx pbcReserved)
        {
            Interlocked.Increment(ref inOperationCount);
        }
    }

    class ClipboardRemoteDrive: AsyncDataObject, IDataObject
    {
        public const string CFSTR_CLOUD_DRIVE_ITEMS = "TSviewCloudDriveItems";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FILEDESCRIPTOR
        {
            public UInt32 dwFlags;
            public Guid clsid;
            public System.Drawing.Size sizel;
            public System.Drawing.Point pointl;
            public UInt32 dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public UInt32 nFileSizeHigh;
            public UInt32 nFileSizeLow;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public String cFileName;
        }

        const uint FD_CLSID = 0x00000001;
        const uint FD_SIZEPOINT = 0x00000002;
        const uint FD_ATTRIBUTES = 0x00000004;
        const uint FD_CREATETIME = 0x00000008;
        const uint FD_ACCESSTIME = 0x00000010;
        const uint FD_WRITESTIME = 0x00000020;
        const uint FD_FILESIZE = 0x00000040;
        const uint FD_PROGRESSUI = 0x00004000;
        const uint FD_LINKUI = 0x00008000;
        const uint FD_UNICODE = 0x80000000;

        const uint FILE_ATTRIBUTE_READONLY = 0x1;
        const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        const uint FILE_ATTRIBUTE_ARCHIVE = 0x20;
        const uint FILE_ATTRIBUTE_DEVICE = 0x40;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const uint FILE_ATTRIBUTE_TEMPORARY = 0x100;
        const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x200;
        const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        const uint FILE_ATTRIBUTE_COMPRESSED = 0x800;
        const uint FILE_ATTRIBUTE_OFFLINE = 0x1000;
        const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x2000;
        const uint FILE_ATTRIBUTE_ENCRYPTED = 0x4000;
        const uint FILE_ATTRIBUTE_INTEGRITY_STREAM = 0x8000;
        const uint FILE_ATTRIBUTE_VIRTUAL = 0x10000;
        const uint FILE_ATTRIBUTE_NO_SCRUB_DATA = 0x20000;

        [DllImport("USER32.DLL", CharSet= CharSet.Auto, SetLastError= true)]
        static extern short RegisterClipboardFormat(string format);

        [DllImport("urlmon.dll", PreserveSig = false, ExactSpelling = true)]
        static extern IEnumFORMATETC CreateFormatEnumerator(
            int cfmtetc,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
            FORMATETC[] rgfmtetc);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        public static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

        static short CF_FILECONTENTS = RegisterClipboardFormat("FileContents");
        static short CF_FILEDESCRIPTORW = RegisterClipboardFormat("FileGroupDescriptorW");
        static short CF_PREFERREDDROPEFFECT = RegisterClipboardFormat("Preferred DropEffect");
        public static short CF_CLOUD_DRIVE_ITEMS = RegisterClipboardFormat(CFSTR_CLOUD_DRIVE_ITEMS);

        internal sealed class HResults
        {
            internal const int S_OK = 0;
            internal const int E_NOTIMPL = unchecked((int)0x80004001);
            internal const int E_POINTER = unchecked((int)0x80004003);
            internal const int E_FAIL = unchecked((int)0x80004005);
            internal const int E_FILENOTFOUND = unchecked((int)0x80070002);
            internal const int E_PATHNOTFOUND = unchecked((int)0x80070003);
            internal const int E_ACCESSDENIED = unchecked((int)0x80070005);
            internal const int E_INVALID_DATA = unchecked((int)0x8007000D);
            internal const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
            internal const int E_INVALIDARG = unchecked((int)0x80070057);
            internal const int E_INSUFFICIENT_BUFFER = unchecked((int)0x8007007A);
            internal const int WSAECONNABORTED = unchecked((int)0x80072745);
            internal const int WSAECONNRESET = unchecked((int)0x80072746);
            internal const int ERROR_TOO_MANY_CMDS = unchecked((int)0x80070038);
            internal const int ERROR_NOT_SUPPORTED = unchecked((int)0x80070032);
        };

        const int DV_E_FORMATETC = unchecked((int)0x80040064);
        const int DV_E_LINDEX = unchecked((int)0x80040068);
        const int DV_E_TYMED = unchecked((int)0x80040069);
        const int DV_E_DVASPECT = unchecked((int)0x8004006B);
        const int OLE_E_NOTRUNNING = unchecked((int)0x80040005);
        const int OLE_E_ADVISENOTSUPPORTED = unchecked((int)0x80040003);
        const int DATA_S_SAMEFORMATETC = unchecked((int)0x00040130);
        const int STG_E_MEDIUMFULL = unchecked((int)0x80030070);


        [ClassInterface(ClassInterfaceType.None)]
        public class StreamWrapper : IStream
        {
            public StreamWrapper(Stream stream)
            {
                this.stream = stream ?? throw new ArgumentNullException("stream", "Can't wrap null stream.");
            }

            Stream stream;

            public void Clone(out IStream ppstm)
            {
                ppstm = null;
            }

            public void Commit(int grfCommitFlags) { }

            public void CopyTo(IStream pstm,
              long cb, IntPtr pcbRead, IntPtr pcbWritten)
            { }

            public void LockRegion(long libOffset, long cb, int dwLockType) { }

            public void Read(byte[] pv, int cb, IntPtr pcbRead)
            {
                Marshal.WriteInt64(pcbRead, stream.Read(pv, 0, cb));
            }

            public void Revert() { }

            public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
            {
                Marshal.WriteInt64(plibNewPosition, stream.Seek(dlibMove, (SeekOrigin)dwOrigin));
            }

            public void SetSize(long libNewSize) { }

            public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
            {
                pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG();
                pstatstg.cbSize = stream.Length;
            }

            public void UnlockRegion(long libOffset, long cb, int dwLockType) { }

            public void Write(byte[] pv, int cb, IntPtr pcbWritten) { }
        };

        private byte[] fileGroupDescriptorBuffer;
        private string[] selectedItemPaths;
        private string[] baseItemPaths;

        private Stream dstream;

        private FORMATETC[] formatetc = new FORMATETC[] {
            new FORMATETC { cfFormat = CF_CLOUD_DRIVE_ITEMS,  dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -2, ptd = IntPtr.Zero, tymed = TYMED.TYMED_ISTREAM },
            new FORMATETC { cfFormat = CF_FILEDESCRIPTORW,  dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, ptd = IntPtr.Zero, tymed = TYMED.TYMED_HGLOBAL },
        };

        private void addfile(string path, Stream stream)
        {
            FILEDESCRIPTOR fileDescriptor = new FILEDESCRIPTOR();
            fileDescriptor.dwFlags = FD_PROGRESSUI | FD_UNICODE;
            fileDescriptor.cFileName = path;

            int size = Marshal.SizeOf(typeof(FILEDESCRIPTOR));
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(fileDescriptor, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            stream.Write(bytes, 0, bytes.Length);
        }

        private void addfolder(string path, Stream stream)
        {
            FILEDESCRIPTOR fileDescriptor = new FILEDESCRIPTOR();
            fileDescriptor.dwFlags = FD_PROGRESSUI | FD_UNICODE | FD_ATTRIBUTES;
            fileDescriptor.dwFileAttributes = FILE_ATTRIBUTE_DIRECTORY;
            fileDescriptor.cFileName = path;

            int size = Marshal.SizeOf(typeof(FILEDESCRIPTOR));
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(fileDescriptor, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            stream.Write(bytes, 0, bytes.Length);
        }

        private Dictionary<string, IRemoteItem> ExpandPath(string basepath, IRemoteItem items)
        {
            var total = new ConcurrentBag<KeyValuePair<string, IRemoteItem>>();
            string filename = items.Name;
            total.Add(new KeyValuePair<string, IRemoteItem>(basepath + filename, items));
            if (items.ItemType == RemoteItemType.Folder)
            {
                var children = RemoteServerFactory.PathToItem(items.FullPath).Result.Children;
                if (children.Count() > 0)
                {
                    Parallel.ForEach(children,
                        new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                        () => new Dictionary<string, IRemoteItem>(), (x, state, local) =>
                        {
                            return local.Concat(ExpandPath(basepath + filename + "\\", RemoteServerFactory.PathToItem(x.FullPath).Result)).ToDictionary(y => y.Key, y => y.Value);
                        },
                        (subtotal) =>
                        {
                            foreach (var i in subtotal)
                                total.Add(i);
                        });
                }
            }
            return total.ToDictionary(y => y.Key, y=> y.Value);
        }

        public ClipboardRemoteDrive(IEnumerable<IRemoteItem> items)
        {
            if ((items?.Count() ?? 0) == 0) throw new ArgumentException("no item selected");

            var total = new ConcurrentBag<KeyValuePair<string, IRemoteItem>>();
            Parallel.ForEach(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 1.0)) },
                () => new Dictionary<string, IRemoteItem>(), 
                (x, state, local) =>
                {
                    return local.Concat(ExpandPath("", RemoteServerFactory.PathToItem(x.FullPath).Result)).ToDictionary(y => y.Key, y => y.Value);
                },
                (subtotal) =>
                {
                    foreach (var i in subtotal)
                        total.Add(i);
                });
            SortedDictionary<string, IRemoteItem> exItems = new SortedDictionary<string, IRemoteItem>(total.ToDictionary(y => y.Key, y => y.Value));
            selectedItemPaths = exItems.Values.Select(x => x.FullPath).ToArray();
            baseItemPaths = items.Select(x => x.FullPath).ToArray();

            var flist = new List<FORMATETC>();
            using (var stream = new MemoryStream())
            {
                stream.Write(BitConverter.GetBytes(selectedItemPaths.Count()), 0, sizeof(int));

                int index = 0;
                foreach (var i in exItems)
                {
                    if (i.Value.ItemType == RemoteItemType.Folder) addfolder(i.Key, stream);
                    else addfile(i.Key, stream);
                    flist.Add(new FORMATETC { cfFormat = CF_FILECONTENTS, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index++, ptd = IntPtr.Zero, tymed = TYMED.TYMED_ISTREAM });
                }

                stream.Position = 0;
                fileGroupDescriptorBuffer = new byte[stream.Length];
                stream.Read(fileGroupDescriptorBuffer, 0, fileGroupDescriptorBuffer.Length);
            }
            formatetc = formatetc.Concat(flist).ToArray();
        }

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            medium = new STGMEDIUM();
            if (format.dwAspect != DVASPECT.DVASPECT_CONTENT)
            {
                Marshal.ThrowExceptionForHR(DV_E_DVASPECT);
            }
            if (format.cfFormat == CF_CLOUD_DRIVE_ITEMS)
            {
                var mem = new MemoryStream();
                var bf = new BinaryFormatter();
                bf.Serialize(mem, baseItemPaths); // copy string[] contains "server://path/to/item"
                mem.Position = 0;
                medium.tymed = TYMED.TYMED_ISTREAM;
                medium.unionmember = Marshal.GetComInterfaceForObject(new StreamWrapper(mem), typeof(IStream));
                medium.pUnkForRelease = null;
            }
            else if (format.cfFormat == CF_FILEDESCRIPTORW)
            {
                var hGlobal = Marshal.AllocHGlobal(fileGroupDescriptorBuffer.Length);
                Marshal.Copy(fileGroupDescriptorBuffer, 0, hGlobal, fileGroupDescriptorBuffer.Length);
                medium.tymed = TYMED.TYMED_HGLOBAL;
                medium.unionmember = hGlobal;
                medium.pUnkForRelease = null;
            }
            else if (format.cfFormat == CF_FILECONTENTS)
            {
                if (format.lindex >= 0 && format.lindex < selectedItemPaths.Length)
                {
                    medium.tymed = TYMED.TYMED_ISTREAM;
                    dstream?.Dispose();
                    medium.unionmember = Marshal.GetComInterfaceForObject(new StreamWrapper(dstream = RemoteServerFactory.PathToItem(selectedItemPaths[format.lindex]).Result.DownloadItemRaw()), typeof(IStream));
                    medium.pUnkForRelease = null;
                }
                else
                {
                    Marshal.ThrowExceptionForHR(DV_E_TYMED);
                }
            }
            else
            {
                Marshal.ThrowExceptionForHR(DV_E_TYMED);
            }
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
        {
            throw new NotImplementedException();
        }

        public int QueryGetData(ref FORMATETC format)
        {
            if (format.dwAspect != DVASPECT.DVASPECT_CONTENT)
            {
                return DV_E_DVASPECT;
            }
            if (format.cfFormat == CF_CLOUD_DRIVE_ITEMS)
            {
                return HResults.S_OK;
            }
            else if (format.cfFormat == CF_FILEDESCRIPTORW)
            {
                return HResults.S_OK;
            }
            else if (format.cfFormat == CF_FILECONTENTS)
            {
                return HResults.S_OK;
            }
            else
            {
                return DV_E_TYMED;
            }
        }

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
        {
            throw new NotImplementedException();
        }

        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
        {
            throw new NotImplementedException();
        }

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            return CreateFormatEnumerator(formatetc.Length, formatetc);
        }

        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
        {
            throw new NotImplementedException();
        }

        public void DUnadvise(int connection)
        {
            throw new NotImplementedException();
        }

        public int EnumDAdvise(out IEnumSTATDATA enumAdvise)
        {
            throw new NotImplementedException();
        }

        override public void EndOperation(uint hResult, IBindCtx pbcReserved, uint dwEffects)
        {
            base.EndOperation(hResult, pbcReserved, dwEffects);
            dstream?.Dispose();
            dstream = null;
        }
    
    }
}
