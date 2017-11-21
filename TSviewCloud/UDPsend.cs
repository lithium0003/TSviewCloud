using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TSviewCloudConfig;

namespace TSviewCloud
{
    public class SenderBreakCanceledException : Exception
    {
        public long bytePerSec;
        public long WaitForByte;
        public DateTime InitialTOT;

        public SenderBreakCanceledException(long bytePerSec, long WaitForByte, DateTime InitialTOT) : base()
        {
            this.bytePerSec = bytePerSec;
            this.WaitForByte = WaitForByte;
            this.InitialTOT = InitialTOT;
        }
    }

    public class PlayEOF_CanceledException : Exception
    {
    }

    public class TOTChangeEventArgs : EventArgs
    {
        public long Position;
        public DateTime initialTOT;
        public DateTime TOT_JST;
        public long bytePerSec;
    }

    public delegate void TOTchangeEventHandler(object sender, TOTChangeEventArgs e);

    public class UDP_TS_Stream : Stream
    {
        UDPSender udp;
        long _Position;
        long prepro_Pos;
        MemoryStream inbufer;
        CancellationToken ct;
        bool init_TOT;
        long offsetTOT;
        DateTime StartTime;
        DateTime InitialTime;
        const int packetlen = 188 * 1024;
        byte[] packet = new byte[packetlen];
        int packet_last;
        long bytecounter;
        long bytePerSec;
        long ToWaitByte;
        event TOTchangeEventHandler _Handler;
        Queue<Task> SendTasks = new Queue<Task>();
        const int SendTaskMax = 1;
        
        public UDP_TS_Stream(CancellationToken cancellationToken = default(CancellationToken))
        {
            _Position = 0;
            prepro_Pos = 0;
            inbufer = new MemoryStream();
            ct = cancellationToken;
            init_TOT = false;
            StartTime = DateTime.Now;
            InitialTime = default(DateTime);
            packet_last = 0;
            bytecounter = 0;
            bytePerSec = 0;
            ToWaitByte = 0;
        }

        public TimeSpan SendDuration = default(TimeSpan);
        public DateTime SendStartTime = default(DateTime);
        public TimeSpan SendDelay = default(TimeSpan);
        public long CancelForTooLess = 200 * 1024 * 1024;
        public event TOTchangeEventHandler TOTChangeHander
        {
            add
            {
                if (udp != null)
                    udp.TOTChangeHander += value;
                else
                    _Handler += value;
            }
            remove
            {
                if (udp != null)
                    udp.TOTChangeHander -= value;
                else
                    _Handler -= value;
            }
        }

