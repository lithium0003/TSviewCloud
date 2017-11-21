using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace TS_packet
{
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
    struct TOT_transport_packet
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public byte sync_byte;
        public bool IsSync { get { return sync_byte == 0x47; } }

        [System.Runtime.InteropServices.FieldOffset(1)]
        private byte PID_hi_byte;
        [System.Runtime.InteropServices.FieldOffset(2)]
        private byte PID_lo_byte;
        public bool transport_error_indicator       { get { return (PID_hi_byte & 0x80) == 0x80; } }
        public bool payload_unit_start_indicator    { get { return (PID_hi_byte & 0x40) == 0x40; } }
        public bool transport_priority              { get { return (PID_hi_byte & 0x20) == 0x20; } }
        public UInt16 PID { get { return (UInt16)((PID_hi_byte & 0x1F) << 8 | PID_lo_byte); } }

        [System.Runtime.InteropServices.FieldOffset(3)]
        private byte counter_byte;
        public byte transport_scrambling_control    { get { return (byte)((counter_byte & 0xC0) >> 6); } }
        public byte adaptation_field_control        { get { return (byte)((counter_byte & 0x30) >> 4); } }
        public byte continuity_counter              { get { return (byte)(counter_byte & 0x0F); } }
        
        [System.Runtime.InteropServices.FieldOffset(4)]
        public byte pointer_next;

        [System.Runtime.InteropServices.FieldOffset(5)]
        public byte table_id;

        [System.Runtime.InteropServices.FieldOffset(6)]
        private byte section_length_hi_byte;
        [System.Runtime.InteropServices.FieldOffset(7)]
        private byte section_length_lo_byte;
        public UInt16 section_length { get { return (UInt16)((section_length_hi_byte & 0x0F) << 8 | section_length_lo_byte); } }

        [System.Runtime.InteropServices.FieldOffset(8)]
        private byte MJD_hi;
        [System.Runtime.InteropServices.FieldOffset(9)]
        private byte MJD_lo;
        public UInt16 MJD { get { return (UInt16)((MJD_hi << 8 | MJD_lo)); } }

        [System.Runtime.InteropServices.FieldOffset(10)]
        private byte hour_byte;
        public int hour { get { return ((hour_byte & 0xF0) >> 4) * 10 + (hour_byte & 0x0F); } }

        [System.Runtime.InteropServices.FieldOffset(11)]
        private byte min_byte;
        public int min { get { return ((min_byte & 0xF0) >> 4) * 10 + (min_byte & 0x0F); } }

        [System.Runtime.InteropServices.FieldOffset(12)]
        private byte sec_byte;
        public int sec { get { return ((sec_byte & 0xF0) >> 4) * 10 + (sec_byte & 0x0F); } }

        public bool IsTOT
        {
            get
            {
                return IsSync &&
                    payload_unit_start_indicator &&
                    adaptation_field_control == 0x01 &&
                    PID == 0x14 &&
                    pointer_next == 0 &&
                    table_id == 0x73;
            }
        }

        public DateTime JST_time
        {
            get
            {
                DateTime MJD_base = new DateTime(1858, 11, 17, 0, 0, 0);

                TimeSpan time = new TimeSpan(MJD, hour, min, sec);
                return MJD_base + time;
            }
        }


    }


}
