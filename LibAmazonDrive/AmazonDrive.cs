using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using TSviewCloudPlugin;

namespace LibAmazonDrive
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

    [DataContract]
    public class GetEndpoint_Info
    {
        [DataMember]
        public bool? customerExists;
        [DataMember]
        public string contentUrl;
        [DataMember]
        public string metadataUrl;
    }

    [DataContract]
    public class GetAccountInfo_Info
    {
        [DataMember]
        public string termsOfUse;
        [DataMember]
        public string status;
    }

    [DataContract]
    public class ClientProperties
    {
        [DataMember(Name = "dateUpdated")]
        public string dateUpdated_prop
        {
            get { return dateUpdated_str; }
            set
            {
                dateUpdated = DateTime.Parse(value);
                dateUpdated_str = value;
            }
        }
        private string dateUpdated_str;
        public DateTime dateUpdated;

        [DataMember(Name = "dateCreated")]
        public string dateCreated_prop
        {
            get { return dateCreated_str; }
            set
            {
                dateCreated = DateTime.Parse(value);
                dateCreated_str = value;
            }
        }
        private string dateCreated_str;
        public DateTime dateCreated;
    }

    [Serializable]
    [DataContract]
    public class FileMetadata_Info
    {
        [DataMember]
        public string eTagResponse;
        [DataMember]
        public string id;
        [DataMember]
        public string name;
        [DataMember]
        public string kind;
        [DataMember]
        public int? version;
        [DataMember(Name = "modifiedDate")]
        public string modifiedDate_prop
        {
            get { return modifiedDate_str; }
            set
            {
                modifiedDate = DateTime.Parse(value);
                modifiedDate_str = value;
            }
        }
        private string modifiedDate_str;
        public DateTime? modifiedDate;
        [DataMember(Name = "createdDate")]
        public string createdDate_prop
        {
            get { return createdDate_str; }
            set
            {
                createdDate = DateTime.Parse(value);
                createdDate_str = value;
            }
        }
        private string createdDate_str;
        public DateTime? createdDate;
        [DataMember]
        public ClientProperties clientProperties;
        [DataMember]
        public string[] labels;
        [DataMember]
        public string description;
        [DataMember]
        public string createdBy;
        [DataMember]
        public string[] parents;
        [DataMember]
        public string status;
        [DataMember]
        public string tempLink;
        [DataMember]
        public bool? restricted;
        [DataMember]
        public bool? isRoot;
        [DataMember]
        public bool? isShared;

        [DataMember]
        public ContentProperties_Info contentProperties;
    }

    [Serializable]
    [DataContract]
    public class ContentProperties_Info
    {
        [DataMember]
        public int? version;
        [DataMember]
        public string md5;
        [DataMember]
        public long? size;
        [DataMember]
        public string contentType;
        [DataMember]
        public string extension;
    }

    [DataContract]
    public class FileListdata_Info
    {
        [DataMember]
        public long? count;
        [DataMember]
        public string nextToken;
        [DataMember]
        public FileMetadata_Info[] data;
    }


    [DataContract]
    public class Changes_Info
    {
        [DataMember]
        public string checkpoint;
        [DataMember]
        public bool? end;
        [DataMember]
        public bool? reset;
        [DataMember]
        public int? statusCode;
        [DataMember]
        public FileMetadata_Info[] nodes;
    }

    [Serializable]
    [DataContract]
    public class DriveData_Info
    {
        [DataMember]
        public string checkpoint;
        [DataMember]
        public FileMetadata_Info[] nodes;
    }


    public class AmazonDriveUploadException : Exception
    {
        public string Hash;

        public AmazonDriveUploadException() : base()
        {
        }
        public AmazonDriveUploadException(string message) : base(message)
        {
        }
        public AmazonDriveUploadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    class AmazonDrive
    {
        static public void Log(string func, string str = "")
        {
            TSviewCloudConfig.Config.Log.LogOut("\t[AmazonDrive(" + func + ")] " + str);
        }

        static public async Task<AuthKeys> RefreshAuthorizationCode(AuthKeys key, CancellationToken ct = default(CancellationToken))
        {
            string error_str;
            using (var client = new HttpClient())
            {
                try
                {
                    Log("RefreshAuthorizationCode");
                    var response = await client.PostAsync(
                        (string.IsNullOrEmpty(ConfigAPI.client_secret)) ? ConfigAPI.App_RefreshToken : ConfigAPI.AmazonAPI_token,
                        new FormUrlEncodedContent(new Dictionary<string, string>{
                            {"grant_type","refresh_token"},
                            {"refresh_token",key.refresh_token},
                            {"client_id",ConfigAPI.client_id},
                            {"client_secret",ConfigAPI.client_secret},
                        }),
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    key = ParseAuthResponse(responseBody);
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    System.Diagnostics.Debug.WriteLine(error_str);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    System.Diagnostics.Debug.WriteLine(error_str);
                }
            }
            return key;
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
        private async Task<T> DoWithRetry<T>(DoConnection<T> func, CancellationToken ct = default(CancellationToken), string LogPrefix = "DoWithRetry")
        {
            Random rnd = new Random();
            var retry = 0;
            string error_str = "";
            while (++retry < 30)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await func();
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log(LogPrefix, error_str);

                    if (ex.Message.Contains("429") ||
                        ex.Message.Contains("500") ||
                        ex.Message.Contains("503"))
                    {
                        var waitsec = rnd.Next((int)Math.Pow(2, Math.Min(retry - 1, 8)));
                        Log(LogPrefix, "wait " + waitsec.ToString() + " sec");
                        await Task.Delay(waitsec * 1000).ConfigureAwait(false);
                    }
                    else if (ex.Message.Contains("401"))
                    {
                        Log(LogPrefix, "auth failed.");
                        var retry_auth = 5;
                        while (retry_auth-- > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            Auth = await RefreshAuthorizationCode(Auth, ct).ConfigureAwait(false);
                            await EnsureEndpoint(ct).ConfigureAwait(false);
                            if (await GetAccountInfo(ct).ConfigureAwait(false))
                            {
                                Log(LogPrefix, "Refresh sucess.");
                                AuthTimer = DateTime.Now;
                                break;
                            }
                            await Task.Delay(1000, ct).ConfigureAwait(false);
                        }
                        if (retry_auth > 0) continue;
                        Log(LogPrefix, "Refresh failed.");
                        break;
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
        public GetEndpoint_Info endpoints
        {
            get { return new GetEndpoint_Info() { contentUrl = contentUrl, metadataUrl = metadataUrl }; }
            set { contentUrl = value.contentUrl; metadataUrl = value.metadataUrl; }
        }
        public DateTime endpoint_Age;
        DateTime AuthTimer;
        string contentUrl;
        string metadataUrl;

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
                Auth = await RefreshAuthorizationCode(Auth, ct).ConfigureAwait(false);
                await EnsureEndpoint(ct).ConfigureAwait(false);
                if (await GetAccountInfo(ct).ConfigureAwait(false))
                {
                    AuthTimer = DateTime.Now;
                    return true;
                }
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            return false;
        }

        /// <summary>
        /// make sure endpoints
        /// </summary>
        /// <param name="ct"></param>
        /// <returns>true: endpoints are refreshed. false: endpoints are not need refresh.</returns>
        public async Task<bool> EnsureEndpoint(CancellationToken ct = default(CancellationToken))
        {
            if(string.IsNullOrEmpty(metadataUrl) || string.IsNullOrEmpty(contentUrl))
            {
                await GetEndpoint(ct).ConfigureAwait(false);
                return true;
            }
            if (DateTime.Now - endpoint_Age < TimeSpan.FromDays(3)) return false;
            await GetEndpoint(ct).ConfigureAwait(false);
            return true;
        }

        public async Task<GetEndpoint_Info> GetEndpoint(CancellationToken ct = default(CancellationToken))
        {
            Log("GetEndpoint");
            try
            {
                using (var client = new HttpClient())
                {
                    return await DoWithRetry(async () =>
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        var response = await client.GetAsync(
                            ConfigAPI.getEndpoint,
                            ct
                        ).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var data = ParseResponse<GetEndpoint_Info>(responseBody);
                        contentUrl = data.contentUrl;
                        metadataUrl = data.metadataUrl;
                        endpoint_Age = DateTime.Now;
                        return data;
                    }, ct, "GetEndpoint");
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

        public async Task<bool> GetAccountInfo(CancellationToken ct = default(CancellationToken))
        {
            if (metadataUrl == "") return false;
            Log("GetAccountInfo");
            try
            {
                using (var client = new HttpClient())
                {
                    return await DoWithRetry(async () =>
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                        var response = await client.GetAsync(
                            metadataUrl + "account/info",
                            ct
                        ).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        var data = ParseResponse<GetAccountInfo_Info>(responseBody);
                        return true;
                    }, ct, "GetAccountInfo");
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



        public async Task<FileMetadata_Info> GetFileMetadata(string id, CancellationToken ct = default(CancellationToken), bool templink = false)
        {
            Log("GetFileMetadata", id);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);
            using (var client = new HttpClient())
            {
                return await DoWithRetry(async () =>
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    var response = await client.GetAsync(
                        metadataUrl + "nodes/" + id + ((templink) ? "?tempLink=true&resourceVersion=V2" : "?resourceVersion=V2"),
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return ParseResponse<FileMetadata_Info>(responseBody);
                }, ct, "GetFileMetadata");
            }
        }

        public async Task<FileMetadata_Info> SetFileMetadata(string id, IRemoteItemAttrib updated, CancellationToken ct = default(CancellationToken), bool templink = false)
        {
            Log("SetFileMetadata", id);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);
            using (var client = new HttpClient())
            {
                return await DoWithRetry(async () =>
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    string url = metadataUrl + "nodes/" + id + "?resourceVersion=V2";

                    var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);

                    string data = "{ \"clientProperties\":{\"dateCreated\":\"" 
                    + updated.CreatedDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    + "\",\"dateUpdated\":\"" 
                    + updated.ModifiedDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") 
                    + "\"}}";

                    var content = new StringContent(data, Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
                    req.Content = content;
                    var response = await client.SendAsync(req, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return ParseResponse<FileMetadata_Info>(responseBody);
                }, ct, "SetFileMetadata");
            }
        }

        public async Task<FileListdata_Info> ListMetadata(string filters = null, string startToken = null, CancellationToken ct = default(CancellationToken))
        {
            Log("ListMetadata");
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);
            using (var client = new HttpClient())
            {
                return await DoWithRetry(async () =>
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    var url = metadataUrl + "nodes?resourceVersion=V2&";
                    if (!string.IsNullOrEmpty(filters)) url += "filters=" + filters + '&';
                    if (!string.IsNullOrEmpty(startToken)) url += "startToken=" + startToken + '&';
                    url = url.Trim('?', '&');
                    var response = await client.GetAsync(
                        url,
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var info = ParseResponse<FileListdata_Info>(responseBody);
                    if (!string.IsNullOrEmpty(info.nextToken))
                    {
                        var next_info = await ListMetadata(filters, info.nextToken, ct: ct).ConfigureAwait(false);
                        info.data = info.data.Concat(next_info.data).ToArray();
                    }
                    return info;
                }, ct, "ListMetadata");
            }
        }

        public async Task<FileListdata_Info> ListChildren(string id, string startToken = null, CancellationToken ct = default(CancellationToken))
        {
            Log("ListChildren", id);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);
            using (var client = new HttpClient())
            {
                return await DoWithRetry(async () =>
                {
                    ct.ThrowIfCancellationRequested();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    var response = await client.GetAsync(
                        metadataUrl + "nodes/" + id + "/children?resourceVersion=V2" + (string.IsNullOrEmpty(startToken) ? "" : "&startToken=" + startToken),
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var info = ParseResponse<FileListdata_Info>(responseBody);
                    if (!string.IsNullOrEmpty(info.nextToken))
                    {
                        var next_info = await ListChildren(id, info.nextToken, ct: ct).ConfigureAwait(false);
                        info.data = info.data.Concat(next_info.data).ToArray();
                    }
                    return info;
                }, ct, "ListChildren");
            }
        }


        [DataContract]
        public class Changesreq_Info
        {
            [DataMember(EmitDefaultValue = false)]
            public string checkpoint;

            [DataMember(EmitDefaultValue = false)]
            public int? chunkSize;

            [DataMember(EmitDefaultValue = false)]
            public int? maxNodes;

            [DataMember(EmitDefaultValue = false)]
            public string includePurged;
        }

        public async Task<Changes_Info[]> Changes(string checkpoint = null, int? chankSize = null, CancellationToken ct = default(CancellationToken))
        {
            Log("changes");
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);
            using (var handler = new HttpClientHandler())
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromDays(1);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);

                    return await DoWithRetry(async () =>
                    {
                        DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(Changesreq_Info));

                        MemoryStream ms = new MemoryStream();

                        jsonSer.WriteObject(ms, new Changesreq_Info
                        {
                            checkpoint = checkpoint,
                            chunkSize = chankSize,
                            maxNodes = null,
                            includePurged = "false",
                        });
                        ms.Position = 0;

                        StreamReader sr = new StreamReader(ms);

                        var res = new List<Changes_Info>();

                        var req = new HttpRequestMessage(HttpMethod.Post, metadataUrl + "changes?resourceVersion=V2");
                        req.Content = new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json");
                        var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();

                        var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        byte[] buf = new byte[16 * 1024 * 1024];
                        int len = 0;
                        int offset = 0;
                        while (true)
                        {
                            using (var mem = Stream.Synchronized(new MemoryStream()))
                            {
                                try
                                {
                                    bool reading = true;
                                    while (reading)
                                    {
                                        ct.ThrowIfCancellationRequested();
                                        if (offset > 0)
                                        {
                                            for (int i = offset; i < len; i++)
                                            {
                                                if (buf[i] == '\n')
                                                {
                                                    mem.Write(buf, offset, i - offset);
                                                    offset = i + 1;
                                                    reading = false;
                                                    break; // for
                                                }
                                            }
                                            if (!reading)
                                                break; // reading while
                                            mem.Write(buf, offset, len - offset);
                                            len = 0;
                                            offset = 0;
                                        }
                                        var task = responseStream.ReadAsync(buf, 0, buf.Length, ct).ContinueWith((t) =>
                                        {
                                            reading = false;
                                            if (t.Wait(-1, ct))
                                            {
                                                len = t.Result;
                                                if (len == 0) return;
                                                for (int i = 0; i < len; i++)
                                                {
                                                    if (buf[i] == '\n')
                                                    {
                                                        mem.Write(buf, 0, i);
                                                        offset = i + 1;
                                                        return;
                                                    }
                                                }
                                                mem.Write(buf, 0, len);
                                                offset = 0;
                                                reading = true;
                                            }
                                        }, ct);
                                        await task.ConfigureAwait(false);
                                    }
                                }
                                catch
                                {
                                    break;
                                }
                                if (mem.Position == 0) break;
                                mem.Position = 0;
                                try
                                {
                                    res.Add(ParseResponse<Changes_Info>(mem));
                                }
                                catch { }
                            }
                        }
                        return res.ToArray();
                    }, ct, "changes").ConfigureAwait(false);
                }
            }

        }

        [DataContract]
        public class ItemUpload_Info
        {
            [DataMember(EmitDefaultValue = false)]
            public string name;

            [DataMember(EmitDefaultValue = false)]
            public string kind;

            [DataMember(EmitDefaultValue = false)]
            public string[] parents;
        }

 
        public async Task<FileMetadata_Info> UploadStream(Stream uploadStream, string parent_id, string uploadname, long? uploadsize = null, CancellationToken ct = default(CancellationToken))
        {
            Log("UploadStream", uploadname);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

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

                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(ItemUpload_Info));

                    MemoryStream ms = new MemoryStream();
                    jsonSer.WriteObject(ms, new ItemUpload_Info
                    {
                        name = uploadname,
                        kind = "FILE",
                        parents = string.IsNullOrEmpty(parent_id) ? null : new string[] { parent_id }
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
                                contentUrl + "nodes?suppress=deduplication&resourceVersion=V2",
                                content,
                                ct).ConfigureAwait(false);
                            HashStr = contStream.Hash.ToLower();
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var ret = ParseResponse<FileMetadata_Info>(responseBody);
                            if (ret.contentProperties?.md5 != HashStr)
                                throw new AmazonDriveUploadException(HashStr);
                            Log("UploadStream", string.Format("{0} done. MD5:{1}", uploadname, HashStr));
                            return ret;
                        }
                    }
                }
                catch (AmazonDriveUploadException)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    Log("UploadStream", string.Format("{0} HTTP error. MD5:{1}", uploadname, HashStr));
                    error_str = ex.Message;
                    Log("UploadStream", error_str);
                    throw new AmazonDriveUploadException(HashStr, ex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("UploadStream", error_str);
                    throw;
                }
            }
        }


        public async Task<Stream> DownloadItem(string targetID, long? from = null, long? to = null, string hash = null, long? length = null, CancellationToken ct = default(CancellationToken))
        {
            string id = targetID;

            Log("DownloadItem", id);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

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
                string url = contentUrl + "nodes/" + id + "/content?download=false";
                return await DoWithRetry(async () =>
                {
                    var response = await client.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    if ((from ?? 0) == 0 && to == null)
                        return new HashStream(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), new MD5CryptoServiceProvider(), hash, length);
                    else
                        return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }, ct, "DownloadItem").ConfigureAwait(false);
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
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

            string error_str;
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(ItemUpload_Info));

                     MemoryStream ms = new MemoryStream();

                    jsonSer.WriteObject(ms, new ItemUpload_Info
                    {
                        name = null,
                        kind = "FILE",
                        parents = null,
                    });
                    ms.Position = 0;

                    StreamReader sr = new StreamReader(ms);

                    var response = await client.PutAsync(
                        metadataUrl + "trash/" + id,
                        new StringContent(sr.ReadToEnd(), Encoding.UTF8, "application/json"),
                        ct
                    ).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return true;
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log("Trash",error_str);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("Trash", error_str);
                }
            }
            return false;
        }


        public async Task<FileMetadata_Info> RenameItem(string id, string newname, CancellationToken ct = default(CancellationToken))
        {
            Log("rename" , id + ' ' + newname);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

            string error_str;
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);
                    string url = metadataUrl + "nodes/" + id + "?resourceVersion=V2";

                    var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
                    string data = "{ \"name\" : \"" + newname + "\" }";
                    var content = new StringContent(data, Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("application/json");
                    req.Content = content;
                    var response = await client.SendAsync(req, ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return ParseResponse<FileMetadata_Info>(responseBody);
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log("rename", error_str);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("rename", error_str);
                }
            }
            throw new SystemException("rename failed. " + error_str);
        }

        public async Task<FileMetadata_Info> CreateFolder(string foldername, string parent_id = null, CancellationToken ct = default(CancellationToken))
        {
            Log("CreateFolder", foldername);
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

            string error_str;
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);

                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(ItemUpload_Info));

                    MemoryStream ms = new MemoryStream();

                    jsonSer.WriteObject(ms, new ItemUpload_Info
                    {
                        name = foldername,
                        kind = "FOLDER",
                        parents = string.IsNullOrEmpty(parent_id) ? null : new string[] { parent_id }
                    });
                    ms.Position = 0;

                    StreamReader sr = new StreamReader(ms);

                    var response = await client.PostAsync(
                        metadataUrl + "nodes?resourceVersion=V2",
                        new StringContent(sr.ReadToEnd(), Encoding.UTF8, "application/json"),
                        ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return ParseResponse<FileMetadata_Info>(responseBody);
                }
                catch (HttpRequestException ex)
                {
                    error_str = ex.Message;
                    Log("CreateFolder", error_str);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error_str = ex.ToString();
                    Log("CreateFolder", error_str);
                }
            }
            throw new SystemException("mkFolder failed. " + error_str);
        }

        [DataContract]
        public class MoveChild_Info
        {
            [DataMember(EmitDefaultValue = false)]
            public string fromParent;

            [DataMember(EmitDefaultValue = false)]
            public string childId;
        }

        public async Task<FileMetadata_Info> MoveChild(string childid, string fromParentId, string toParentId, CancellationToken ct = default(CancellationToken))
        {
            Log("MoveChild");
            string error_str;
            await EnsureToken(ct).ConfigureAwait(false);
            await EnsureEndpoint(ct).ConfigureAwait(false);

            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.access_token);

                    DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(MoveChild_Info));

                    MemoryStream ms = new MemoryStream();

                    jsonSer.WriteObject(ms, new MoveChild_Info
                    {
                        fromParent = fromParentId,
                        childId = childid
                    });
                    ms.Position = 0;

                    StreamReader sr = new StreamReader(ms);

                    var response = await client.PostAsync(
                        metadataUrl + "nodes/" + toParentId + "/children?resourceVersion=V2",
                        new StringContent(sr.ReadToEnd(), Encoding.UTF8, "application/json"),
                        ct).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return ParseResponse<FileMetadata_Info>(responseBody);
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
