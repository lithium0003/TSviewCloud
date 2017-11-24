using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibAmazonDrive
{
    public class ConfigAPI
    {
        readonly static string client_id_enc = "YW16bjEuYXBwbGljYXRpb24tb2EyLWNsaWVudC45ZTEyZjdlMGY5ZWI0ZWI2OTUwOWRjNjI3MmVhNTBlMQ==";
        readonly static string client_secret_enc = SecretKeys.HiddenKey.AmazonDrive;

        public static string client_id
        {
            get { return Encoding.ASCII.GetString(Convert.FromBase64String(client_id_enc)); }
        }
        public static string client_secret
        {
            get { return Encoding.ASCII.GetString(Convert.FromBase64String(client_secret_enc)); }
        }
 
        public const string App_redirect = "https://lithium03.info/login/redirect";
        public const string App_GetToken = "https://lithium03.info/login/token";
        public const string LoginSuccess = "https://lithium03.info/login/login_success.html";
        public const string App_RefreshToken = "https://lithium03.info/login/refresh";

        public const string AmazonAPI_login = "https://www.amazon.com/ap/oa";
        public const string AmazonAPI_token = "https://api.amazon.com/auth/o2/token";
        public const string getEndpoint = "https://drive.amazonaws.com/drive/v1/account/endpoint";

    }
}
