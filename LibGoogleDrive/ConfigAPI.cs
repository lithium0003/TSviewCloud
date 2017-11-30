using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibGoogleDrive
{
    public class ConfigAPI
    {
        public static string oauth_uri = "https://accounts.google.com/o/oauth2/v2/auth";
        public static string token_uri = "https://www.googleapis.com/oauth2/v4/token";
        public static string drive_uri = "https://www.googleapis.com/drive/v3";
        public static string upload_uri = "https://www.googleapis.com/upload/drive/v3";
        public const string App_GetToken = "https://lithium03.info/login/google/token";
        public const string App_RefreshToken = "https://lithium03.info/login/google/refresh";

        public static string redirect_uri;

        readonly static string client_id_enc = "MTEwNzg0NTgyNzIyLWZubmRmbzFscGtkdWs5YXQ2Z2JzYmpyZ2RyZTQwNjVzLmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29t";
        readonly static string client_id_web_enc = "MTEwNzg0NTgyNzIyLTh1YWp2ZW4zZzl0Z21wYTFsY2szM2FtYTBvZDlqZnZ1LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29t";
        readonly static string client_secret_enc = SecretKeys.HiddenKey.GoogleDrive;

        public static string client_id
        {
            get { return Encoding.ASCII.GetString(Convert.FromBase64String(client_id_enc)); }
        }
        public static string client_id_web
        {
            get { return Encoding.ASCII.GetString(Convert.FromBase64String(client_id_web_enc)); }
        }
        public static string client_secret
        {
            get { return Encoding.ASCII.GetString(Convert.FromBase64String(client_secret_enc)); }
        }
    }
}
