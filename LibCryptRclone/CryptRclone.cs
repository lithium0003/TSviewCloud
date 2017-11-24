using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LibCryptRclone
{
    public class CryptRclone
    {
        private class Poly1305
        {
            public const int TagSize = 16;

            public static void Sum(out byte[] tag, byte[] msg, byte[] key, int offset = 0)
            {
                if (key.Length != 32) throw new ArgumentException("key length must be 32");
                tag = new byte[TagSize];

                ulong r0 = BitConverter.ToUInt32(key, 0) & 0x3ffffff;
                ulong r1 = (BitConverter.ToUInt32(key, 3) >> 2) & 0x3ffff03;
                ulong r2 = (BitConverter.ToUInt32(key, 6) >> 4) & 0x3ffc0ff;
                ulong r3 = (BitConverter.ToUInt32(key, 9) >> 6) & 0x3f03fff;
                ulong r4 = (BitConverter.ToUInt32(key, 12) >> 8) & 0x00fffff;

                uint h0 = 0;
                uint h1 = 0;
                uint h2 = 0;
                uint h3 = 0;
                uint h4 = 0;

                ulong R1 = r1 * 5;
                ulong R2 = r2 * 5;
                ulong R3 = r3 * 5;
                ulong R4 = r4 * 5;

                int pos = 0;
                while (pos + offset <= msg.Length - TagSize)
                {
                    // h += msg
                    h0 += BitConverter.ToUInt32(msg, 0 + pos + offset) & 0x3ffffff;
                    h1 += (BitConverter.ToUInt32(msg, 3 + pos + offset) >> 2) & 0x3ffffff;
                    h2 += (BitConverter.ToUInt32(msg, 6 + pos + offset) >> 4) & 0x3ffffff;
                    h3 += (BitConverter.ToUInt32(msg, 9 + pos + offset) >> 6) & 0x3ffffff;
                    h4 += (BitConverter.ToUInt32(msg, 12 + pos + offset) >> 8) | (1 << 24);

                    // h *= r
                    ulong d0 = (h0 * r0) + (h1 * R4) + (h2 * R3) + (h3 * R2) + (h4 * R1);
                    ulong d1 = (d0 >> 26) + (h0 * r1) + (h1 * r0) + (h2 * R4) + (h3 * R3) + (h4 * R2);
                    ulong d2 = (d1 >> 26) + (h0 * r2) + (h1 * r1) + (h2 * r0) + (h3 * R4) + (h4 * R3);
                    ulong d3 = (d2 >> 26) + (h0 * r3) + (h1 * r2) + (h2 * r1) + (h3 * r0) + (h4 * R4);
                    ulong d4 = (d3 >> 26) + (h0 * r4) + (h1 * r3) + (h2 * r2) + (h3 * r1) + (h4 * r0);

                    // h %= p
                    h0 = (uint)d0 & 0x3ffffff;
                    h1 = (uint)d1 & 0x3ffffff;
                    h2 = (uint)d2 & 0x3ffffff;
                    h3 = (uint)d3 & 0x3ffffff;
                    h4 = (uint)d4 & 0x3ffffff;

                    h0 += (uint)(d4 >> 26) * 5;
                    h1 += h0 >> 26;
                    h0 = h0 & 0x3ffffff;

                    pos += TagSize;
                }

                if (pos + offset < msg.Length)
                {
                    byte[] block = new byte[TagSize];
                    int len = msg.Length - (pos + offset);
                    Array.Copy(msg, pos + offset, block, 0, len);
                    block[len] = 0x01;

                    // h += msg
                    h0 += BitConverter.ToUInt32(block, 0) & 0x3ffffff;
                    h1 += (BitConverter.ToUInt32(block, 3) >> 2) & 0x3ffffff;
                    h2 += (BitConverter.ToUInt32(block, 6) >> 4) & 0x3ffffff;
                    h3 += (BitConverter.ToUInt32(block, 9) >> 6) & 0x3ffffff;
                    h4 += (BitConverter.ToUInt32(block, 12) >> 8);

                    // h *= r
                    ulong d0 = (h0 * r0) + (h1 * R4) + (h2 * R3) + (h3 * R2) + (h4 * R1);
                    ulong d1 = (d0 >> 26) + (h0 * r1) + (h1 * r0) + (h2 * R4) + (h3 * R3) + (h4 * R2);
                    ulong d2 = (d1 >> 26) + (h0 * r2) + (h1 * r1) + (h2 * r0) + (h3 * R4) + (h4 * R3);
                    ulong d3 = (d2 >> 26) + (h0 * r3) + (h1 * r2) + (h2 * r1) + (h3 * r0) + (h4 * R4);
                    ulong d4 = (d3 >> 26) + (h0 * r4) + (h1 * r3) + (h2 * r2) + (h3 * r1) + (h4 * r0);

                    // h %= p
                    h0 = (uint)d0 & 0x3ffffff;
                    h1 = (uint)d1 & 0x3ffffff;
                    h2 = (uint)d2 & 0x3ffffff;
                    h3 = (uint)d3 & 0x3ffffff;
                    h4 = (uint)d4 & 0x3ffffff;

                    h0 += (uint)(d4 >> 26) * 5;
                    h1 += h0 >> 26;
                    h0 = h0 & 0x3ffffff;
                }

                // h %= p reduction
                h2 += h1 >> 26;
                h1 &= 0x3ffffff;
                h3 += h2 >> 26;
                h2 &= 0x3ffffff;
                h4 += h3 >> 26;
                h3 &= 0x3ffffff;
                h0 += 5 * (h4 >> 26);
                h4 &= 0x3ffffff;
                h1 += h0 >> 26;
                h0 &= 0x3ffffff;

                // h - p
                uint t0 = h0 + 5;
                uint t1 = h1 + (t0 >> 26);
                uint t2 = h2 + (t1 >> 26);
                uint t3 = h3 + (t2 >> 26);
                uint t4 = h4 + (t3 >> 26) - (1 << 26);
                t0 &= 0x3ffffff;
                t1 &= 0x3ffffff;
                t2 &= 0x3ffffff;
                t3 &= 0x3ffffff;

                // select h if h < p else h - p
                uint t_mask = (t4 >> 31) - 1;
                uint h_mask = ~t_mask;
                h0 = (h0 & h_mask) | (t0 & t_mask);
                h1 = (h1 & h_mask) | (t1 & t_mask);
                h2 = (h2 & h_mask) | (t2 & t_mask);
                h3 = (h3 & h_mask) | (t3 & t_mask);
                h4 = (h4 & h_mask) | (t4 & t_mask);

                // h %= 2^128
                h0 |= h1 << 26;
                h1 = ((h1 >> 6) | (h2 << 20));
                h2 = ((h2 >> 12) | (h3 << 14));
                h3 = ((h3 >> 18) | (h4 << 8));

                // s: the s part of the key
                // tag = (h + s) % (2^128)
                ulong t = (ulong)h0 + BitConverter.ToUInt32(key, 16);
                h0 = (uint)t;
                t = (ulong)h1 + BitConverter.ToUInt32(key, 20) + (t >> 32);
                h1 = (uint)t;
                t = (ulong)h2 + BitConverter.ToUInt32(key, 24) + (t >> 32);
                h2 = (uint)t;
                t = (ulong)h3 + BitConverter.ToUInt32(key, 28) + (t >> 32);
                h3 = (uint)t;

                Array.Copy(BitConverter.GetBytes(h0), 0, tag, 0, 4);
                Array.Copy(BitConverter.GetBytes(h1), 0, tag, 4, 4);
                Array.Copy(BitConverter.GetBytes(h2), 0, tag, 8, 4);
                Array.Copy(BitConverter.GetBytes(h3), 0, tag, 12, 4);
            }

            public static bool Verify(byte[] mac, byte[] msg, byte[] key, int offset = 0)
            {
                if (key.Length != 32) return false;
                if (mac.Length != TagSize) return false;

                byte[] t;
                Sum(out t, msg, key, offset);
                return t.SequenceEqual(mac);
            }
        }

        private class SecretboxNet
        {
            static byte[] Sigma = { (byte)'e', (byte)'x', (byte)'p', (byte)'a', (byte)'n', (byte)'d', (byte)' ', (byte)'3', (byte)'2', (byte)'-', (byte)'b', (byte)'y', (byte)'t', (byte)'e', (byte)' ', (byte)'k' };
            uint[] buf = new uint[16];

            public const int Overhead = Poly1305.TagSize;

            internal static uint R(uint x, int y)
            {
                return (x << y) | (x >> (32 - y));
            }

            internal void HSala20(out byte[] output, byte[] input, byte[] k, byte[] c)
            {
                const int round = 20;
                if (input.Length != 16) throw new ArgumentException("input buffer length must be 16");
                if (k.Length != 32) throw new ArgumentException("k length must be 32");
                if (c.Length != 16) throw new ArgumentException("c length must be 16");
                output = new byte[32];

                buf[0] = BitConverter.ToUInt32(c, 0);
                buf[1] = BitConverter.ToUInt32(k, 0);
                buf[2] = BitConverter.ToUInt32(k, 4);
                buf[3] = BitConverter.ToUInt32(k, 8);
                buf[4] = BitConverter.ToUInt32(k, 12);
                buf[5] = BitConverter.ToUInt32(c, 4);
                buf[6] = BitConverter.ToUInt32(input, 0);
                buf[7] = BitConverter.ToUInt32(input, 4);
                buf[8] = BitConverter.ToUInt32(input, 8);
                buf[9] = BitConverter.ToUInt32(input, 12);
                buf[10] = BitConverter.ToUInt32(c, 8);
                buf[11] = BitConverter.ToUInt32(k, 16);
                buf[12] = BitConverter.ToUInt32(k, 20);
                buf[13] = BitConverter.ToUInt32(k, 24);
                buf[14] = BitConverter.ToUInt32(k, 28);
                buf[15] = BitConverter.ToUInt32(c, 12);

                for (int i = 0; i < round; i += 2)
                {
                    buf[4] ^= R(buf[0] + buf[12], 7);
                    buf[8] ^= R(buf[4] + buf[0], 9);
                    buf[12] ^= R(buf[8] + buf[4], 13);
                    buf[0] ^= R(buf[12] + buf[8], 18);

                    buf[9] ^= R(buf[5] + buf[1], 7);
                    buf[13] ^= R(buf[9] + buf[5], 9);
                    buf[1] ^= R(buf[13] + buf[9], 13);
                    buf[5] ^= R(buf[1] + buf[13], 18);

                    buf[14] ^= R(buf[10] + buf[6], 7);
                    buf[2] ^= R(buf[14] + buf[10], 9);
                    buf[6] ^= R(buf[2] + buf[14], 13);
                    buf[10] ^= R(buf[6] + buf[2], 18);

                    buf[3] ^= R(buf[15] + buf[11], 7);
                    buf[7] ^= R(buf[3] + buf[15], 9);
                    buf[11] ^= R(buf[7] + buf[3], 13);
                    buf[15] ^= R(buf[11] + buf[7], 18);


                    buf[1] ^= R(buf[0] + buf[3], 7);
                    buf[2] ^= R(buf[1] + buf[0], 9);
                    buf[3] ^= R(buf[2] + buf[1], 13);
                    buf[0] ^= R(buf[3] + buf[2], 18);

                    buf[6] ^= R(buf[5] + buf[4], 7);
                    buf[7] ^= R(buf[6] + buf[5], 9);
                    buf[4] ^= R(buf[7] + buf[6], 13);
                    buf[5] ^= R(buf[4] + buf[7], 18);

                    buf[11] ^= R(buf[10] + buf[9], 7);
                    buf[8] ^= R(buf[11] + buf[10], 9);
                    buf[9] ^= R(buf[8] + buf[11], 13);
                    buf[10] ^= R(buf[9] + buf[8], 18);

                    buf[12] ^= R(buf[15] + buf[14], 7);
                    buf[13] ^= R(buf[12] + buf[15], 9);
                    buf[14] ^= R(buf[13] + buf[12], 13);
                    buf[15] ^= R(buf[14] + buf[13], 18);
                }

                Array.Copy(BitConverter.GetBytes(buf[0]), 0, output, 0, 4);
                Array.Copy(BitConverter.GetBytes(buf[5]), 0, output, 4, 4);
                Array.Copy(BitConverter.GetBytes(buf[10]), 0, output, 8, 4);
                Array.Copy(BitConverter.GetBytes(buf[15]), 0, output, 12, 4);
                Array.Copy(BitConverter.GetBytes(buf[6]), 0, output, 16, 4);
                Array.Copy(BitConverter.GetBytes(buf[7]), 0, output, 20, 4);
                Array.Copy(BitConverter.GetBytes(buf[8]), 0, output, 24, 4);
                Array.Copy(BitConverter.GetBytes(buf[9]), 0, output, 28, 4);
            }

            internal void SalaCore(byte[] output, byte[] input, byte[] k, byte[] c)
            {
                uint[] inbuf = new uint[16];
                uint[] outbuf = new uint[16];
                const int round = 20;
                if (input.Length != 16) throw new ArgumentException("input buffer length must be 16");
                if (k.Length != 32) throw new ArgumentException("k length must be 32");
                if (c.Length != 16) throw new ArgumentException("c length must be 16");

                inbuf[0] = BitConverter.ToUInt32(c, 0);
                inbuf[1] = BitConverter.ToUInt32(k, 0);
                inbuf[2] = BitConverter.ToUInt32(k, 4);
                inbuf[3] = BitConverter.ToUInt32(k, 8);
                inbuf[4] = BitConverter.ToUInt32(k, 12);
                inbuf[5] = BitConverter.ToUInt32(c, 4);
                inbuf[6] = BitConverter.ToUInt32(input, 0);
                inbuf[7] = BitConverter.ToUInt32(input, 4);
                inbuf[8] = BitConverter.ToUInt32(input, 8);
                inbuf[9] = BitConverter.ToUInt32(input, 12);
                inbuf[10] = BitConverter.ToUInt32(c, 8);
                inbuf[11] = BitConverter.ToUInt32(k, 16);
                inbuf[12] = BitConverter.ToUInt32(k, 20);
                inbuf[13] = BitConverter.ToUInt32(k, 24);
                inbuf[14] = BitConverter.ToUInt32(k, 28);
                inbuf[15] = BitConverter.ToUInt32(c, 12);

                SalaCore(round, inbuf, outbuf);
                for (int i = 0; i < 16; i++)
                    Array.Copy(BitConverter.GetBytes(outbuf[i]), 0, output, i * 4, 4);
            }

            internal void SalaCore(int round, uint[] input, uint[] output)
            {
                if (round % 2 != 0) throw new ArgumentOutOfRangeException("round");
                if (input.Length != 16) throw new ArgumentException("input buffer length must be 16");
                if (output.Length != 16) throw new ArgumentException("output buffer length must be 16");

                Array.Copy(input, buf, 16);

                for (int i = 0; i < round; i += 2)
                {
                    buf[4] ^= R(buf[0] + buf[12], 7);
                    buf[8] ^= R(buf[4] + buf[0], 9);
                    buf[12] ^= R(buf[8] + buf[4], 13);
                    buf[0] ^= R(buf[12] + buf[8], 18);

                    buf[9] ^= R(buf[5] + buf[1], 7);
                    buf[13] ^= R(buf[9] + buf[5], 9);
                    buf[1] ^= R(buf[13] + buf[9], 13);
                    buf[5] ^= R(buf[1] + buf[13], 18);

                    buf[14] ^= R(buf[10] + buf[6], 7);
                    buf[2] ^= R(buf[14] + buf[10], 9);
                    buf[6] ^= R(buf[2] + buf[14], 13);
                    buf[10] ^= R(buf[6] + buf[2], 18);

                    buf[3] ^= R(buf[15] + buf[11], 7);
                    buf[7] ^= R(buf[3] + buf[15], 9);
                    buf[11] ^= R(buf[7] + buf[3], 13);
                    buf[15] ^= R(buf[11] + buf[7], 18);


                    buf[1] ^= R(buf[0] + buf[3], 7);
                    buf[2] ^= R(buf[1] + buf[0], 9);
                    buf[3] ^= R(buf[2] + buf[1], 13);
                    buf[0] ^= R(buf[3] + buf[2], 18);

                    buf[6] ^= R(buf[5] + buf[4], 7);
                    buf[7] ^= R(buf[6] + buf[5], 9);
                    buf[4] ^= R(buf[7] + buf[6], 13);
                    buf[5] ^= R(buf[4] + buf[7], 18);

                    buf[11] ^= R(buf[10] + buf[9], 7);
                    buf[8] ^= R(buf[11] + buf[10], 9);
                    buf[9] ^= R(buf[8] + buf[11], 13);
                    buf[10] ^= R(buf[9] + buf[8], 18);

                    buf[12] ^= R(buf[15] + buf[14], 7);
                    buf[13] ^= R(buf[12] + buf[15], 9);
                    buf[14] ^= R(buf[13] + buf[12], 13);
                    buf[15] ^= R(buf[14] + buf[13], 18);
                }

                Array.Copy(buf, output, 16);
                for (int i = 0; i < 16; i++)
                {
                    output[i] += input[i];
                }
            }

            internal void XORKeyStream(out byte[] output, byte[] input, byte[] counter, byte[] key, int offset = 0)
            {
                if (counter.Length != 16) throw new ArgumentException("counter length must be 16");
                if (key.Length != 32) throw new ArgumentException("key length must be 32");

                byte[] block = new byte[64];
                byte[] inCounter = new byte[16];
                Array.Copy(counter, inCounter, 16);

                output = new byte[input.Length - offset];
                int pos = 0;
                while (pos + offset <= input.Length - 64)
                {
                    SalaCore(block, inCounter, key, Sigma);
                    for (int i = 0; i < block.Length; i++)
                        output[pos + i] = (byte)(input[pos + offset + i] ^ block[i]);

                    uint u = 1;
                    for (int i = 8; i < 16; i++)
                    {
                        u += inCounter[i];
                        inCounter[i] = (byte)u;
                        u >>= 8;
                    }

                    pos += 64;
                }

                if (pos + offset < input.Length)
                {
                    SalaCore(block, inCounter, key, Sigma);
                    for (int i = 0; i + offset + pos < input.Length; i++)
                        output[pos + i] = (byte)(input[offset + pos + i] ^ block[i]);
                }
            }

            internal void Setup(out byte[] subkey, out byte[] counter, byte[] nonce, byte[] key)
            {
                if (nonce.Length != 24) throw new ArgumentException("nonce length must be 24");
                if (key.Length != 32) throw new ArgumentException("key length must be 32");

                byte[] hnonce = new byte[16];
                counter = new byte[16];

                Array.Copy(nonce, hnonce, 16);
                HSala20(out subkey, hnonce, key, Sigma);

                Array.Copy(nonce, 16, counter, 0, 8);
            }

            internal void Seal(out byte[] output, byte[] message, byte[] nonce, byte[] key)
            {
                if (nonce.Length != 24) throw new ArgumentException("nonce length must be 24");
                if (key.Length != 32) throw new ArgumentException("key length must be 32");

                byte[] subkey;
                byte[] counter;
                Setup(out subkey, out counter, nonce, key);

                byte[] firstBlock = new byte[64];
                XORKeyStream(out firstBlock, firstBlock, counter, subkey);

                byte[] poly1305Key = new byte[32];
                Array.Copy(firstBlock, poly1305Key, 32);

                output = new byte[message.Length + Poly1305.TagSize];

                byte[] firstMessageBlock;
                if (message.Length < 32)
                {
                    firstMessageBlock = new byte[message.Length];
                    Array.Copy(message, firstMessageBlock, message.Length);
                }
                else
                {
                    firstMessageBlock = new byte[32];
                    Array.Copy(message, firstMessageBlock, 32);
                }

                for (int i = 0; i < firstMessageBlock.Length; i++)
                {
                    output[Poly1305.TagSize + i] = (byte)(firstBlock[32 + i] ^ firstMessageBlock[i]);
                }

                counter[8] = 1;
                byte[] outputbuf;
                XORKeyStream(out outputbuf, message, counter, subkey, firstMessageBlock.Length);
                Array.Copy(outputbuf, 0, output, Poly1305.TagSize + firstMessageBlock.Length, outputbuf.Length);

                byte[] tag;
                Poly1305.Sum(out tag, output, poly1305Key, Poly1305.TagSize);
                Array.Copy(tag, output, tag.Length);
            }

            internal bool Open(out byte[] output, byte[] box, byte[] nonce, byte[] key)
            {
                if (nonce.Length != 24) throw new ArgumentException("nonce length must be 24");
                if (key.Length != 32) throw new ArgumentException("key length must be 32");

                byte[] subkey;
                byte[] counter;
                Setup(out subkey, out counter, nonce, key);

                byte[] firstBlock = new byte[64];
                XORKeyStream(out firstBlock, firstBlock, counter, subkey);

                byte[] poly1305Key = new byte[32];
                Array.Copy(firstBlock, poly1305Key, 32);

                byte[] tag = new byte[Poly1305.TagSize];
                Array.Copy(box, tag, tag.Length);

                if (!Poly1305.Verify(tag, box, poly1305Key, Poly1305.TagSize))
                {
                    output = null;
                    return false;
                }

                output = new byte[box.Length - Poly1305.TagSize];

                byte[] firstMessageBlock;
                if (output.Length < 32)
                {
                    firstMessageBlock = new byte[output.Length];
                    Array.Copy(box, Poly1305.TagSize, firstMessageBlock, 0, firstMessageBlock.Length);
                }
                else
                {
                    firstMessageBlock = new byte[32];
                    Array.Copy(box, Poly1305.TagSize, firstMessageBlock, 0, firstMessageBlock.Length);
                }

                for (int i = 0; i < firstMessageBlock.Length; i++)
                {
                    output[i] = (byte)(firstBlock[32 + i] ^ firstMessageBlock[i]);
                }

                counter[8] = 1;
                byte[] outputbuf;
                XORKeyStream(out outputbuf, box, counter, subkey, firstMessageBlock.Length + Poly1305.TagSize);
                Array.Copy(outputbuf, 0, output, firstMessageBlock.Length, outputbuf.Length);
                return true;
            }
        }

        private class AES_EME : Aes
        {
            private AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

            public AES_EME() : base()
            {
                aes.Padding = PaddingMode.None;
                aes.Mode = CipherMode.ECB;
                KeySize = 256;
            }

            public override byte[] Key
            {
                get
                {
                    return base.Key;
                }

                set
                {
                    base.Key = value;
                    aes.Key = value;
                }
            }

            public override int KeySize
            {
                get
                {
                    return base.KeySize;
                }

                set
                {
                    base.KeySize = value;
                    aes.KeySize = value;
                }
            }
            public override ICryptoTransform CreateDecryptor()
            {
                return CreateDecryptor(Key, IV);
            }

            public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
            {
                return new AES_EME_crypt(aes.CreateDecryptor(rgbKey, rgbIV), rgbIV, aes.CreateEncryptor(rgbKey, rgbIV));
            }

            public override ICryptoTransform CreateEncryptor()
            {
                return CreateEncryptor(Key, IV);
            }

            public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
            {
                return new AES_EME_crypt(aes.CreateEncryptor(rgbKey, rgbIV), rgbIV);
            }

            public override void GenerateIV()
            {
                aes.GenerateIV();
                base.IV = aes.IV;
            }

            public override void GenerateKey()
            {
                aes.GenerateKey();
                base.Key = aes.Key;
            }
        }

        private class AES_EME_crypt : ICryptoTransform
        {
            ICryptoTransform aes;
            ICryptoTransform aes_crypt;
            byte[] tweak;
            bool Decrypt = false;

            public AES_EME_crypt(ICryptoTransform aes, byte[] tweak, ICryptoTransform aes_crypt = null)
            {
                this.aes = aes;
                this.tweak = tweak;
                if (aes_crypt != null) Decrypt = true;
                this.aes_crypt = aes_crypt ?? aes;
            }

            public bool CanReuseTransform
            {
                get
                {
                    return true;
                }
            }

            public bool CanTransformMultipleBlocks
            {
                get
                {
                    return true;
                }
            }

            public int InputBlockSize
            {
                get
                {
                    return aes.InputBlockSize;
                }
            }

            public int OutputBlockSize
            {
                get
                {
                    return aes.OutputBlockSize;
                }
            }

            private void MultByTwo(out byte[] output, byte[] input)
            {
                if (input.Length != 16) throw new ArgumentOutOfRangeException("input length must be 16");
                output = new byte[16];

                output[0] = (byte)(2 * input[0]);
                if (input[15] >= 128)
                {
                    output[0] = (byte)(output[0] ^ 135);
                }
                for (int j = 1; j < 16; j++)
                {
                    output[j] = (byte)(2 * input[j]);
                    if (input[j - 1] >= 128)
                    {
                        output[j] += 1;
                    }
                }
            }

            // tabulateL - calculate L_i for messages up to a length of m cipher blocks
            private byte[][] TabulateL(int m)
            {
                /* set L0 = 2*AESenc(K; 0) */
                var eZero = new byte[16];

                var Li = new byte[16];

                aes_crypt.TransformBlock(eZero, 0, 16, Li, 0);

                var LTable = new byte[m][];
                for (int i = 0; i < m; i++)
                {
                    LTable[i] = new byte[16];
                }

                // Allocate pool once and slice into m pieces in the loop

                for (int i = 0; i < m; i++)
                {
                    MultByTwo(out Li, Li);
                    Array.Copy(Li, LTable[i], 16);
                }
                return LTable;
            }

            private void XorBlocks(out byte[] output, byte[] in1, byte[] in2)
            {
                if (in1.Length != in2.Length) throw new ArgumentOutOfRangeException("input length are different");

                output = new byte[in1.Length];
                for (int i = 0; i < in1.Length; i++)
                    output[i] = (byte)(in1[i] ^ in2[i]);
            }

            private void XorBlocks(out byte[] output, byte[] in1, int offset1, int len, byte[] in2, int offset2)
            {
                output = new byte[len];
                for (int i = 0; i < len; i++)
                    output[i] = (byte)(in1[i + offset1] ^ in2[i + offset2]);
            }

            private void XorBlocks(ref byte[] output, int outoffset, byte[] in1, int offset1, int len, byte[] in2, int offset2)
            {
                for (int i = 0; i < len; i++)
                    output[i + outoffset] = (byte)(in1[i + offset1] ^ in2[i + offset2]);
            }

            public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
            {
                var T = tweak;
                var P = inputBuffer;

                if (aes.InputBlockSize != 16 || aes.OutputBlockSize != 16) throw new ArgumentOutOfRangeException("block size must be 16");
                if (T.Length != 16) throw new ArgumentOutOfRangeException("tweak length must be 16");
                if (inputCount % 16 != 0) throw new ArgumentOutOfRangeException("Data P must be a multiple of 16 long");

                var m = inputCount / 16;
                if (m == 0 || m > 16 * 8) throw new ArgumentOutOfRangeException("EME operates on 1 to 16*8 block-cipher blocks");

                var C = new byte[inputCount];
                var LTable = TabulateL(m);

                byte[] PPj;
                for (int j = 0; j < m; j++)
                {
                    var Pj = new byte[16];
                    Array.Copy(P, j * 16 + inputOffset, Pj, 0, 16);
                    /* PPj = 2**(j-1)*L xor Pj */
                    XorBlocks(out PPj, Pj, LTable[j]);
                    /* PPPj = AESenc(K; PPj) */
                    aes.TransformBlock(PPj, 0, 16, C, j * 16);
                }

                byte[] MP;
                /* MP =(xorSum PPPj) xor T */
                XorBlocks(out MP, C, 0, 16, T, 0);
                for (int j = 1; j < m; j++)
                {
                    XorBlocks(out MP, MP, 0, 16, C, j * 16);
                }

                /* MC = AESenc(K; MP) */
                var MC = new byte[16];
                aes.TransformBlock(MP, 0, 16, MC, 0);

                /* M = MP xor MC */
                byte[] M;
                XorBlocks(out M, MP, MC);
                byte[] CCCj;
                for (int j = 1; j < m; j++)
                {
                    MultByTwo(out M, M);
                    /* CCCj = 2**(j-1)*M xor PPPj */
                    XorBlocks(out CCCj, C, j * 16, 16, M, 0);
                    Array.Copy(CCCj, 0, C, j * 16, 16);
                }

                /* CCC1 = (xorSum CCCj) xor T xor MC */
                byte[] CCC1;
                XorBlocks(out CCC1, MC, T);
                for (int j = 1; j < m; j++)
                {
                    XorBlocks(out CCC1, CCC1, 0, 16, C, j * 16);
                }
                Array.Copy(CCC1, 0, C, 0, 16);

                for (int j = 0; j < m; j++)
                {
                    /* CCj = AES-enc(K; CCCj) */
                    aes.TransformBlock(C, j * 16, 16, C, j * 16);
                    /* Cj = 2**(j-1)*L xor CCj */
                    XorBlocks(ref C, j * 16, C, j * 16, 16, LTable[j], 0);
                }
                Array.Copy(C, 0, outputBuffer, outputOffset, C.Length);
                return C.Length;
            }

            public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
            {
                var padlen = 16 - inputCount % 16;
                if (padlen == 0) padlen = 16;
                if (Decrypt) padlen = 0;
                var paddedPlaintext = new byte[inputCount + padlen];
                Array.Copy(inputBuffer, inputOffset, paddedPlaintext, 0, inputCount);
                for (int i = inputCount; i < paddedPlaintext.Length; i++)
                {
                    paddedPlaintext[i] = (byte)padlen;
                }
                var output = new byte[paddedPlaintext.Length];
                TransformBlock(paddedPlaintext, 0, paddedPlaintext.Length, output, 0);
                return output;
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // TODO: dispose managed state (managed objects).
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~AES_EME() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion

        }



        const int nameCipherBlockSize = 16;
        static readonly string fileMagic = "RCLONE\x00\x00";
        static readonly int fileMagicSize = fileMagic.Length;
        const int fileNonceSize = 24;
        public static readonly int fileHeaderSize = fileMagicSize + fileNonceSize;
        const int blockHeaderSize = 16;
        public const int blockDataSize = 64 * 1024;
        public const int chunkSize = blockHeaderSize + blockDataSize;
        public static readonly string encryptedSuffix = ".bin";

        static readonly byte[] defaultSalt = new byte[] { 0xA8, 0x0D, 0xF4, 0x3A, 0x8F, 0xBD, 0x03, 0x08, 0xA7, 0xCA, 0xB8, 0x3E, 0x58, 0x1F, 0x86, 0xB1 };

        byte[] dataKey = new byte[32]; // Key for secretbox
        byte[] nameKey = new byte[32]; // 16,24 or 32 bytes
        byte[] nameTweak = new byte[nameCipherBlockSize]; // used to tweak the name crypto

        static RNGCryptoServiceProvider rnd = new RNGCryptoServiceProvider();

        AES_EME name_aes = new AES_EME();

        static CryptRclone()
        {
            CreateDecodeMap();
        }

        public CryptRclone()
        {
            GenarateKey();
        }

        public CryptRclone(string password, string salt = null)
        {
            _password = password;
            _salt = salt;
            GenarateKey();
        }

        public void GenarateKey()
        {
            byte[] key;
            int keysize = dataKey.Length + nameKey.Length + nameTweak.Length;
            var saltbytes = defaultSalt;
            if (!string.IsNullOrEmpty(_salt))
            {
                saltbytes = Encoding.ASCII.GetBytes(_salt);
            }
            if (string.IsNullOrEmpty(_password))
            {
                key = new byte[keysize];
            }
            else
            {
                key = SCrypt.ComputeDerivedKey(Encoding.ASCII.GetBytes(_password), saltbytes, 16384, 8, 1, null, keysize);
            }
            Array.Copy(key, 0, dataKey, 0, dataKey.Length);
            Array.Copy(key, dataKey.Length, nameKey, 0, nameKey.Length);
            Array.Copy(key, dataKey.Length + nameKey.Length, nameTweak, 0, nameTweak.Length);

            //aes
            name_aes.Key = nameKey;
            name_aes.IV = nameTweak;
        }

        bool _isEncryptedName;
        public bool IsEncryptedName { get => _isEncryptedName; set => _isEncryptedName = value; }

        string _password;
        public string Password
        {
            get { return _password; }
            set
            {
                if (_password == value) return;
                if (value == null) return;
                _password = value;
                GenarateKey();
            }
        }

        string _salt;
        public string Salt
        {
            get { return _salt; }
            set
            {
                if (_salt == value) return;
                if (value == null) return;
                _salt = value;
                GenarateKey();
            }
        }


        static readonly string encodeHex = "0123456789ABCDEFGHIJKLMNOPQRSTUV";

        static string EncodeFileName(byte[] input)
        {
            int len = input.Length;
            int offset = 0;
            if (len == 0) return "";
            byte[] output = new byte[(len + 4) / 5 * 8];
            int p = 0;
            while (len - offset > 0)
            {
                byte b0, b1, b2, b3, b4, b5, b6, b7;
                b0 = b1 = b2 = b3 = b4 = b5 = b6 = b7 = 0;

                var r = len - offset;
                if (r > 4)
                {
                    b7 = (byte)(input[offset + 4] & 0x1F);
                    b6 = (byte)(input[offset + 4] >> 5);
                }
                if (r > 3)
                {
                    b6 |= (byte)((input[offset + 3] << 3) & 0x1F);
                    b5 = (byte)((input[offset + 3] >> 2) & 0x1F);
                    b4 = (byte)(input[offset + 3] >> 7);
                }
                if (r > 2)
                {
                    b4 |= (byte)((input[offset + 2] << 1) & 0x1F);
                    b3 = (byte)((input[offset + 2] >> 4) & 0x1F);
                }
                if (r > 1)
                {
                    b3 |= (byte)((input[offset + 1] << 4) & 0x1F);
                    b2 = (byte)((input[offset + 1] >> 1) & 0x1F);
                    b1 = (byte)((input[offset + 1] >> 6) & 0x1F);
                }
                if (r > 0)
                {
                    b1 |= (byte)((input[offset] << 2) & 0x1F);
                    b0 = (byte)(input[offset] >> 3);
                }

                output[p + 0] = (byte)encodeHex[b0];
                output[p + 1] = (byte)encodeHex[b1];
                output[p + 2] = (byte)encodeHex[b2];
                output[p + 3] = (byte)encodeHex[b3];
                output[p + 4] = (byte)encodeHex[b4];
                output[p + 5] = (byte)encodeHex[b5];
                output[p + 6] = (byte)encodeHex[b6];
                output[p + 7] = (byte)encodeHex[b7];

                if (r < 5)
                {
                    output[p + 7] = (byte)'=';
                    if (r < 4)
                    {
                        output[p + 6] = (byte)'=';
                        output[p + 5] = (byte)'=';
                        if (r < 3)
                        {
                            output[p + 4] = (byte)'=';
                            if (r < 2)
                            {
                                output[p + 3] = (byte)'=';
                                output[p + 2] = (byte)'=';

                            }
                        }
                    }
                    break;
                }
                offset += 5;
                p += 8;
            }
            return Encoding.ASCII.GetString(output).TrimEnd('=').ToLower();
        }

        static Dictionary<char, byte> decodemap = null;

        static void CreateDecodeMap()
        {
            decodemap = new Dictionary<char, byte>();
            for (byte i = 0; i < encodeHex.Length; i++)
            {
                decodemap.Add(encodeHex[i], i);
            }
            decodemap.Add('=', 0);
        }

        static byte[] DecodeFileName(string input)
        {
            var len = input.Length;
            var padlen = ((len / 8) + 1) * 8 - len;
            input = input.ToUpper();
            if (padlen > 0)
                input = input + new string('=', padlen);

            if (padlen == 7 || padlen == 5 || padlen == 2) return null;

            len = input.Length;
            var output = new byte[len / 8 * 5];
            int p = 0;
            int offset = 0;

            try
            {
                var buf = new byte[8];
                var dst = new byte[5];
                while (len - offset > 0)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        buf[j] = decodemap[input[offset + j]];
                    }
                    dst[4] = (byte)(buf[6] << 5 | buf[7]);
                    dst[3] = (byte)(buf[4] << 7 | buf[5] << 2 | buf[6] >> 3);
                    dst[2] = (byte)(buf[3] << 4 | buf[4] >> 1);
                    dst[1] = (byte)(buf[1] << 6 | buf[2] << 1 | buf[3] >> 4);
                    dst[0] = (byte)(buf[0] << 3 | buf[1] >> 2);

                    int c = 0;
                    if (len - offset > 8)
                    {
                        c = 5;
                    }
                    else
                    {
                        switch (padlen)
                        {
                            case 1:
                                c = 4;
                                break;
                            case 3:
                                c = 3;
                                break;
                            case 4:
                                c = 2;
                                break;
                            case 6:
                                c = 1;
                                break;

                        }
                    }
                    for (int j = 0; j < c; j++)
                        output[p + j] = dst[j];

                    offset += 8;
                    p += c;
                }
                Array.Resize(ref output, p);
                return output;
            }
            catch
            {
                return null;
            }
        }

        public string EncryptName(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return "";
            using (var encrypter = name_aes.CreateEncryptor())
            {
                var input = Encoding.UTF8.GetBytes(plaintext);
                return EncodeFileName(encrypter.TransformFinalBlock(input, 0, input.Length));
            }
        }

        static string CheckNameValid(byte[] plaintxt)
        {
            if (plaintxt.Any(x => x < 0x20 || x == 0x7F)) return "";
            try
            {
                return Encoding.UTF8.GetString(plaintxt);
            }
            catch
            {
                return "";
            }
        }

        public string DecryptName(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return "";

            if (IsEncryptedName)
            {
                var rawcipher = DecodeFileName(ciphertext);
                if (rawcipher == null) return "";
                if (rawcipher.Length == 0 || rawcipher.Length % (name_aes.BlockSize / 8) != 0) return "";
                using (var decryptor = name_aes.CreateDecryptor())
                {
                    var Plaintext = decryptor.TransformFinalBlock(rawcipher, 0, rawcipher.Length);
                    int padlen = Plaintext[Plaintext.Length - 1];
                    if (padlen > (name_aes.BlockSize / 8)) return "";
                    Array.Resize(ref Plaintext, Plaintext.Length - padlen);
                    return CheckNameValid(Plaintext);
                }
            }
            else
            {
                if (ciphertext.EndsWith(encryptedSuffix))
                    return ciphertext.Substring(0, ciphertext.Length - encryptedSuffix.Length);
                return ciphertext;
            }
        }

        static public bool IsNameEncrypted(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Regex.IsMatch(name, "^[0-9a-v]+$"))
            {
                var namelen = name.Length;
                var padlen = 8 - ((namelen - 1) % 8 + 1);
                var clen = (namelen + padlen) / 8 * 5;
                switch (padlen)
                {
                    case 0:
                        break;
                    case 1:
                        clen -= 1;
                        break;
                    case 3:
                        clen -= 2;
                        break;
                    case 4:
                        clen -= 3;
                        break;
                    case 6:
                        clen -= 4;
                        break;
                    default:
                        clen = 0;
                        break;
                }
                if (clen > 0 && clen % 16 == 0)
                {
                    return true;
                }
            }
            return false;
        }

        static public Int64 CalcEncryptedSize(Int64 org_size)
        {
            if (org_size < 1) return fileHeaderSize;

            var chunk_num = (org_size - 1) / blockDataSize;
            var last_chunk_size = (org_size - 1) % blockDataSize + 1;

            return fileHeaderSize + chunkSize * chunk_num + (blockHeaderSize + last_chunk_size);
        }

        static public Int64 CalcDecryptedSize(Int64 crypt_size)
        {
            crypt_size -= fileHeaderSize;
            if (crypt_size <= 0) return crypt_size;

            var chunk_num = crypt_size / chunkSize;
            var last_chunk_size = crypt_size % chunkSize;

            if (last_chunk_size == 0) return chunk_num * blockDataSize;
            if (last_chunk_size < blockHeaderSize) return -1;

            return chunk_num * blockDataSize + last_chunk_size - blockHeaderSize;
        }


        public class CryptRclone_CryptStream : Stream
        {
            Stream innerStream;
            long orgLength;
            long _position = 0;
            long _cryptedlen = 0;
            byte[] plainBlock;
            byte[] cryptedBlock;
            long crypted_blockno = -1;
            long last_blockno = -1;
            Secretbox.Secretbox crypter = new Secretbox.Secretbox();
            CryptRclone cRclone;

            byte[] _nonce;
            private bool disposed;

            public CryptRclone_CryptStream(CryptRclone cRclone, Stream baseStream, long? length = null, byte[] nonce = null) : base()
            {
                if (nonce != null && nonce.Length != fileNonceSize) throw new ArgumentOutOfRangeException("nonce");
                if (nonce == null)
                {
                    _nonce = new byte[fileNonceSize];
                    rnd.GetBytes(_nonce);
                }
                else
                {
                    _nonce = nonce;
                }

                innerStream = baseStream;
                orgLength = length ?? innerStream.Length;
                _cryptedlen = EncryptedSize(orgLength);
                last_blockno = (_cryptedlen == fileHeaderSize) ? 0 : (_cryptedlen - fileHeaderSize - 1) / chunkSize;
                this.cRclone = cRclone;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (!disposed)
                {
                    if (isDisposing)
                    {
                        innerStream?.Dispose();
                    }

                    innerStream = null;
 
                    disposed = true;
                }
            }

            static long EncryptedSize(long size)
            {
                long blocks = size / blockDataSize;
                long residue = size % blockDataSize;
                long encryptedSize = fileHeaderSize + blocks * chunkSize;
                if (residue != 0) encryptedSize += (blockHeaderSize + residue);
                return encryptedSize;
            }

            public override long Length { get { return _cryptedlen; } }
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

            void IncNonce()
            {
                for (int i = 0; i < _nonce.Length; i++)
                {
                    var digit = _nonce[i];
                    byte newdigit = (byte)(digit + 1);
                    _nonce[i] = newdigit;
                    if (newdigit >= digit) break;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int readbyte = 0;
                if (count <= 0) return readbyte;
                if (_position < fileMagicSize)
                {
                    int len = (int)(fileMagicSize - _position);
                    if (len > count) len = count;
                    Array.Copy(Encoding.ASCII.GetBytes(fileMagic), _position, buffer, offset, len);
                    _position += len;
                    count -= len;
                    readbyte += len;
                    offset += len;
                    if (count <= 0) return readbyte;
                }
                if (_position < fileMagicSize + fileNonceSize)
                {
                    int len = count;
                    if (len > fileNonceSize) len = fileNonceSize;
                    int nonce_offset = (int)_position - fileMagicSize;
                    Array.Copy(_nonce, nonce_offset, buffer, offset, len);
                    _position += len;
                    count -= len;
                    readbyte += len;
                    offset += len;
                    if (count <= 0) return readbyte;
                }
                while (_position < _cryptedlen)
                {
                    long blockno = (_position - fileHeaderSize) / chunkSize;
                    if (crypted_blockno != blockno)
                    {
                        int len = blockDataSize;
                        if (last_blockno == blockno) len = (int)(_cryptedlen - _position);

                        plainBlock = new byte[len];
                        System.Diagnostics.Debug.Assert(innerStream.Position == blockno * blockDataSize);
                        var readlen = 0;
                        while (readlen < len && innerStream.Position < orgLength)
                        {
                            readlen += innerStream.Read(plainBlock, readlen, len - readlen);
                        }
                        Array.Resize(ref plainBlock, readlen);

                        crypter.Seal(out cryptedBlock, plainBlock, _nonce, cRclone.dataKey);
                        crypted_blockno = blockno;

                        IncNonce();
                    }

                    int crypt_offset = (int)(_position - fileHeaderSize - blockno * chunkSize);
                    int len2 = count;
                    if (len2 > cryptedBlock.Length - crypt_offset) len2 = cryptedBlock.Length - crypt_offset;
                    Array.Copy(cryptedBlock, crypt_offset, buffer, offset, len2);
                    _position += len2;
                    count -= len2;
                    readbyte += len2;
                    offset += len2;
                    if (count <= 0) return readbyte;
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


        public class CryptRclone_DeryptStream : Stream, IHashStream
        {
            Stream innerStream;
            long CryptLength; // encrypted stream size
            long OriginalLength; // plain stream size
            long _CryptPossition; // encrypted stream position
            long _Possition;  // plain stream position
            byte[] plainBlock;
            byte[] cryptedBlock;
            long plain_blockno;
            bool LengthSeek = false;
            Secretbox.Secretbox decrypter = new Secretbox.Secretbox();
            CryptRclone cRclone;

            byte[] _nonce;
            byte[] _nonce_base;
            private bool disposed;

            public byte[] Nonce
            {
                get { return _nonce_base; }
            }

            public CryptRclone_DeryptStream(CryptRclone cRclone, Stream baseStream, long orignalOffset = 0, long cryptedOffset = 0, long cryptedLength = -1, byte[] nonce = null) : base()
            {
                innerStream = baseStream;
                this.cRclone = cRclone;
                if (nonce != null && nonce.Length != fileNonceSize) throw new ArgumentOutOfRangeException("nonce");
                if (nonce != null)
                {
                    _nonce_base = nonce;
                    _nonce = (byte [])nonce.Clone();
                }
                if (_nonce == null && orignalOffset > fileMagicSize) throw new ArgumentOutOfRangeException("nonce");

                if (cryptedLength < 0) CryptLength = innerStream.Length;
                else CryptLength = cryptedLength;
                OriginalLength = CalcDecryptedSize(CryptLength);

                _CryptPossition = cryptedOffset;
                _Possition = orignalOffset;
                if (_CryptPossition < fileMagicSize)
                {
                    if(cryptedLength < fileMagicSize)
                        throw new FormatException("CryptRclone Crypted file: Header error");

                    int len = (int)(fileMagicSize - _CryptPossition);
                    byte[] buf = new byte[len];
                    int readlen = 0;
                    while(readlen < len)
                    {
                        readlen += innerStream.Read(buf, readlen, len - readlen);
                    }

                    byte[] header = Encoding.ASCII.GetBytes(fileMagic);

                    if (!header.Skip((int)_CryptPossition).SequenceEqual(buf))
                    {
                        throw new FormatException("CryptRclone Crypted file: Header error");
                    }

                    _CryptPossition += len;
                }
                if (_CryptPossition < fileHeaderSize)
                {
                    if (cryptedLength < fileHeaderSize)
                        throw new FormatException("CryptRclone Crypted file: Header error");

                    int len = (int)(fileHeaderSize - _CryptPossition);
                    if (len != fileNonceSize && _nonce == null) throw new ArgumentOutOfRangeException("nonce");
                    _nonce = new byte[fileNonceSize];
                    int readlen = 0;
                    while (readlen < fileNonceSize)
                    {
                        readlen += innerStream.Read(_nonce, readlen, fileNonceSize - readlen);
                    }

                    _nonce_base = (byte[])_nonce.Clone();

                    _CryptPossition += len;
                }
                long crypt_blockno = (_CryptPossition - fileHeaderSize) / chunkSize;
                plain_blockno = _Possition / blockDataSize;

                if (plain_blockno < crypt_blockno)
                    throw new ArgumentOutOfRangeException("rewind not allowed");
                while (plain_blockno > crypt_blockno && _CryptPossition < CryptLength)
                {
                    long nextblockpos = (crypt_blockno + 1) * chunkSize + fileHeaderSize;
                    if (nextblockpos > CryptLength) nextblockpos = CryptLength;
                    int len = (int)(nextblockpos - _CryptPossition);
                    byte[] buf = new byte[len];
                    int offset = 0;
                    while (len < offset)
                    {
                        offset += innerStream.Read(buf, offset, len - offset);
                    }

                    _CryptPossition += len;
                    crypt_blockno = (_CryptPossition - fileHeaderSize) / chunkSize;
                }
                if(crypt_blockno > 0)
                    AddNonce(crypt_blockno);
                FillBuffer();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (!disposed)
                {
                    if (isDisposing)
                    {
                        innerStream?.Dispose();
                    }

                    innerStream = null;

                    disposed = true;
                }
            }

            void AddNonce(long count)
            {
                for (int i = 0; i < _nonce.Length; i++)
                {
                    var digit = _nonce[i];
                    byte addcount = (byte)(count & 0xFF);
                    byte newdigit = (byte)(digit + addcount);
                    _nonce[i] = newdigit;
                    count = count >> 8;
                    if (newdigit < digit)
                    {
                        count++;
                    }
                    if (count == 0) break;
                }
            }

            void IncNonce()
            {
                for (int i = 0; i < _nonce.Length; i++)
                {
                    var digit = _nonce[i];
                    byte newdigit = (byte)(digit + 1);
                    _nonce[i] = newdigit;
                    if (newdigit >= digit) break;
                }
            }

            private void FillBuffer()
            {
                if (disposed) return;
                if (_Possition >= OriginalLength) return;
                plain_blockno = _Possition / blockDataSize;
                long nextblockpos = (plain_blockno + 1) * chunkSize + fileHeaderSize;
                if (nextblockpos > CryptLength) nextblockpos = CryptLength;
                int len = (int)(nextblockpos - _CryptPossition);
                if (len == 0)
                {
                    cryptedBlock = null;
                    plainBlock = null;
                    return;
                }
                if (len < blockHeaderSize)
                    throw new FormatException("CryptRclone Crypted file: Block Header error");

                cryptedBlock = new byte[len];
                int offset = 0;
                while (offset < len)
                {
                    if (innerStream == null) return;
                    offset += innerStream.Read(cryptedBlock, offset, len - offset);
                }

                if (!decrypter.Open(out plainBlock, cryptedBlock, _nonce, cRclone.dataKey))
                    throw new FormatException("CryptRclone Crypted file: Bad Block error");

                _CryptPossition += len;
                IncNonce();
            }

            public override long Length { get { return OriginalLength; } }
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

            public string Hash
            {
                get
                {
                    return (innerStream as IHashStream).Hash;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read_byte = 0;
                while (count > 0 && _Possition < OriginalLength && !disposed)
                {
                    int block_pos = (int)(_Possition - plain_blockno * blockDataSize);
                    int len = count;
                    if (blockDataSize - block_pos < count) len = blockDataSize - block_pos;
                    if (len > plainBlock.Length - block_pos) len = plainBlock.Length - block_pos;
                    Array.Copy(plainBlock, block_pos, buffer, offset, len);

                    count -= len;
                    offset += len;
                    _Possition += len;
                    read_byte += len;

                    if (plain_blockno != _Possition / blockDataSize)
                        FillBuffer();
                }
                return read_byte;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (origin == SeekOrigin.End && offset == 0)
                {
                    LengthSeek = true;
                    return OriginalLength;
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


        #region License
        /*
        CryptSharp
        Copyright (c) 2011, 2013 James F. Bellinger <http://www.zer7.com/software/cryptsharp>

        Permission to use, copy, modify, and/or distribute this software for any
        purpose with or without fee is hereby granted, provided that the above
        copyright notice and this permission notice appear in all copies.

        THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
        WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
        MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
        ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
        WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
        ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
        OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
        */
        #endregion

        public class SCrypt
        {
            const int hLen = 32;

            /// <summary>
            /// Computes a derived key.
            /// </summary>
            /// <param name="key">The key to derive from.</param>
            /// <param name="salt">
            ///     The salt.
            ///     A unique salt means a unique SCrypt stream, even if the original key is identical.
            /// </param>
            /// <param name="cost">
            ///     The cost parameter, typically a fairly large number such as 262144.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="blockSize">
            ///     The mixing block size, typically 8.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="parallel">
            ///     The level of parallelism, typically 1.
            ///     CPU time scales approximately linearly with this parameter.
            /// </param>
            /// <param name="maxThreads">
            ///     The maximum number of threads to spawn to derive the key.
            ///     This is limited by the <paramref name="parallel"/> value.
            ///     <c>null</c> will use as many threads as possible.
            /// </param>
            /// <param name="derivedKeyLength">The desired length of the derived key.</param>
            /// <returns>The derived key.</returns>
            public static byte[] ComputeDerivedKey(byte[] key, byte[] salt,
                                                   int cost, int blockSize, int parallel, int? maxThreads,
                                                   int derivedKeyLength)
            {
                if (derivedKeyLength < 0) throw new ArgumentOutOfRangeException("derivedKeyLength");

                using (Pbkdf2 kdf = GetStream(key, salt, cost, blockSize, parallel, maxThreads))
                {
                    return kdf.Read(derivedKeyLength);
                }
            }

            /// <summary>
            /// The SCrypt algorithm creates a salt which it then uses as a one-iteration
            /// PBKDF2 key stream with SHA256 HMAC. This method lets you retrieve this intermediate salt.
            /// </summary>
            /// <param name="key">The key to derive from.</param>
            /// <param name="salt">
            ///     The salt.
            ///     A unique salt means a unique SCrypt stream, even if the original key is identical.
            /// </param>
            /// <param name="cost">
            ///     The cost parameter, typically a fairly large number such as 262144.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="blockSize">
            ///     The mixing block size, typically 8.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="parallel">
            ///     The level of parallelism, typically 1.
            ///     CPU time scales approximately linearly with this parameter.
            /// </param>
            /// <param name="maxThreads">
            ///     The maximum number of threads to spawn to derive the key.
            ///     This is limited by the <paramref name="parallel"/> value.
            ///     <c>null</c> will use as many threads as possible.
            /// </param>
            /// <returns>The effective salt.</returns>
            public static byte[] GetEffectivePbkdf2Salt(byte[] key, byte[] salt,
                                                        int cost, int blockSize, int parallel, int? maxThreads)
            {
                if (key == null) throw new ArgumentNullException("key");
                if (salt == null) throw new ArgumentNullException("salt");
                return MFcrypt(key, salt, cost, blockSize, parallel, maxThreads);
            }

            /// <summary>
            /// Creates a derived key stream from which a derived key can be read.
            /// </summary>
            /// <param name="key">The key to derive from.</param>
            /// <param name="salt">
            ///     The salt.
            ///     A unique salt means a unique scrypt stream, even if the original key is identical.
            /// </param>
            /// <param name="cost">
            ///     The cost parameter, typically a fairly large number such as 262144.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="blockSize">
            ///     The mixing block size, typically 8.
            ///     Memory usage and CPU time scale approximately linearly with this parameter.
            /// </param>
            /// <param name="parallel">
            ///     The level of parallelism, typically 1.
            ///     CPU time scales approximately linearly with this parameter.
            /// </param>
            /// <param name="maxThreads">
            ///     The maximum number of threads to spawn to derive the key.
            ///     This is limited by the <paramref name="parallel"/> value.
            ///     <c>null</c> will use as many threads as possible.
            /// </param>
            /// <returns>The derived key stream.</returns>
            public static Pbkdf2 GetStream(byte[] key, byte[] salt,
                                           int cost, int blockSize, int parallel, int? maxThreads)
            {
                byte[] B = GetEffectivePbkdf2Salt(key, salt, cost, blockSize, parallel, maxThreads);
                Pbkdf2 kdf = new Pbkdf2(new HMACSHA256(key), B, 1);
                Array.Clear(B, 0, B.Length);
                return kdf;
            }

            private static bool IsPositivePowerOf2(int value)
            {
                return 0 < value && 0 == (value & (value - 1));
            }

            static byte[] MFcrypt(byte[] P, byte[] S,
                                  int cost, int blockSize, int parallel, int? maxThreads)
            {
                int MFLen = blockSize * 128;
                if (maxThreads == null) { maxThreads = int.MaxValue; }

                if (!IsPositivePowerOf2(cost))
                { throw new ArgumentOutOfRangeException("cost", "Cost must be a positive power of 2."); }
                if (blockSize < 1 || blockSize > int.MaxValue / 128) throw new ArgumentOutOfRangeException("blockSize");
                if (parallel < 1 || blockSize > int.MaxValue / MFLen) throw new ArgumentOutOfRangeException("parallel");
                if (maxThreads < 1) throw new ArgumentOutOfRangeException("maxThreads");

                byte[] B = Pbkdf2.ComputeDerivedKey(new HMACSHA256(P), S, 1, parallel * MFLen);

                uint[] B0 = new uint[B.Length / 4];
                for (int i = 0; i < B0.Length; i++)
                {
                    B0[i] = (uint)B[i * 4 + 3] << 24 |
                            (uint)B[i * 4 + 2] << 16 |
                            (uint)B[i * 4 + 1] << 8 |
                            (uint)B[i * 4 + 0];
                } // code is easier with uint[]
                ThreadSMixCalls(B0, MFLen, cost, blockSize, parallel, (int)maxThreads);
                for (int i = 0; i < B0.Length; i++)
                {
                    B[i * 4 + 3] = (byte)(B0[i] >> 24);
                    B[i * 4 + 2] = (byte)(B0[i] >> 16);
                    B[i * 4 + 1] = (byte)(B0[i] >> 8);
                    B[i * 4 + 0] = (byte)(B0[i]);
                }
                Array.Clear(B0, 0, B0.Length);

                return B;
            }

            static void ThreadSMixCalls(uint[] B0, int MFLen,
                                        int cost, int blockSize, int parallel, int maxThreads)
            {
                int current = 0;
                ThreadStart workerThread = delegate ()
                {
                    while (true)
                    {
                        int j = Interlocked.Increment(ref current) - 1;
                        if (j >= parallel) { break; }

                        SMix(B0, j * MFLen / 4, B0, j * MFLen / 4, (uint)cost, blockSize);
                    }
                };

                int threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, Math.Min(maxThreads, parallel)));
                Thread[] threads = new Thread[threadCount - 1];
                for (int i = 0; i < threads.Length; i++) { (threads[i] = new Thread(workerThread, 8192)).Start(); }
                workerThread();
                for (int i = 0; i < threads.Length; i++) { threads[i].Join(); }
            }

            static void SMix(uint[] B, int Boffset, uint[] Bp, int Bpoffset, uint N, int r)
            {
                uint Nmask = N - 1; int Bs = 16 * 2 * r;
                uint[] scratch1 = new uint[16];
                uint[] scratchX = new uint[16], scratchY = new uint[Bs];
                uint[] scratchZ = new uint[Bs];

                uint[] x = new uint[Bs]; uint[][] v = new uint[N][];
                for (int i = 0; i < v.Length; i++) { v[i] = new uint[Bs]; }

                Array.Copy(B, Boffset, x, 0, Bs);
                for (uint i = 0; i < N; i++)
                {
                    Array.Copy(x, v[i], Bs);
                    BlockMix(x, 0, x, 0, scratchX, scratchY, scratch1, r);
                }
                for (uint i = 0; i < N; i++)
                {
                    uint j = x[Bs - 16] & Nmask; uint[] vj = v[j];
                    for (int k = 0; k < scratchZ.Length; k++) { scratchZ[k] = x[k] ^ vj[k]; }
                    BlockMix(scratchZ, 0, x, 0, scratchX, scratchY, scratch1, r);
                }
                Array.Copy(x, 0, Bp, Bpoffset, Bs);

                for (int i = 0; i < v.Length; i++) { Array.Clear(v[i], 0, v[i].Length); }
                Array.Clear(v, 0, v.Length);
                Array.Clear(x, 0, x.Length);
                Array.Clear(scratchX, 0, scratchX.Length);
                Array.Clear(scratchY, 0, scratchY.Length);
                Array.Clear(scratchZ, 0, scratchZ.Length);
                Array.Clear(scratch1, 0, scratch1.Length);
            }

            static void BlockMix
                (uint[] B,        // 16*2*r
                 int Boffset,
                 uint[] Bp,       // 16*2*r
                 int Bpoffset,
                 uint[] x,        // 16
                 uint[] y,        // 16*2*r -- unnecessary but it allows us to alias B and Bp
                 uint[] scratch,  // 16
                 int r)
            {
                int k = Boffset, m = 0, n = 16 * r;
                Array.Copy(B, (2 * r - 1) * 16, x, 0, 16);

                for (int i = 0; i < r; i++)
                {
                    for (int j = 0; j < scratch.Length; j++) { scratch[j] = x[j] ^ B[j + k]; }
                    Secretbox.Secretbox sala = new Secretbox.Secretbox();
                    sala.SalaCore208(scratch, x);
                    Array.Copy(x, 0, y, m, 16);
                    k += 16;

                    for (int j = 0; j < scratch.Length; j++) { scratch[j] = x[j] ^ B[j + k]; }
                    sala.SalaCore208(scratch, x);
                    Array.Copy(x, 0, y, m + n, 16);
                    k += 16;

                    m += 16;
                }

                Array.Copy(y, 0, Bp, Bpoffset, y.Length);
            }
        }

        /// <summary>
        /// Implements the PBKDF2 key derivation function.
        /// </summary>
        /// 
        /// <example>
        /// <code title="Computing a Derived Key">
        /// using System.Security.Cryptography;
        /// using CryptSharp.Utility;
        /// 
        /// // Compute a 128-byte derived key using HMAC-SHA256, 1000 iterations, and a given key and salt.
        /// byte[] derivedKey = Pbkdf2.ComputeDerivedKey(new HMACSHA256(key), salt, 1000, 128);
        /// </code>
        /// <code title="Creating a Derived Key Stream">
        /// using System.IO;
        /// using System.Security.Cryptography;
        /// using CryptSharp.Utility;
        ///
        /// // Create a stream using HMAC-SHA512, 1000 iterations, and a given key and salt.
        /// Stream derivedKeyStream = new Pbkdf2(new HMACSHA512(key), salt, 1000);
        /// </code>
        /// </example>
        public class Pbkdf2 : Stream
        {
            #region PBKDF2
            byte[] _saltBuffer, _digest, _digestT1;
            KeyedHashAlgorithm _hmacAlgorithm;
            int _iterations;

            /// <summary>
            /// Creates a new PBKDF2 stream.
            /// </summary>
            /// <param name="hmacAlgorithm">
            ///     The HMAC algorithm to use, for example <see cref="HMACSHA256"/>.
            ///     Make sure to set <see cref="KeyedHashAlgorithm.Key"/>.
            /// </param>
            /// <param name="salt">
            ///     The salt.
            ///     A unique salt means a unique PBKDF2 stream, even if the original key is identical.
            /// </param>
            /// <param name="iterations">The number of iterations to apply.</param>
            public Pbkdf2(KeyedHashAlgorithm hmacAlgorithm, byte[] salt, int iterations)
            {
                if (hmacAlgorithm == null) throw new ArgumentNullException("hmacAlgorithm");
                if (salt == null) throw new ArgumentNullException("salt");
                if (salt.Length > int.MaxValue - 4) throw new ArgumentOutOfRangeException("salt");
                if (iterations < 1) throw new ArgumentOutOfRangeException("iterations");
                if (hmacAlgorithm.HashSize == 0 || hmacAlgorithm.HashSize % 8 != 0)
                { throw new ArgumentOutOfRangeException("hmacAlgorithm", "Unsupported hash size."); }

                int hmacLength = hmacAlgorithm.HashSize / 8;
                _saltBuffer = new byte[salt.Length + 4]; Array.Copy(salt, _saltBuffer, salt.Length);
                _iterations = iterations; _hmacAlgorithm = hmacAlgorithm;
                _digest = new byte[hmacLength]; _digestT1 = new byte[hmacLength];
            }

            /// <summary>
            /// Reads from the derived key stream.
            /// </summary>
            /// <param name="count">The number of bytes to read.</param>
            /// <returns>Bytes from the derived key stream.</returns>
            public byte[] Read(int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException("count");

                byte[] buffer = new byte[count];
                int bytes = Read(buffer, 0, count);
                if (bytes < count)
                {
                    throw new ArgumentOutOfRangeException("count", string.Format("Can only return {0} bytes.", bytes));
                }

                return buffer;
            }

            /// <summary>
            /// Computes a derived key.
            /// </summary>
            /// <param name="hmacAlgorithm">
            ///     The HMAC algorithm to use, for example <see cref="HMACSHA256"/>.
            ///     Make sure to set <see cref="KeyedHashAlgorithm.Key"/>.
            /// </param>
            /// <param name="salt">
            ///     The salt.
            ///     A unique salt means a unique derived key, even if the original key is identical.
            /// </param>
            /// <param name="iterations">The number of iterations to apply.</param>
            /// <param name="derivedKeyLength">The desired length of the derived key.</param>
            /// <returns>The derived key.</returns>
            public static byte[] ComputeDerivedKey(KeyedHashAlgorithm hmacAlgorithm, byte[] salt, int iterations,
                                                   int derivedKeyLength)
            {
                if (derivedKeyLength < 0) throw new ArgumentOutOfRangeException("derivedKeyLength");

                using (Pbkdf2 kdf = new Pbkdf2(hmacAlgorithm, salt, iterations))
                {
                    return kdf.Read(derivedKeyLength);
                }
            }

            /// <summary>
            /// Closes the stream, clearing memory and disposing of the HMAC algorithm.
            /// </summary>
            public override void Close()
            {
                Array.Clear(_saltBuffer, 0, _saltBuffer.Length);
                Array.Clear(_digest, 0, _digest.Length);
                Array.Clear(_digestT1, 0, _digestT1.Length);
                _hmacAlgorithm.Clear();
            }

            void ComputeBlock(uint pos)
            {
                _saltBuffer[_saltBuffer.Length - 4] = (byte)(pos >> 24);
                _saltBuffer[_saltBuffer.Length - 3] = (byte)(pos >> 16);
                _saltBuffer[_saltBuffer.Length - 2] = (byte)(pos >> 8);
                _saltBuffer[_saltBuffer.Length - 1] = (byte)(pos);
                ComputeHmac(_saltBuffer, _digestT1);
                Array.Copy(_digestT1, _digest, _digestT1.Length);

                for (int i = 1; i < _iterations; i++)
                {
                    ComputeHmac(_digestT1, _digestT1);
                    for (int j = 0; j < _digest.Length; j++) { _digest[j] ^= _digestT1[j]; }
                }

                Array.Clear(_digestT1, 0, _digestT1.Length);
            }

            void ComputeHmac(byte[] input, byte[] output)
            {
                _hmacAlgorithm.Initialize();
                _hmacAlgorithm.TransformBlock(input, 0, input.Length, input, 0);
                _hmacAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
                Array.Copy(_hmacAlgorithm.Hash, output, output.Length);
            }
            #endregion

            #region Stream
            long _blockStart, _blockEnd, _pos;

            /// <exclude />
            public override void Flush()
            {

            }

            /// <inheritdoc />
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException("buffer");
                if (offset < 0 || count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException("buffer", string.Format("Range [{0}, {1}) is outside array bounds [0, {2}).", offset, offset + count, buffer.Length));
                int bytes = 0;

                while (count > 0)
                {
                    if (Position < _blockStart || Position >= _blockEnd)
                    {
                        if (Position >= Length) { break; }

                        long pos = Position / _digest.Length;
                        ComputeBlock((uint)(pos + 1));
                        _blockStart = pos * _digest.Length;
                        _blockEnd = _blockStart + _digest.Length;
                    }

                    int bytesSoFar = (int)(Position - _blockStart);
                    int bytesThisTime = (int)Math.Min(_digest.Length - bytesSoFar, count);
                    Array.Copy(_digest, bytesSoFar, buffer, bytes, bytesThisTime);
                    count -= bytesThisTime; bytes += bytesThisTime; Position += bytesThisTime;
                }

                return bytes;
            }

            /// <inheritdoc />
            public override long Seek(long offset, SeekOrigin origin)
            {
                long pos;

                switch (origin)
                {
                    case SeekOrigin.Begin: pos = offset; break;
                    case SeekOrigin.Current: pos = Position + offset; break;
                    case SeekOrigin.End: pos = Length + offset; break;
                    default: throw new ArgumentOutOfRangeException("origin", "Unknown seek type.");
                }

                if (pos < 0) { throw new ArgumentException("Can't seek before the stream start.", "offset"); }
                Position = pos; return pos;
            }

            /// <exclude />
            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            /// <exclude />
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            /// <exclude />
            public override bool CanRead
            {
                get { return true; }
            }

            /// <exclude />
            public override bool CanSeek
            {
                get { return true; }
            }

            /// <exclude />
            public override bool CanWrite
            {
                get { return false; }
            }

            /// <summary>
            /// The maximum number of bytes that can be derived is 2^32-1 times the HMAC size.
            /// </summary>
            public override long Length
            {
                get { return (long)_digest.Length * uint.MaxValue; }
            }

            /// <summary>
            /// The position within the derived key stream.
            /// </summary>
            public override long Position
            {
                get { return _pos; }
                set
                {
                    if (_pos < 0) { throw new ArgumentException("Can't seek before the stream start."); }
                    _pos = value;
                }
            }
            #endregion
        }
    }
}
