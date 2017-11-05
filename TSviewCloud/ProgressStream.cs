using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TSviewCloudPlugin
{
    public class PositionChangeEventArgs : EventArgs
    {
        public long Length;
        public long Position;
        public double Rate;
        public string Log;
    }

    public delegate void PoschangeEventHandler(object sender, PositionChangeEventArgs e);

    /// <summary>
    /// stream with progress
    /// </summary>
    public class PositionStream : Stream
    {
        Stream innerStream;
        DateTime PrevTime = DateTime.Now;
        double Rate_mean;
        long tPosition;
        bool stream_mode;

        public PositionStream(Stream s) : base()
        {
            innerStream = s;
            data.Position = innerStream.Position;
            data.Length = innerStream.Length;
            data.Rate = 0;
            Rate_mean = 0;
            stream_mode = false;
        }
        public PositionStream(Stream s, long StreamSize, long? Position = null) : base()
        {
            innerStream = s;
            data.Position = tPosition = Position ?? 0;
            data.Length = StreamSize;
            data.Rate = 0;
            Rate_mean = 0;
            stream_mode = true;
        }
        public event PoschangeEventHandler PosChangeEvent;
        private PositionChangeEventArgs data = new PositionChangeEventArgs();

        private string ConvertUnit(double rate)
        {
            if (rate < 1024)
                return string.Format("{0:#,0.00}Byte/s", rate);
            if (rate < 1024 * 1024)
                return string.Format("{0:#,0.00}KiB/s", rate / 1024);
            if (rate < 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}MiB/s", rate / 1024 / 1024);
            if (rate < (double)1024 * 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}GiB/s", rate / 1024 / 1024 / 1024);
            if (rate < (double)1024 * 1024 * 1024 * 1024 * 1024)
                return string.Format("{0:#,0.00}TiB/s", rate / 1024 / 1024 / 1024 / 1024);
            return string.Format("{0:#,0.00}PiB/s", rate / 1024 / 1024 / 1024 / 1024);
        }

        protected void DoEvent()
        {
            if ((DateTime.Now - PrevTime).TotalSeconds > 1)
            {
                long Prev = data.Position;
                if (stream_mode)
                    data.Position = tPosition;
                else
                    data.Position = innerStream.Position;

                data.Rate = (double)(data.Position - Prev) / (DateTime.Now - PrevTime).TotalSeconds;
                PrevTime = DateTime.Now;
                Rate_mean = (Rate_mean + data.Rate) / 2;
                var togo = Math.Round((data.Length - data.Position) / Rate_mean);
                togo = (double.IsInfinity(togo)) ? 0 : togo;
                data.Log = data.Position.ToString("#,0") + '/' + data.Length.ToString("#,0")
                            + string.Format("({0:0.00%}) ", (double)data.Position / data.Length)
                            + ConvertUnit(Rate_mean)
                            + " [to go " + TimeSpan.FromSeconds(togo).ToString() + " ] ";
                try
                {
                    PosChangeEvent?.Invoke(this, data);
                }
                catch { }
            }
        }

        public override long Length { get { return innerStream.Length; } }
        public override bool CanRead { get { return innerStream.CanRead; } }
        public override bool CanWrite { get { return innerStream.CanWrite; } }
        public override bool CanSeek { get { return innerStream.CanSeek; } }
        public override void Flush() { innerStream.Flush(); }
        public override int ReadTimeout
        {
            get
            {
                return innerStream.ReadTimeout;
            }

            set
            {
                innerStream.ReadTimeout = value;
            }
        }

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
            int ret = innerStream.Read(buffer, offset, count);
            tPosition += ret;
            DoEvent();
            return ret;
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
            data.Length = innerStream.Length;
            DoEvent();
        }
    }

    public class LoggingHandler : DelegatingHandler
    {
        public LoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine("Request:");
            System.Diagnostics.Debug.WriteLine(request.ToString());
            if (request.Content != null)
            {
                System.Diagnostics.Debug.WriteLine(await request.Content.ReadAsStringAsync());
            }
            System.Diagnostics.Debug.WriteLine("");

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            System.Diagnostics.Debug.WriteLine("Response:");
            System.Diagnostics.Debug.WriteLine(response.ToString());
            if (response.Content != null)
            {
                System.Diagnostics.Debug.WriteLine(await response.Content.ReadAsStringAsync());
            }
            System.Diagnostics.Debug.WriteLine("");

            return response;
        }
    }
}
