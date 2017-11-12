using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TSviewCloudConfig
{
    public class ConfigCarotDAV
    {
        public readonly static string[] CarotDAV_crypt_names = new string[]
        {
            "^_",
            ":D",
            ";)",
            "T-T",
            "orz",
            "ノシ",
            "（´・ω・）"
        };

        public static string CryptNameHeader = CarotDAV_crypt_names[0];
    }

    [DataContract]
    class SavedataCryptCarotDAV
    {
        [DataMember]
        public string CryptNameHeader;
    }

}
