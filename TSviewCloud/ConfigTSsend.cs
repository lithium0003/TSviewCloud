using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TSviewCloudConfig
{
    class ConfigTSsend
    {
        public static string SendToHost = "localhost";
        public static int SendToPort = 1240;
        public static int SendPacketNum = 32;
        public static int SendDelay = 0;
        public static int SendLongOffset = 2000;
        public static int SendRatebySendCount = 5;
        public static int SendRatebyTOTCount = 1;
        public static System.Windows.Forms.Keys SendVK = default(System.Windows.Forms.Keys);
        public static string SendVK_Application = "";

    }

    [DataContract]
    class SavedataTSsend
    {
        [DataMember]
        public string SendToHost;
        [DataMember]
        public int SendToPort;
        [DataMember]
        public int SendPacketNum;
        [DataMember]
        public int SendDelay;
        [DataMember]
        public int SendLongOffset;
        [DataMember]
        public int SendRatebySendCount;
        [DataMember]
        public int SendRatebyTOTCount;
        [DataMember]
        public System.Windows.Forms.Keys SendVK;
        [DataMember]
        public string SendVK_Application;
    }
}