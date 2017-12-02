using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LibCryptCarotDAV
{
    class CryptCarotDAV
    {
        static byte[] _salt = Encoding.ASCII.GetBytes("CarotDAV Encryption 1.0 ");
        const int BlockSize = 128;
        const int KeySize = 256;
        const int PBKDF2_ITERATION = 0x400;

        public const int BlockSizeByte = BlockSize / 8;
        public const int CryptHeaderByte = 64;
        public const int CryptFooterByte = 64;

        byte[] Key;
        byte[] IV;
        string _password;

        string CryptNameHeader;

        public CryptCarotDAV(string CryptNameHeader)
        {
            this.CryptNameHeader = CryptNameHeader;
        }

        public string Password
        {
            get { return _password; }
            set
            {
                if (_password == value) return;
                if (value == null) return;
                _password = value;
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(_password, _salt, PBKDF2_ITERATION);

                Key = key.GetBytes(KeySize / 8);
                IV = key.GetBytes(BlockSize / 8);
            }
        }


        public string EncryptFilename(string uploadfilename)
        {
            ICryptoTransform encryptor;
            byte[] cryptbuf1 = new byte[BlockSizeByte];
            byte[] cryptbuf2 = new byte[BlockSizeByte];
            byte[] lastblock;
            int orglength;

            var aes = new AesCryptoServiceProvider();
            aes.BlockSize = BlockSize;
            aes.KeySize = KeySize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.Zeros;

            // Create a RijndaelManaged object
            aes.Key = Key;
            aes.IV = IV;
            encryptor = aes.CreateEncryptor();

            using (var ms = new MemoryStream()) {
                using (var cstream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plain = Encoding.UTF8.GetBytes(uploadfilename);
                    orglength = plain.Length;
                    cstream.Write(plain, 0, plain.Length);
                    cstream.FlushFinalBlock();

                    if (ms.Length >= BlockSizeByte * 2)
                    {
                        ms.Position = ms.Length - BlockSizeByte * 2;
                        ms.Read(cryptbuf1, 0, cryptbuf1.Length);
                        ms.Read(cryptbuf2, 0, cryptbuf2.Length);
                        int lastlen = orglength % BlockSizeByte;
                        if (lastlen == 0)
                        {
                            lastblock = cryptbuf1;
                        }
                        else
                        {
                            lastblock = new byte[lastlen];
                            Array.Copy(cryptbuf1, lastblock, lastlen);
                        }
                        ms.Position = ms.Length - BlockSizeByte * 2;
                        ms.Write(cryptbuf2, 0, cryptbuf2.Length);
                        ms.Write(lastblock, 0, lastblock.Length);
                        ms.SetLength(ms.Position);
                    }
                    byte[] crypt = new byte[ms.Length];
                    ms.Position = 0;
                    ms.Read(crypt, 0, crypt.Length);
                    var base64 = Convert.ToBase64String(crypt);
                    base64 = base64.Replace('+', '_');
                    base64 = base64.Replace('/', '-');
                    base64 = base64.Replace("=", "");
                    return CryptNameHeader + base64;
                }
            }
        }

        public string DecryptFilename(string cryptfilename)
        {
            if (!(cryptfilename?.StartsWith(CryptNameHeader) ?? false)) return null;

            var base64 = cryptfilename.Substring(CryptNameHeader.Length);
            base64 = base64.Replace('_', '+');
            base64 = base64.Replace('-', '/');
            switch(base64.Length % 4)
            {
                case 0:
                    break;
                case 1:
                    base64 += "===";
                    break;
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }
            byte[] crypt = Convert.FromBase64String(base64);

            ICryptoTransform decryptor;
            byte[] cryptbuf1 = new byte[BlockSizeByte];
            byte[] cryptbuf2 = new byte[BlockSizeByte];
            byte[] lastblock;

            var aes = new AesCryptoServiceProvider();
            aes.BlockSize = BlockSize;
            aes.KeySize = KeySize;
            aes.Padding = PaddingMode.Zeros;

            aes.Key = Key;
            aes.IV = IV;

            aes.Mode = CipherMode.CBC;
            decryptor = aes.CreateDecryptor();
            try
            {
                using (var ms = new MemoryStream())
                {
                    ms.Write(crypt, 0, crypt.Length);
                    if (ms.Length > BlockSize / 8)
                    {
                        int lastlen = (crypt.Length - 1) % BlockSizeByte + 1;
                        lastblock = new byte[lastlen];
                        ms.Position = ms.Length - BlockSize / 8 - lastlen;
                        ms.Read(cryptbuf2, 0, cryptbuf2.Length);
                        ms.Read(lastblock, 0, lastblock.Length);

                        aes.Mode = CipherMode.ECB;
                        var lastdecryptor = aes.CreateDecryptor();
                        lastdecryptor.TransformBlock(cryptbuf2, 0, BlockSizeByte, cryptbuf1, 0);

                        Array.Copy(lastblock, cryptbuf1, lastblock.Length);
                        ms.Position = ms.Length - BlockSizeByte - lastlen;

                        ms.Write(cryptbuf1, 0, cryptbuf1.Length);
                        ms.Write(cryptbuf2, 0, cryptbuf2.Length);
                    }
                    ms.Position = 0;

                    using (var cstream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        byte[] plain = new byte[ms.Length];
                        cstream.Read(plain, 0, plain.Length);

                        var decrypted = Encoding.UTF8.GetString(plain);
                        decrypted = decrypted.TrimEnd('\0');
                        if (decrypted.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                            return decrypted;
                        else
                            return null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public class CryptCarotDAV_CryptStream : Stream
        {
            byte[] header = new byte[CryptHeaderByte];
            byte[] hash = new byte[CryptFooterByte];

            HashStream innerStream;
            long innerStreamLength;
            CryptoStream cryptStream;
            long _position = 0;
            AesCryptoServiceProvider aes;
            ICryptoTransform encryptor;
            int patlen;
            long cryptlen;
            bool disposed;

            public CryptCarotDAV_CryptStream(CryptCarotDAV crypter, Stream baseStream, long? streamLength = null) : base()
            {
                innerStream = new HashStream(baseStream, new SHA256CryptoServiceProvider());
                innerStreamLength = streamLength ?? innerStream.Length;

                Array.Copy(_salt, header, 24);

                aes = new AesCryptoServiceProvider();
                aes.BlockSize = BlockSize;
                aes.KeySize = KeySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = crypter.Key;
                aes.IV = crypter.IV;
                encryptor = aes.CreateEncryptor();

                cryptStream = new CryptoStream(innerStream, encryptor, CryptoStreamMode.Read);
                if (innerStreamLength > 0)
                    patlen = (int)((innerStreamLength - 1) % BlockSizeByte + 1);
                else
                    patlen = BlockSizeByte;
                cryptlen = innerStreamLength + BlockSizeByte - patlen;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (!disposed)
                {
                    if (isDisposing)
                    {
                        innerStream?.Dispose();
                        cryptStream?.Dispose();
                    }

                    innerStream = null;
                    cryptStream = null;

                    disposed = true;
                }
            }

            public override long Length { get { return innerStreamLength + BlockSizeByte + CryptHeaderByte + CryptFooterByte; } }
            public override bool CanRead { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override bool CanSeek { get { return true; } }
            public override void Flush() { /* do nothing */ }

            public override long Position
            {
                get
                {
                    return _position;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int readbyte = 0;
                if (count <= 0) return readbyte;
                if(_position < CryptHeaderByte)
                {
                    int len = (int)(header.Length - _position);
                    if (len > count) len = count;
                    Array.Copy(header, _position, buffer, offset, len);
                    _position += len;
                    count -= len;
                    readbyte += len;
                    offset += len;
                    if (count <= 0) return readbyte;
                }
                if (_position < CryptHeaderByte + cryptlen)
                {
                    int len = cryptStream.Read(buffer, offset, count);
                    _position += len;
                    count -= len;
                    readbyte += len;
                    offset += len;
                    if (count <= 0) return readbyte;
                }
                if (_position < CryptHeaderByte + cryptlen + patlen)
                {
                    int len = count;
                    if (len > patlen) len = patlen;
                    var zero = new byte[len];
                    Array.Copy(zero, 0, buffer, offset, len);
                    _position += len;
                    count -= len;
                    readbyte += len;
                    offset += len;
                    if (count <= 0) return readbyte;
                }
                if (_position == CryptHeaderByte + cryptlen + patlen)
                {
                    innerStream.Flush();
                    Array.Copy(Encoding.ASCII.GetBytes(innerStream.Hash), hash, hash.Length);
                }
                if (_position < CryptHeaderByte + cryptlen + patlen + CryptFooterByte)
                {
                    long hash_offset = _position - (header.Length + cryptlen + patlen);
                    int len = (int)(hash.Length - hash_offset);
                    if (len < count) count = len;
                    Array.Copy(hash, hash_offset, buffer, offset, count);
                    _position += count;
                    readbyte += count;
                    offset += count;
                }
                return readbyte;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                // do nothing
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }
        }

        public class CryptCarotDAV_DecryptStream : Stream
        {
            Stream innerStream;
            HashStream hash;
            CryptoStream decryptStream;
            AesCryptoServiceProvider aes;
            ICryptoTransform decryptor;
            long CryptOffset; // 暗号化済みストリームのオフセット
            long CryptLength; // 暗号化済みストリームの全体の長さ
            long OrignalLength; // 元のストリームの長さ
            long CryptBodyLength; // 暗号化部分の長さ
            long offset; // デコード済みのストリームと要求されているストリームのオフセット
            long CryptPossiton; // 暗号化済みストリームでの位置
            long _Possition; //元のストリームの位置
            bool EOF = false;
            bool LengthSeek = false;

            bool disposed;

            protected override void Dispose(bool isDisposing)
            {
                if (!disposed)
                {
                    if (isDisposing)
                    {
                        hash?.Dispose();
                        try
                        {
                            decryptStream?.Dispose();
                        }
                        catch { }
                        innerStream?.Dispose();
                    }
                    hash = null;
                    decryptStream = null;
                    innerStream = null;

                    disposed = true;
                }
                base.Dispose(isDisposing);
            }

            public CryptCarotDAV_DecryptStream(CryptCarotDAV crypter, Stream baseStream, long orignalOffset = 0, long cryptedOffset = 0, long cryptedLength = -1) : base()
            {
                innerStream = new NotFlushStream(baseStream);
                if (cryptedLength < 0) CryptLength = innerStream.Length;
                else CryptLength = cryptedLength;
                CryptOffset = cryptedOffset;
                if (cryptedOffset > CryptHeaderByte)
                    offset = orignalOffset - (CryptOffset - CryptHeaderByte);
                else
                    offset = orignalOffset;
                CryptBodyLength = CryptLength - CryptHeaderByte - CryptHeaderByte;
                OrignalLength = CryptBodyLength - BlockSizeByte;
                CryptBodyLength = OrignalLength - ((OrignalLength - 1) % BlockSizeByte + 1) + BlockSizeByte;

                CryptPossiton = CryptOffset;
                // ヘッダ部分の読み込み
                if(CryptPossiton < CryptHeaderByte)
                {
                    int len = (int)(CryptHeaderByte - CryptPossiton);
                    byte[] buf = new byte[len];
                    innerStream.Read(buf, 0, len);
                    CryptPossiton += len;

                    byte[] header = new byte[CryptHeaderByte];
                    Array.Copy(_salt, header, 24);

                    if (!header.Skip((int)cryptedOffset).SequenceEqual(buf))
                    {
                        throw new FormatException("CarotDAV Crypted file: Header error");
                    }
                }

                aes = new AesCryptoServiceProvider();
                aes.BlockSize = BlockSize;
                aes.KeySize = KeySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = crypter.Key;
                aes.IV = crypter.IV;
                decryptor = aes.CreateDecryptor();

                decryptStream = new CryptoStream(innerStream, decryptor, CryptoStreamMode.Read);
                if (orignalOffset == 0)
                    hash = new HashStream(new SHA256CryptoServiceProvider());

                // 要求されているストリームとのズレを解消
                if (offset > 0)
                {
                    long len = offset;
                    long clen = OrignalLength - (CryptPossiton - CryptHeaderByte);
                    if (len > clen) len = clen;

                    const int readsize = 4 * 1024 * 1024;
                    while (len > 0)
                    {
                        var readlen = (len > readsize) ? readsize : len;

                        byte[] buf = new byte[readlen];
                        readlen = decryptStream.Read(buf, 0, buf.Length);
                        offset -= readlen;
                        CryptPossiton += readlen;
                        len -= readlen;
                    }
                    if (offset > 0) EOF = true;
                }
            }

            private void CalcHash(byte[] buffer, int offset, int count)
            {
                if (hash == null) return;
                hash.Write(buffer, offset, count);
                if (EOF)
                {
                    hash.Flush();

                    byte[] pad = new byte[CryptLength - CryptBodyLength - CryptHeaderByte - CryptFooterByte];
                    innerStream.Read(pad, 0, pad.Length);

                    byte[] buf = new byte[CryptFooterByte];
                    innerStream.Read(buf, 0, buf.Length);

                    if (!Encoding.ASCII.GetBytes(hash.Hash).SequenceEqual(buf))
                    {
                        TSviewCloudConfig.Config.Log.LogOut("CarotDAV Crypted file: Hash check failed");
                        throw new FormatException("CarotDAV Crypted file: Hash error");
                    }
                    TSviewCloudConfig.Config.Log.LogOut("CarotDAV Crypted file: Hash check ok");
                }
            }

            public override long Length { get { return OrignalLength; } }
            public override bool CanRead { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override bool CanSeek { get { return true; } }
            public override void Flush() { /* do nothing */ }

            public override long Position
            {
                get
                {
                    return _Possition;
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (EOF) return 0;
                if (LengthSeek) return 0;
                if(CryptPossiton - CryptHeaderByte + count > OrignalLength)
                {
                    count = (int)(OrignalLength - CryptPossiton + CryptHeaderByte);
                }
                try
                {
                    int len = decryptStream.Read(buffer, offset, count);
                    CryptPossiton += len;
                    _Possition += len;
                    if (CryptPossiton - CryptHeaderByte >= OrignalLength)
                    {
                        EOF = true;
                    }
                    CalcHash(buffer, offset, len);
                    return len;
                }
                catch
                {
                    return 0;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if(origin == SeekOrigin.End && offset == 0)
                {
                    LengthSeek = true;
                    return OrignalLength;
                }
                if (_Possition == 0 && origin == SeekOrigin.Begin && offset == 0)
                {
                    if (LengthSeek) LengthSeek = false;
                    return 0;
                }
                if (origin == SeekOrigin.Current && offset == 0)
                {
                    return _Possition;
                }
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }
        }

        public class NotFlushStream : Stream
        {
            Stream innerStream;
            bool disposed;

            public NotFlushStream(Stream innerStream)
            {
                this.innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        innerStream?.Dispose();
                    }
                    innerStream = null;
                    disposed = true;
                }
                base.Dispose(disposing);
            }

            public override long Length { get { return innerStream.Length; } }
            public override bool CanRead { get { return innerStream.CanRead; } }
            public override bool CanWrite { get { return innerStream.CanWrite; } }
            public override bool CanSeek { get { return innerStream.CanSeek; } }
            public override void Flush() { /* do nothing */ }

            public override long Position
            {
                get
                {
                    return innerStream.Position;
                }
                set
                {
                    innerStream.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return innerStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return innerStream.Seek(offset, origin);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                innerStream.Write(buffer, offset, count);
            }

            public override void SetLength(long value)
            {
                innerStream.SetLength(value);
            }
        }
    }
}
