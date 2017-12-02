using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibGoogleDrive
{
    [DataContract]
    public class Authentication_Info
    {
        [DataMember]
        public string token_type;
        [DataMember]
        public int expires_in;
        [DataMember]
        public string access_token;
        [DataMember]
        public string refresh_token;
    }

    public class AuthKeys
    {
        public string access_token;
        public string refresh_token;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [Serializable]
    [DataContract]
    public class FileMetadata_Info
    {
        [DataMember(EmitDefaultValue = false)]
        public string id;
        [DataMember(EmitDefaultValue = false)]
        public string mimeType;
        public bool IsFolder { get => mimeType == "application/vnd.google-apps.folder"; }
        [DataMember(EmitDefaultValue = false)]
        public string name;
        [DataMember(EmitDefaultValue = false)]
        public bool? trashed;
        [DataMember(EmitDefaultValue = false)]
        public string[] parents;

        [DataMember(Name = "viewedByMeTime", EmitDefaultValue = false)]
        public string viewedByMeTime_prop
        {
            get { return viewedByMeTime_str; }
            set
            {
                AccessDate = DateTime.Parse(value);
                viewedByMeTime_str = value;
            }
        }
        private string viewedByMeTime_str;
        public DateTime? AccessDate;

        [DataMember(Name = "modifiedTime", EmitDefaultValue = false)]
        public string modifiedTime_prop
        {
            get { return modifiedTime_str; }
            set
            {
                ModifiedDate = DateTime.Parse(value);
                modifiedTime_str = value;
            }
        }
        private string modifiedTime_str;
        public DateTime? ModifiedDate;

        [DataMember(Name = "createdTime", EmitDefaultValue = false)]
        public string createdTime_prop
        {
            get { return createdTime_str; }
            set
            {
                CreatedDate = DateTime.Parse(value);
                createdTime_str = value;
            }
        }
        private string createdTime_str;
        public DateTime? CreatedDate;

        [DataMember(EmitDefaultValue = false)]
        public string md5Checksum;
        [DataMember(EmitDefaultValue = false)]
        public long? size;
    }

    [DataContract]
    public class FileListdata_Info
    {
        [DataMember]
        public string nextPageToken;
        [DataMember]
        public bool? incompleteSearch;
        [DataMember]
        public FileMetadata_Info[] files;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////


    [DataContract]
    public class ChangesStartPageToken_Info
    {
        [DataMember]
        public string startPageToken;
    }

    [DataContract]
    public class Changesdata_Info
    {
        [DataMember]
        public string type;
        [DataMember]
        public string fileId;
        [DataMember]
        public FileMetadata_Info file;
        [DataMember]
        public bool? removed;
    }

    [DataContract]
    public class ChangesListdata_Info
    {
        [DataMember]
        public string nextPageToken;
        [DataMember]
        public string newStartPageToken;
        [DataMember]
        public Changesdata_Info[] changes;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class GoogleDrive
    {
        static public void Log(string func, string str = "")
        {
            TSviewCloudConfig.Config.Log.LogOut("\t[GoogleDrive(" + func + ")] " + str);
        }

        static public async Task RevokeToken(AuthKeys key, CancellationToken ct = default(CancellationToken))
        {
            if (key == null) return;

            Log("RevokeToken");
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(
                        "https://accounts.google.com/o/oauth2/revoke?token=" + key.access_token,
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    Log("RevokeToken", ex.Message);
                }
                catch (Exception ex)
                {
                    Log("RevokeToken", ex.ToString());
                }
            }
        }

        static public async Task<string> RefreshAuthorizationCode(AuthKeys key, CancellationToken ct = default(CancellationToken))
        {
            Log("RefreshAuthorizationCode");
            if(key.refresh_token == null)
                throw new ArgumentNullException("refresh_token");
            using (var client = new HttpClient())
            {
                try
                {
                    //Log("RefreshAuthorizationCode");
                    var response = await client.PostAsync(
                        (string.IsNullOrEmpty(ConfigAPI.client_secret)) ? ConfigAPI.App_RefreshToken : ConfigAPI.token_uri,
                        new FormUrlEncodedContent(new Dictionary<string, string>{
                            {"grant_type","refresh_token"},
                            {"refresh_token",key.refresh_token},
                            {"client_id", string.IsNullOrEmpty(ConfigAPI.client_secret) ? ConfigAPI.client_id_web: ConfigAPI.client_id},
                            {"client_secret", ConfigAPI.client_secret},
                        }),
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var newkey = ParseAuthResponse(responseBody);
                    return newkey.access_token;
                }
                catch (HttpRequestException ex)
                {
                    Log("RefreshAuthorizationCode", ex.Message);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log("RefreshAuthorizationCode", ex.ToString());
                }
            }
            return null;
        }

        static public AuthKeys ParseAuthResponse(string response)
        {
            AuthKeys key = new AuthKeys();
            var serializer = new DataContractJsonSerializer(typeof(Authentication_Info));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(response)))
            {
                var data = (Authentication_Info)serializer.ReadObject(ms);
                key.access_token = data.access_token;
                key.refresh_token = data.refresh_token;
            }
            return key;
        }

        static T ParseResponse<T>(string response)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(response)))
            {
                return (T)serializer.ReadObject(ms);
            }
        }

        static T ParseResponse<T>(Stream response)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            return (T)serializer.ReadObject(response);
        }

        delegate Task<T> DoConnection<T>();
        static private async Task<T> DoWithRetry<T>(DoConnection<T> func, string LogPrefix = "DoWithRetry")
        {
            Random rnd = new Random();
            var retry = 0;
            string error_str = "";
            while (++retry < 30)
            {
                try
                {
                    return await func().ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log(LogPrefix, error_str);

                    if (ex.Message.Contains("401") ||
                        ex.Message.Contains("403") ||
                        ex.Message.Contains("429") ||
                        ex.Message.Contains("500") ||
                        ex.Message.Contains("503"))
                    {
                        var waitsec = rnd.Next((int)Math.Pow(2, Math.Min(retry, 8))) + 1;
                        Log(LogPrefix, "wait " + waitsec.ToString() + " sec");
                        await Task.Delay(waitsec * 1000);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log(LogPrefix, error_str);
                    break;
                }
            }
            throw new SystemException(LogPrefix + " Failed. " + error_str);
        }


        public AuthKeys Auth;
        DateTime AuthTimer;

        /// <summary>
        /// make sure access_token
        /// </summary>
        /// <param name="ct">CancellationToken</param>
        /// <returns>true: access_token is refreshed. false: not refreshed(fresh key or auth error)</returns>
        public async Task<bool> EnsureToken(CancellationToken ct = default(CancellationToken))
        {
            if (DateTime.Now - AuthTimer < TimeSpan.FromMinutes(50)) return false;
            var retry = 5;
            while (retry-- > 0)
            {
                ct.ThrowIfCancellationRequested();
                Auth.access_token = await RefreshAuthorizationCode(Auth, ct).ConfigureAwait(false) ?? throw new Exception("Authrized failed.");
                if (await GetAccountInfo(ct).ConfigureAwait(false))
                {
                    AuthTimer = DateTime.Now;
                    return true;
                }
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            return false;
        }

        public async Task<bool> GetAccountInfo(CancellationToken ct = default(CancellationToken))
        {
            Log("GetAccountInfo");
            try
            {
                using (var client = new HttpClient())
                {
                    return await DoWithRetry(async () =>
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        var response = await client.GetAsync(
                            ConfigAPI.drive_uri+ "/about?fields=kind",
                            ct
                        ).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        return true;
                    }, "GetAccountInfo").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public async Task<FileMetadata_Info[]> FilesList(string q = null, CancellationToken ct = default(CancellationToken))
        {
            var fields = "nextPageToken,incompleteSearch,files(id,mimeType,name,trashed,parents,viewedByMeTime,modifiedTime,createdTime,md5Checksum,size)";

            Log("GetFilesList");
            await EnsureToken(ct).ConfigureAwait(false);
            try
            {
                var result = new List<FileMetadata_Info>();
                using (var handler = new HttpClientHandler())
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        string pageToken = null;
                        do
                        {
                            result.AddRange(await DoWithRetry(async () =>
                            {
                                var response = await client.GetAsync(
                                    ConfigAPI.drive_uri + "/files?pageSize=1000&fields=" 
                                    + fields 
                                    + ((pageToken != null) ? "&pageToken=" + pageToken : "")
                                    + ((q != null) ? "&q=" + q : ""),
                                    ct
                                ).ConfigureAwait(false);
                                if (!response.IsSuccessStatusCode)
                                    Log("GetFilesList(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                                response.EnsureSuccessStatusCode();
                                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                var data = ParseResponse<FileListdata_Info>(responseBody);
                                pageToken = data.nextPageToken;
                                return data.files;
                            }, "GetFilesList").ConfigureAwait(false));
                        } while (!string.IsNullOrEmpty(pageToken));
                        return result.ToArray();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }


 
        public async Task<string> ChangesGetStartPageToken(CancellationToken ct = default(CancellationToken))
        {
            var fields = "startPageToken";

            Log("GetChangesGetStartPageToken");
            await EnsureToken(ct).ConfigureAwait(false);
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    return await DoWithRetry(async () =>
                    {
                        var response = await client.GetAsync(
                            ConfigAPI.drive_uri + "/changes/startPageToken?fields=" + fields,
                            ct
                        ).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                            Log("GetChangesGetStartPageToken(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var data = ParseResponse<ChangesStartPageToken_Info>(responseBody);
                        return data.startPageToken;
                    }, "GetChangesGetStartPageToken").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public async Task<(Changesdata_Info[] changes, string newPageToken)> ChangesList(string pageToken, CancellationToken ct = default(CancellationToken))
        {
            var fields = "nextPageToken,newStartPageToken,changes(type,removed,fileId,file(id,mimeType,name,trashed,parents,viewedByMeTime,modifiedTime,createdTime,md5Checksum,size))";

            Log("ChangesList");
            await EnsureToken(ct).ConfigureAwait(false);
            try
            {
                var result = new List<Changesdata_Info>();
                string newStartPageToken = null;
                using (var handler = new HttpClientHandler())
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        do
                        {
                            result.AddRange(await DoWithRetry(async () =>
                            {
                                var response = await client.GetAsync(
                                    ConfigAPI.drive_uri + "/changes?pageSize=1000&fields=" + fields + "&pageToken=" + pageToken,
                                    ct
                                ).ConfigureAwait(false);
                                if (!response.IsSuccessStatusCode)
                                    Log("ChangesList(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                                response.EnsureSuccessStatusCode();
                                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                                var data = ParseResponse<ChangesListdata_Info>(responseBody);
                                pageToken = data.nextPageToken;
                                newStartPageToken = data.newStartPageToken;
                                return data.changes;
                            }, "ChangesList").ConfigureAwait(false));
                        } while (string.IsNullOrEmpty(newStartPageToken));
                        return (result.ToArray(), newStartPageToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return (null , null);
            }
        }



        public async Task<FileMetadata_Info> FilesGet(string fileId = "root", CancellationToken ct = default(CancellationToken))
        {
            var fields = "id,mimeType,name,trashed,parents,viewedByMeTime,modifiedTime,createdTime,md5Checksum,size";

            Log("FilesGet");
            await EnsureToken(ct).ConfigureAwait(false);
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        return await DoWithRetry(async () =>
                        {
                            var response = await client.GetAsync(
                                    ConfigAPI.drive_uri + "/files/" + fileId + "?fields=" + fields,
                                        ct
                                    ).ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                                Log("FilesGet(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            var data = ParseResponse<FileMetadata_Info>(responseBody);
                            return data;
                        }, "FilesGet").ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public async Task<FileMetadata_Info> FilesUpdate(string fileId, FileMetadata_Info newinfo, CancellationToken ct = default(CancellationToken))
        {
            var fields = "mimeType,name,trashed";

            Log("FilesUpdate");
            await EnsureToken(ct).ConfigureAwait(false);
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        return await DoWithRetry(async () =>
                        {
                            var req = new HttpRequestMessage(new HttpMethod("PATCH"), 
                                ConfigAPI.drive_uri + "/files/" + fileId + "?fields=" + fields);

                            DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(FileMetadata_Info));

                            MemoryStream ms = new MemoryStream();
                            jsonSer.WriteObject(ms, newinfo);
                            ms.Position = 0;

                            StreamReader sr = new StreamReader(ms);
                            var content = new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json");

                            req.Content = content;
                            var response = await client.SendAsync(req, ct).ConfigureAwait(false);

                            if (!response.IsSuccessStatusCode)
                                Log("FilesUpdate(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            var data = ParseResponse<FileMetadata_Info>(responseBody);
                            return await FilesGet(fileId, ct).ConfigureAwait(false);
                        }, "FilesUpdate").ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<FileMetadata_Info[]> ListChildren(string fileId = "root", CancellationToken ct = default(CancellationToken))
        {
            Log("ListChildren", fileId);
            return await FilesList(string.Format("'{0}'+in+parents", fileId)).ConfigureAwait(false);
        }



        public async Task<FileMetadata_Info> UploadStream(Stream uploadStream, string parent_id, string uploadname, long? uploadsize = null, CancellationToken ct = default(CancellationToken))
        {
            uploadsize = uploadsize ?? uploadStream.Length;
            if (false && uploadsize < 5 * 1024 * 1024)
                return await UploadStreamShort(uploadStream, parent_id, uploadname, uploadsize, ct).ConfigureAwait(false);
            else
                return await UploadStreamLong(uploadStream, parent_id, uploadname, uploadsize, ct).ConfigureAwait(false);
        }

        private async Task<FileMetadata_Info> UploadStreamShort(Stream uploadStream, string parent_id, string uploadname, long? uploadsize = null, CancellationToken ct = default(CancellationToken))
        {
 
            Log("UploadStreamShort", uploadname);
            await EnsureToken(ct).ConfigureAwait(false);
 
            string error_str;
            string HashStr = "";
            uploadsize = uploadsize ?? uploadStream.Length;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromDays(1);
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    var content = new MultipartFormDataContent();

                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(FileMetadata_Info));

                    MemoryStream ms = new MemoryStream();
                    jsonSer.WriteObject(ms, new FileMetadata_Info
                    {
                        name = uploadname,
                        parents = new[] { parent_id },
                    });
                    ms.Position = 0;

                    StreamReader sr = new StreamReader(ms);
                    content.Add(new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json"), "metadata");

                    using (var contStream = new HashStream(uploadStream, new MD5CryptoServiceProvider()))
                    {

                        var fileContent = new StreamContent(contStream);
                        fileContent.Headers.ContentLength = uploadsize;
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        content.Add(fileContent, "content", uploadname);

                        using (fileContent)
                        {
                            var response = await client.PostAsync(
                                ConfigAPI.upload_uri + "/files?uploadType=multipart",
                                content,
                                ct).ConfigureAwait(false);
                            HashStr = contStream.Hash.ToLower();
                            if (!response.IsSuccessStatusCode)
                                Log("UploadStreamShort(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var ret = ParseResponse<FileMetadata_Info>(responseBody);
                            ret = await FilesGet(ret.id, ct).ConfigureAwait(false);
                            if (ret.md5Checksum != HashStr)
                                throw new IOException(string.Format("Upload hash not match: Local {0} Remote {1}", ret.md5Checksum, HashStr));
                            Log("UploadStream", string.Format("{0} done. MD5:{1}", uploadname, HashStr));
                            return ret;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log("UploadStreamShort", string.Format("{0} HTTP error. MD5:{1}", uploadname, HashStr));
                    error_str = ex.Message;
                    Log("UploadStreamShort", error_str);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("UploadStreamShort", error_str);
                    throw;
                }
            }
        }

        private async Task<FileMetadata_Info> UploadStreamLong(Stream uploadStream, string parent_id, string uploadname, long? uploadsize = null, CancellationToken ct = default(CancellationToken))
        {

            Log("UploadStreamLong", uploadname);
            await EnsureToken(ct).ConfigureAwait(false);

            string error_str;
            string HashStr = "";
            uploadsize = uploadsize ?? uploadStream.Length;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromDays(1);
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);

                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(FileMetadata_Info));

                    MemoryStream ms = new MemoryStream();
                    jsonSer.WriteObject(ms, new FileMetadata_Info
                    {
                        name = uploadname,
                        parents = new[] { parent_id },
                    });
                    ms.Position = 0;

                    StreamReader sr = new StreamReader(ms);

                    var response1 = await client.PostAsync(
                        ConfigAPI.upload_uri + "/files?uploadType=resumable",
                        new StringContent(sr.ReadToEnd(), Encoding.UTF8, "application/json"),
                        ct).ConfigureAwait(false);
                    if (!response1.IsSuccessStatusCode)
                        Log("UploadStreamLong1(error)", await response1.Content.ReadAsStringAsync().ConfigureAwait(false));
                    response1.EnsureSuccessStatusCode();
                    var uploadsession = response1.Headers.Location;

                    using (var contStream = new HashStream(uploadStream, new MD5CryptoServiceProvider()))
                    {

                        var fileContent = new StreamContent(contStream);
                        fileContent.Headers.ContentLength = uploadsize;
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                        using (fileContent)
                        {
                            var response2 = await client.PutAsync(
                                uploadsession,
                                fileContent,
                                ct).ConfigureAwait(false);
                            HashStr = contStream.Hash.ToLower();
                            if (!response2.IsSuccessStatusCode)
                                Log("UploadStreamLong2(error)", await response2.Content.ReadAsStringAsync().ConfigureAwait(false));
                            response2.EnsureSuccessStatusCode();
                            string responseBody = await response2.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var ret = ParseResponse<FileMetadata_Info>(responseBody);
                            ret = await FilesGet(ret.id, ct).ConfigureAwait(false);
                            if (ret.md5Checksum != HashStr)
                                throw new IOException(string.Format("Upload hash not match: Local {0} Remote {1}", ret.md5Checksum, HashStr));
                            Log("UploadStream", string.Format("{0} done. MD5:{1}", uploadname, HashStr));
                            return ret;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log("UploadStreamLong", string.Format("{0} HTTP error. MD5:{1}", uploadname, HashStr));
                    error_str = ex.Message;
                    Log("UploadStreamLong", error_str);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("UploadStreamLong", error_str);
                    throw;
                }
            }
        }


        public async Task<Stream> DownloadItem(string targetID, long? from = null, long? to = null, string hash = null, long? length = null, CancellationToken ct = default(CancellationToken))
        {
            string id = targetID;

            Log("DownloadItem", id);
            await EnsureToken(ct).ConfigureAwait(false);

            string error_str = "";
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromDays(1);
            try
            {
                long? fix_from = from, fix_to = to;

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                if (from != null || to != null)
                {
                    client.DefaultRequestHeaders.Range = new RangeHeaderValue(from, to);
                }
                string url = ConfigAPI.drive_uri + "/files/" + id + "?alt=media";
                return await DoWithRetry(async () =>
                {
                    var response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        Log("DownloadItem(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    response.EnsureSuccessStatusCode();
                    if ((from ?? 0) == 0 && to == null)
                        return new HashStream(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), new MD5CryptoServiceProvider(), hash, length);
                    else
                        return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }, "DownloadItem").ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                error_str = ex.Message;
                Log("DownloadItem", error_str);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                error_str = ex.ToString();
                Log("DownloadItem", error_str);
                throw;
            }
            throw new SystemException("DownloadItem failed. " + error_str);
        }


        public async Task<bool> TrashItem(string id, CancellationToken ct = default(CancellationToken))
        {
            Log("Trash", id);
            var ret = await FilesUpdate(id, new FileMetadata_Info() { trashed = true }, ct).ConfigureAwait(false);
            return ret?.trashed ?? false;
        }

        public async Task<FileMetadata_Info> RenameItem(string id, string newname, CancellationToken ct = default(CancellationToken))
        {
            Log("rename", id + ' ' + newname);
            return await FilesUpdate(id, new FileMetadata_Info() { name = newname }, ct).ConfigureAwait(false);
        }


        public async Task<FileMetadata_Info> CreateFolder(string foldername, string parent_id = "root", CancellationToken ct = default(CancellationToken))
        {
            Log("CreateFolder", foldername);
            await EnsureToken(ct).ConfigureAwait(false);

            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        return await DoWithRetry(async () =>
                        {
                            DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(FileMetadata_Info));

                            MemoryStream ms = new MemoryStream();
                            jsonSer.WriteObject(ms, new FileMetadata_Info
                            {
                                name = foldername,
                                parents = new[] { parent_id },
                                mimeType = "application/vnd.google-apps.folder",
                            });
                            ms.Position = 0;

                            StreamReader sr = new StreamReader(ms);
                            var content = new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json");

                            var response = await client.PostAsync(
                                ConfigAPI.drive_uri + "/files",
                                content,
                                ct).ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                                Log("CreateFolder(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var ret = ParseResponse<FileMetadata_Info>(responseBody);
                            return await FilesGet(ret.id, ct).ConfigureAwait(false);

                        }, "CreateFolder").ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public async Task<FileMetadata_Info> MoveChild(string childid, string fromParentId, string toParentId, CancellationToken ct = default(CancellationToken))
        {
            var fields = "mimeType,name,trashed";

            Log("MoveChild");
            string error_str;
            await EnsureToken(ct).ConfigureAwait(false);

            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    return await DoWithRetry(async () =>
                    {
                        var req = new HttpRequestMessage(new HttpMethod("PATCH"),
                        ConfigAPI.drive_uri + "/files/" + childid
                        + "?fields=" + fields
                        + "&addParents=" + toParentId
                        + "&removeParents=" + fromParentId);

                        DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(FileMetadata_Info));

                        MemoryStream ms = new MemoryStream();
                        jsonSer.WriteObject(ms, new FileMetadata_Info
                        {
                        });
                        ms.Position = 0;

                        StreamReader sr = new StreamReader(ms);
                        var content = new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json");

                        req.Content = content;
                        var response = await client.SendAsync(req, ct).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                            Log("MoveChild(error)", await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var data = ParseResponse<FileMetadata_Info>(responseBody);

                        return await FilesGet(childid, ct).ConfigureAwait(false);
                    }, "MoveChild").ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log("MoveChild", error_str);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("MoveChild", error_str);
                }
            }
            throw new SystemException("moveChild failed. " + error_str);
        }

    }
}
