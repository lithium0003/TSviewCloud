using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibGoogleDrive
{
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
        }

        public FormLogin(TSviewCloudPlugin.GoogleDriveSystem server) : this()
        {
            this.server = server;
        }

        TSviewCloudPlugin.GoogleDriveSystem server;

        string Get_code_challenge()
        {
            string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

            var rnd = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var buf = new byte[64];

            rnd.GetBytes(buf);
            return string.Join("", buf.Select(x => charset[x % charset.Length]));
        }


        public AuthKeys Login(CancellationToken ct = default(CancellationToken))
        {
            var response_type = "code";
            var scope = "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/drive.metadata";
            var code_challenge_method = "plain";
            string url;
            code_challenge = Get_code_challenge();

            if (string.IsNullOrEmpty(ConfigAPI.client_secret))
            {
                // secret key is absent.
                // redirect to web and proxy call

                ConfigAPI.redirect_uri = "https://lithium03.info/login/google/redirect";

                url = ConfigAPI.oauth_uri
                    + "?scope=" + Uri.EscapeUriString(scope)
                    + "&access_type=offline"
                    + "&include_granted_scopes=true"
                    + "&response_type=" + response_type
                    + "&redirect_uri=" + Uri.EscapeUriString(ConfigAPI.redirect_uri)
                    + "&client_id=" + ConfigAPI.client_id_web;
            }
            else
            {
                // secret key exists.
                // redirect to local

                LocalServer = StartHttpServer();

                url = ConfigAPI.oauth_uri
                    + "?scope=" + Uri.EscapeUriString(scope)
                    + "&response_type=" + response_type
                    + "&redirect_uri=" + Uri.EscapeUriString(ConfigAPI.redirect_uri)
                    + "&client_id=" + ConfigAPI.client_id
                    + "&code_challenge_method=" + code_challenge_method
                    + "&code_challenge=" + code_challenge;
            }
            ct_soruce = CancellationTokenSource.CreateLinkedTokenSource(ct);

            webBrowser1.Navigate(url);
            ShowDialog();
            return key;
        }

        string code_challenge;

        private CancellationTokenSource ct_soruce;
        private Task LocalServer;
        private AuthKeys key;

        private Task StartHttpServer()
        {
            List<int> usedPorts = new List<int>();
            Random r = new Random();

            HttpListener listener;
            int newPort = -1;
            string redirecturl;
            while (true)
            {
                listener = new HttpListener();
                newPort = r.Next(49152, 65535); // IANA suggests the range 49152 to 65535 for dynamic or private ports.
                if (usedPorts.Contains(newPort))
                {
                    continue;
                }
                redirecturl = string.Format("http://127.0.0.1:{0}/", newPort);
                listener.Prefixes.Add(redirecturl);
                usedPorts.Add(newPort);
                try
                {
                    listener.Start();
                }
                catch
                {
                    continue;
                }
                break;
            }

            ConfigAPI.redirect_uri = redirecturl;
            return Task.Run(() =>
            {
                while (!ct_soruce.Token.IsCancellationRequested)
                {
                    IAsyncResult result = listener.BeginGetContext(
                        (asyncRes) =>
                        {
                            HttpListener listr = (HttpListener)asyncRes.AsyncState;
                            HttpListenerContext context = listr.EndGetContext(asyncRes);

                            HttpListenerRequest req = context.Request;
                            HttpListenerResponse res = context.Response;

                            Console.WriteLine(req.RawUrl);
                            res.Close();
                        }, listener);
                    WaitHandle.WaitAny(new[] { ct_soruce.Token.WaitHandle, result.AsyncWaitHandle });
                }
            });
        }

 
 
        async Task<AuthKeys> GetAccessToken(string code, CancellationToken ct = default(CancellationToken))
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.PostAsync(
                        ConfigAPI.token_uri,
                        new FormUrlEncodedContent(new Dictionary<string, string>{
                            {"grant_type","authorization_code"},
                            {"code",code},
                            {"client_id",ConfigAPI.client_id},
                            {"client_secret",ConfigAPI.client_secret},
                            {"redirect_uri",Uri.EscapeUriString(ConfigAPI.redirect_uri)},
                            {"code_verifier",code_challenge},
                        }),
                        ct
                    );
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    return GoogleDrive.ParseAuthResponse(responseBody);
                }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
            }
            return null;
        }

        private async void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            Text = e.Url.AbsoluteUri;
            var path = e.Url.AbsoluteUri;
            if (path.StartsWith(ConfigAPI.redirect_uri))
            {
                if (string.IsNullOrEmpty(ConfigAPI.client_secret))
                {
                    // secret key exists.
                    // get access token via remote.

                    return;
                }
                else
                {
                    // secret key exists.
                    // get access token from local.

                    const string code_str = "?code=";
                    var i = path.IndexOf(code_str);
                    if (i < 0) return;
                    var j = path.IndexOf('&', i);

                    var code = (j < 0) ? path.Substring(i + code_str.Length) : path.Substring(i + code_str.Length, j - (i + code_str.Length));
                    key = await GetAccessToken(code, ct_soruce.Token);

                    if (key != null && key.access_token != "")
                    {
                        server.Refresh_Token = key.refresh_token;

                        webBrowser1.DocumentText = "<html><body>Login success</body></html>";
                        timer1.Enabled = true;
                    }
                }
            }
            if (path.StartsWith(ConfigAPI.App_GetToken))
            {
                try
                {
                    var body = webBrowser1.DocumentText;
                    var i = body.IndexOf("{");
                    var j = body.IndexOf("}");
                    if (i < 0 || j < 0) return;

                    key = GoogleDrive.ParseAuthResponse(body.Substring(i, j - i));

                    if (key.refresh_token == null)
                    {
                        GoogleDrive.RevokeToken(key).Wait();
                        webBrowser1.DocumentText = "<html><body>Login failed. please retry</body></html>";
                        key.access_token = null;
                        return;
                    }

                    // Save refresh_token
                    server.Refresh_Token = key.refresh_token;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
                if (key != null && key.access_token != "")
                {
                    webBrowser1.DocumentText = "<html><body>Login success</body></html>";
                    timer1.Enabled = true;
                }
            }
        }

        private void FormLogin_FormClosing(object sender, FormClosingEventArgs e)
        {
            ct_soruce.Cancel();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            Close();
        }

    }
}