        public override long Length { get { throw new NotSupportedException("not supported Length"); } }
        public override bool CanRead { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override void Flush() { }

        public override long Position
        {
            get
            {
                return _Position;
            }
            set
            {
                throw new NotSupportedException("not supported SetPosition");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("not supported Read");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("not supported seek");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("not supported SetLength");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _Position += (count - offset);
            if (udp != null)
            {
                udp.SendPacket(buffer, offset, count);
                return;
            }
            else
            {
                inbufer.Write(buffer, offset, count);
            }

            if (bytePerSec == 0)
            {
                var tmppos = inbufer.Position;
                inbufer.Position = prepro_Pos;
                bytePerSec = Calc1secBytes(inbufer);
                prepro_Pos = inbufer.Position;
                inbufer.Position = tmppos;

                if (bytePerSec == 0)
                {
                    // まだバッファが足りないので次を待つ
                    return;
                }

                if (SendStartTime != default(DateTime))
                {
                    // 開始時間指定
                    if (SendStartTime > InitialTime)
                    {
                        ToWaitByte = (long)((SendStartTime - InitialTime).TotalSeconds * bytePerSec * 0.9);
                        if (ToWaitByte > _Position - offsetTOT && ToWaitByte > CancelForTooLess)
                        {
                            throw new SenderBreakCanceledException(bytePerSec, ToWaitByte + offsetTOT, InitialTime);
                        }
                    }
                }
                else if (SendDelay != default(TimeSpan))
                {
                    // 開始遅れ時間指定
                    ToWaitByte = (long)(SendDelay.TotalSeconds * bytePerSec * 0.9);
                    if (ToWaitByte > _Position && ToWaitByte - _Position > CancelForTooLess)
                    {
                        throw new SenderBreakCanceledException(bytePerSec, ToWaitByte, InitialTime);
                    }
                }
            }

            inbufer.Position = 0;
            if (SendStartTime != default(DateTime))
            {
                // 開始時間指定
                if (SendStartTime > InitialTime)
                {
                    if (SeekToTOTTime(inbufer) < SendStartTime)
                    {
                        // まだ
                        inbufer.SetLength(0);
                        inbufer.Position = 0;
                        return;
                    }
                }
            }
            else if (SendDelay != default(TimeSpan))
            {
                // 開始遅れ時間指定
                if (ToWaitByte > _Position)
                {
                    // まだ
                    inbufer.SetLength(0);
                    inbufer.Position = 0;
                    return;
                }
                var remlen = _Position - ToWaitByte;
                inbufer.Position = inbufer.Length - remlen;
            }

            if (udp == null)
            {
                udp = new UDPSender(bytePerSec, InitialTime, ct);
                udp.TOTChangeHander += _Handler;
                udp.SendDuration = SendDuration;
                udp.SendPacket(inbufer);
                inbufer.SetLength(0);
                inbufer.Position = 0;
            }
        }

        public DateTime SeekToTOTTime(Stream data)
        {
            DateTime ret = default(DateTime);

            packet_last = 0;
            var packet_length = 0;

            var data_remain = data.Length - data.Position;
            while (data_remain > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (data_remain + packet_last < packetlen)
                {
                    int len = data.Read(packet, packet_last, (int)data_remain);
                    data_remain -= len;
                    packet_last += len;
                    packet_length = packet_last;
                }
                else
                {
                    int len2 = data.Read(packet, packet_last, packetlen - packet_last);
                    data_remain -= len2;
                    packet_last += len2;
                    if (packet_last >= packetlen)
                    {
                        packet_last -= packetlen;
                        packet_length = packetlen;
                    }
                }

                GCHandle gch = GCHandle.Alloc(packet, GCHandleType.Pinned);
                try
                {
                    for (var j = 0; j < packet_length; j += 188)
                    {
                        var TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                        while (!TOT.IsSync && ++j < packetlen)
                        {
                            TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                        }
                        if (TOT.IsTOT)
                        {
                            ret = TOT.JST_time;
                            if (ret > SendStartTime)
                            {
                                try
                                {
                                    data.Position -= (long)(bytePerSec * (ret - SendStartTime).TotalSeconds / 188) * 188;
                                }
                                catch
                                {
                                    data.Position = 0;
                                }
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    gch.Free();
                }
            }
            return ret;
        }

        public long Calc1secBytes(Stream data)
        {
            long ret = 0;
            var data_remain = data.Length - data.Position;
            while (data_remain > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (data_remain + packet_last < packetlen)
                {
                    int len = data.Read(packet, packet_last, (int)data_remain);
                    packet_last += len;
                    break;
                }
                int len2 = data.Read(packet, packet_last, packetlen - packet_last);
                data_remain -= len2;
                packet_last += len2;
                if (packet_last >= packetlen) packet_last -= packetlen;

                int offset = 0;
                GCHandle gch = GCHandle.Alloc(packet, GCHandleType.Pinned);
                try
                {
                    for (var j = 0; j < packetlen; j += 188)
                    {
                        var TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                        while (!TOT.IsSync && ++j < packetlen)
                        {
                            TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                            offset = j;
                        }
                        if (TOT.IsTOT)
                        {
                            if (!init_TOT)
                            {
                                InitialTime = TOT.JST_time;
                                init_TOT = true;
                                offsetTOT = bytecounter;
                                bytecounter = 0;
                            }
                            var sendspent = TOT.JST_time - InitialTime;

                            if (sendspent >= TimeSpan.FromSeconds(10))
                            {
                                ret = (Int64)(bytecounter / sendspent.TotalSeconds);
                            }
                        }
                        bytecounter += 188;
                    }
                }
                finally
                {
                    gch.Free();
                }
                packet_last = offset;
                if (ret > 0) return ret;
            }
            return 0;
        }

        public class UDPSender
        {
            class Time_posision
            {
                public DateTime Time;
                public long Position;
                public Time_posision(DateTime Time, long Position)
                {
                    this.Time = Time;
                    this.Position = Position;
                }
            }
            Queue<Time_posision> TOT_log = new Queue<Time_posision>();
            Time_posision initialTOT;
            DateTime InitialTOT_time;
            Time_posision prevTOT;
            Queue<Time_posision> Send_log = new Queue<Time_posision>();
            Time_posision prevSendTime;
            TimeSpan DelayforSend = TimeSpan.FromMilliseconds(0);
            double waitskipforDelay = 0;

            CancellationToken ct;
            System.Net.Sockets.UdpClient udp;
            int packetlen = 188 * ConfigTSsend.SendPacketNum;
            byte[] packet;
            int packet_last;
            bool init_TOT;
            long Position;
            long bytePerSec;
            int synccount;
            long SendBytes;
            TOTChangeEventArgs HandlerArg = new TOTChangeEventArgs();

            public TimeSpan SendDuration = default(TimeSpan);

            public event TOTchangeEventHandler TOTChangeHander;

            public UDPSender(long bytePsec, DateTime initTOT, CancellationToken cancellationToken = default(CancellationToken))
            {
                Position = 0;
                bytePerSec = bytePsec;
                ct = cancellationToken;
                udp = new System.Net.Sockets.UdpClient();
                packet_last = 0;
                init_TOT = false;
                synccount = 0;
                SendBytes = 0;
                HandlerArg.bytePerSec = bytePsec;
                HandlerArg.initialTOT = initTOT;
                prevSendTime = new Time_posision(DateTime.Now, 0);
                packet = new byte[packetlen];
            }

            private bool FillBuffer(Stream data)
            {
                var data_remain = data.Length - data.Position;
                if (data_remain > 0)
                {
                    int len = data.Read(packet, packet_last, packetlen - packet_last);
                    packet_last += len;
                    if (packet_last == packetlen)
                    {
                        packet_last = 0;
                        return true;
                    }
                    else
                        return false;
                }
                return false;
            }

            private bool FillBuffer(byte[] buffer, ref int offset, ref int count)
            {
                if (count > 0)
                {
                    if (count + packet_last < packetlen)
                    {
                        Array.Copy(buffer, offset, packet, packet_last, count);
                        packet_last += count;
                        offset += count;
                        count = 0;
                        return false;
                    }
                    int filllen = packetlen - packet_last;
                    Array.Copy(buffer, offset, packet, packet_last, filllen);
                    offset += filllen;
                    count -= filllen;
                    packet_last = 0;
                    return true;
                }
                return false;
            }

            private void SendUDP()
            {
                if (synccount < 5) return;

                ct.ThrowIfCancellationRequested();
                while (Send_log.Count > ConfigTSsend.SendRatebySendCount)
                    prevSendTime = Send_log.Dequeue();

                var rate = (SendBytes - prevSendTime.Position) / (DateTime.Now - prevSendTime.Time).TotalSeconds;
                if (rate > bytePerSec)
                {
                    var slp = ((SendBytes - prevSendTime.Position) - bytePerSec * (DateTime.Now - prevSendTime.Time).TotalSeconds) / bytePerSec;
                    slp += ConfigTSsend.SendDelay * 0.001;
                    slp *= 1000;
                    if(waitskipforDelay > 0)
                    {
                        if(waitskipforDelay > slp)
                        {
                            waitskipforDelay -= slp;
                            DelayforSend -= TimeSpan.FromMilliseconds(slp);
                            slp = 0;
                        }
                        else
                        {
                            slp -= waitskipforDelay;
                            DelayforSend -= TimeSpan.FromMilliseconds(waitskipforDelay);
                            waitskipforDelay = 0;
                        }
                    }
                    if (slp > 0)
                    {
                        Thread.Sleep((int)slp);
                    }
                }

                udp.Send(packet, packetlen, ConfigTSsend.SendToHost, ConfigTSsend.SendToPort);
                SendBytes += packetlen;
                Send_log.Enqueue(new Time_posision(DateTime.Now, SendBytes));

                if (init_TOT)
                {
                    var StreamTime = prevTOT.Time - initialTOT.Time + TimeSpan.FromSeconds((SendBytes - prevTOT.Position) / bytePerSec);
                    var RealTime = DateTime.Now - InitialTOT_time;
                    if (RealTime - DelayforSend - TimeSpan.FromMilliseconds(ConfigTSsend.SendLongOffset) > StreamTime)
                    {
                        DelayforSend = RealTime - StreamTime;
                        waitskipforDelay = DelayforSend.TotalMilliseconds;
                        if (waitskipforDelay > ConfigTSsend.SendLongOffset) waitskipforDelay = ConfigTSsend.SendLongOffset;

                        System.Diagnostics.Debug.WriteLine("delay {0} {1}", DelayforSend, waitskipforDelay);
                    }
                    if (StreamTime > RealTime - DelayforSend + TimeSpan.FromMilliseconds(ConfigTSsend.SendLongOffset))
                    {
                        var slp = StreamTime - (RealTime - DelayforSend + TimeSpan.FromMilliseconds(ConfigTSsend.SendLongOffset));
                        if (slp.TotalMilliseconds > 0)
                        {
                            Thread.Sleep(slp);
                        }
                    }
                }
            }

            private void checkTOT()
            {
                int offset = 0;
                GCHandle gch = GCHandle.Alloc(packet, GCHandleType.Pinned);
                try
                {
                    for (var j = 0; j < packetlen; j += 188)
                    {
                        Position += 188;
                        ct.ThrowIfCancellationRequested();
                        var TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                        if (!TOT.IsSync)
                        {
                            synccount = 0;
                            while (!TOT.IsSync && ++j < packetlen)
                            {
                                TOT = (TS_packet.TOT_transport_packet)Marshal.PtrToStructure(gch.AddrOfPinnedObject() + j, typeof(TS_packet.TOT_transport_packet));
                                offset = j;
                            }
                            if (!TOT.IsSync)
                            {
                                offset = 0;
                            }
                        }
                        if (TOT.IsSync && ++synccount > 5)
                        {
                            synccount = 5;
                        }
                        if (TOT.IsTOT)
                        {
                            if (!init_TOT)
                            {
                                initialTOT = new Time_posision(TOT.JST_time, Position);
                                InitialTOT_time = DateTime.Now;
                                prevTOT = initialTOT;
                                TOT_log.Enqueue(initialTOT);
                                init_TOT = true;
                            }
                            else
                            {
                                TOT_log.Enqueue(new Time_posision(TOT.JST_time, Position));
                                while (TOT_log.Count > ConfigTSsend.SendRatebyTOTCount)
                                    prevTOT = TOT_log.Dequeue();
                                bytePerSec = (long)((Position - prevTOT.Position) / (TOT.JST_time - prevTOT.Time).TotalSeconds);
                            }
                            HandlerArg.TOT_JST = TOT.JST_time;
                            HandlerArg.Position = Position;
                            HandlerArg.bytePerSec = bytePerSec;
                            TOTChangeHander?.Invoke(this, HandlerArg);

                            var sendspent = TOT.JST_time - initialTOT.Time;
                            if (SendDuration != default(TimeSpan) && sendspent >= SendDuration)
                            {
                                throw new PlayEOF_CanceledException();
                            }
                        }
                    }
                }
                finally
                {
                    gch.Free();
                }
                if (offset > 0)
                {
                    byte[] syncpacket = new byte[packetlen];
                    packet_last = packetlen - offset;
                    Array.Copy(packet, offset, syncpacket, 0, packet_last);
                    packet = syncpacket;
                }
            }

            public void SendPacket(Stream data)
            {
                while (data.Position < data.Length)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!FillBuffer(data)) continue;
                    SendUDP();
                    checkTOT();
                }
            }

            public void SendPacket(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!FillBuffer(buffer, ref offset, ref count)) continue;
                    SendUDP();
                    checkTOT();
                }
            }
        }
    }
}
