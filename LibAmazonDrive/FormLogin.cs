using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibAmazonDrive
{
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
            synchronizationContext = SynchronizationContext.Current;
            var appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
            SetIEKeyforWebBrowserControl(appName);
        }

        public FormLogin(TSviewCloudPlugin.AmazonDriveSystem server):this()
        {
            this.server = server;
        }

        TSviewCloudPlugin.AmazonDriveSystem server;

        AuthKeys key;
        CancellationTokenSource ct_soruce;
        string error_str;
        private readonly SynchronizationContext synchronizationContext;


        public AuthKeys Login(CancellationToken ct = default(CancellationToken))
        {
            string url = ConfigAPI.AmazonAPI_login + "?" +
                "client_id=" + ConfigAPI.client_id + "&" +
                "scope=" + "clouddrive%3Aread_all+clouddrive%3Awrite" + "&" +
                "response_type=" + "code" + "&" +
                "redirect_uri=" + ConfigAPI.App_redirect;
            ct_soruce = CancellationTokenSource.CreateLinkedTokenSource(ct);

            webBrowser1.Navigate(url);
            ShowDialog();
            return key;
        }

        private void FormLogin_FormClosing(object sender, FormClosingEventArgs e)
        {
            ct_soruce.Cancel();
        }

        private async void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            Text = e.Url.AbsoluteUri;
            var path = e.Url.AbsoluteUri;
            if (path.StartsWith(ConfigAPI.App_redirect))
            {
                if (string.IsNullOrEmpty(ConfigAPI.client_secret))
                    return;

                const string code_str = "?code=";
                var i = path.IndexOf(code_str);
                if (i < 0) return;

                string code = path.Substring(i + code_str.Length, path.IndexOf('&', i) - i - code_str.Length);
                await GetAuthorizationCode(code, ct_soruce.Token);

                if (key != null && key.access_token != "")
                {
                    webBrowser1.Navigate(ConfigAPI.LoginSuccess);
                    timer1.Enabled = true;
                }
            }
            if (path.StartsWith(ConfigAPI.App_GetToken))
            {
                try
                {
                    var body = webBrowser1.DocumentText;
                    //using (var f = File.OpenWrite(Path.Combine(Config.Config_BasePath,"test.log")))
                    //using (var sw = new StreamWriter(f))
                    //{
                    //    sw.Write(body);
                    //}
                    var i = body.IndexOf("{");
                    var j = body.IndexOf("}");
                    if (i < 0 || j < 0) return;

                    key = AmazonDrive.ParseAuthResponse(body.Substring(i, j - i));
                    // Save refresh_token
                    server.Refresh_Token = key.refresh_token;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    System.Diagnostics.Debug.WriteLine(error_str);
                }
                if (key != null && key.access_token != "")
                {
                    webBrowser1.Navigate(ConfigAPI.LoginSuccess);
                    timer1.Enabled = true;
                }
            }
        }

        private async Task GetAuthorizationCode(string access_code, CancellationToken ct = default(CancellationToken))
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.PostAsync(
                        ConfigAPI.AmazonAPI_token,
                        new FormUrlEncodedContent(new Dictionary<string, string>{
                            {"grant_type","authorization_code"},
                            {"code",access_code},
                            {"client_id",ConfigAPI.client_id},
                            {"client_secret",ConfigAPI.client_secret},
                            {"redirect_uri",Uri.EscapeUriString(ConfigAPI.App_redirect)},
                        }),
                        ct
                    );
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    // Above three lines can be replaced with new helper method in following line
                    // string body = await client.GetStringAsync(uri);
                    key = AmazonDrive.ParseAuthResponse(responseBody);

                    // Save refresh_token
                    server.Refresh_Token = key.refresh_token;
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    System.Diagnostics.Debug.WriteLine(error_str);
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    System.Diagnostics.Debug.WriteLine(error_str);
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            Close();
        }

        private void SetIEKeyforWebBrowserControl(string appName)
        {
            Microsoft.Win32.RegistryKey regkey = null;
            try
            {
                regkey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION");
                if (regkey == null) return;

                regkey.SetValue(appName, unchecked(0x2AF8), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Application Settings Failed");
                MessageBox.Show(ex.Message);
            }
            finally
            {
                // Close the Registry
                regkey?.Close();
            }
        }
    }
}
