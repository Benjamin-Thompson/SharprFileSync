using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SharprFileSync.Services
{
    public sealed class SharprSyncService
    {
        private static Lazy<SharprSyncService> instance = new Lazy<SharprSyncService>(() => new SharprSyncService());

        private static CredentialCache _credentialCache;
        private  string _sharprUser = "1xnTyjWyD0s5BtVZpZCN";
        private  string _sharprPass = "1jnvrrfmvpFnBYZxx8DNVknFZmthQqpYRB7q3L09 ";
        private  string _sharprURL = "https://sharprua.com/api/";
        private string _localDocumentList = "Documents";
        private List<SharprFileMetadata> _metadata = new List<SharprFileMetadata>();
        private List<SharprTransferRecord> currentFileList = new List<SharprTransferRecord>();

        private SharprSyncService()
        {
            currentFileList = new List<SharprTransferRecord>();
        }

        public static SharprSyncService Instance
        {
            get { return instance.Value; }
        }

        public void InitSettings (string url, string user, string pass, string documentList, List<SharprFileMetadata> metadata)
        {
            _sharprURL = url;
            _sharprPass = pass;
            _sharprUser = user;
            _localDocumentList = documentList;
            _metadata = metadata;

        }

     

        private  HttpClient CreateSharprRequest()
        {
            var client = new HttpClient();

            string userpass = _sharprUser + ":" + _sharprPass;
            var userpassB = Encoding.UTF8.GetBytes(userpass);
            var userpassB64 = Convert.ToBase64String(userpassB);

            //var decrypted = Convert.FromBase64String("MXJzMlBDQ2dDdlI4TTFZVlRWWVo6MDVna05IZ1hrQjlLWUR6UXlsU0sxQlRKOG1INDU1eGo2dDR4WGJMbiA=");

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", userpassB64);
            client.DefaultRequestHeaders.Add("Accept-Encoding", "deflate");

            return client;
        }


        public SharprInitResults InitialListLoad(SPWeb web)
        {
            SharprInitResults result = new SharprInitResults();

            result.UploadSuccessCount = 0;
            result.UploadFailCount = 0;
            result.TotalFileCount = 0;

            SPList list = web.Lists[_localDocumentList];

            List<string> fields = new List<string>();
            foreach(SharprFileMetadata m in _metadata)
            {
                if (!fields.Contains(m.SharePointPropertyName)) fields.Add(m.SharePointPropertyName);
            }

            foreach (SPListItem li in list.GetItems(fields.ToArray()))
            {
                string uniqueId = "";
                string fileName = "";
                string contentType = "";               
                List<SharprFileMetadata> metadata = new List<SharprFileMetadata>();


                uniqueId = li.UniqueId.ToString();
                fileName = li.File.Name;
                contentType = MimeMapping.GetMimeMapping(fileName);

                byte[] fileContents = li.File.OpenBinary();
                MemoryStream mStream = new MemoryStream();
                mStream.Write(fileContents, 0, fileContents.Length);

                metadata.AddRange(_metadata);
                foreach(SharprFileMetadata m in metadata)
                {
                    try { m.PropertyValue = ((string)li[m.SharePointPropertyName]); }
                    catch { }
                }

                string fResult = UploadFileToSharpr(uniqueId, fileName, contentType, mStream, metadata);

                if (fResult == "Created") result.UploadSuccessCount++;
                else result.UploadFailCount++;
                result.TotalFileCount ++;
            }

            return result;

        }

        public  string UploadFileToSharpr(string fileGUID, string fileName, string contentType, MemoryStream fileContents, List<SharprFileMetadata> metadata)
        {
            string result = "PENDING";
            if (this.currentFileList.Find(c => c.Guid == fileGUID) == null)
            {               
                HttpClient client = CreateSharprRequest();

                if (fileContents.CanRead && fileContents.Length > 0)
                {
                    string fileDataString = contentType + Convert.ToBase64String(fileContents.ToArray());
                    //in the event that the file data string contains the file type in it, strip that off (it messes up Sharpr)
                    if (fileDataString.StartsWith(contentType)) fileDataString = fileDataString.TrimStart(contentType.ToCharArray()).TrimStart(' ');
                    SharprAddUpdateRequest req = new SharprAddUpdateRequest();
                    req.refNumber = fileGUID;
                    req.filename = fileName;
                    req.data = "data:" + contentType + ";base64, " + fileDataString;
                    req.file_size = fileDataString.Length;
                    req.tags = new List<string>();


                    foreach (SharprFileMetadata m in metadata)
                    {
                        //sharepoint stores numeric values in the format "3;#3.00000000000000"; we need to test for this formatting, and if present, convert it to a plain number.
                        string pValue = "";
                        string testValue = TestValueForSharepointInt(m.PropertyValue);
                        if (testValue != "") pValue = testValue;
                        else pValue = m.PropertyValue;

                        if (m.SharprPropertyName.ToLower() == "category") req.category = pValue;
                        else if (m.SharprPropertyName.ToLower() == "classification_id") req.classification_id = pValue;
                        else if (m.SharprPropertyName.ToLower() == "tags") req.tags.Add(pValue);
                    }


                    string stringJson = Newtonsoft.Json.JsonConvert.SerializeObject(req);

                    stringJson = stringJson.Replace("refNumber", "ref");

                    var content = new StringContent(stringJson, Encoding.UTF8, "application/json");

                    var tResponse = client.PutAsync(_sharprURL + "v2/files/sync", content);
                    tResponse.Wait();

                    var tRead = tResponse.Result.Content.ReadAsStringAsync();
                    tRead.Wait();

                    if (tRead.Result != null) result = tResponse.Result.StatusCode.ToString();
                }
                else
                {
                    result = "FILE-EMPTY";
                }

                client.Dispose();

                this.currentFileList.Add(new SharprTransferRecord { Guid = fileGUID, FileName = fileName, TimeStamp = DateTime.UtcNow, Result = result });
            }
            return result;
        }


        public string TestValueForSharepointInt(string value)
        {
            string result = "";

            Regex r = new Regex(@"\d*;#\d*\.\d*");
            if (r.Match(value).Success) // like "3;#3.00000000000000"
            {
                result = value.Substring(0, value.IndexOf(';'));
            }


            return result;
        }
       public  string RemoveFileFromSharpr(string fileGUID)
        {
            string result = "PENDING";
            HttpClient client = CreateSharprRequest();

            ArraySegment<byte> buffer = new ArraySegment<byte>();

            if (fileGUID != null && fileGUID.Length > 0)
            {

                //# An API Response ID is also sent that references Sharpr's log ID
                //responseId = response.getHeader("API-Response-Id")
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ref\":\"" + fileGUID + "\"");

                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");

                var tResponse = client.DeleteAsync(_sharprURL + "v2/files/sync/" + fileGUID);
                tResponse.Wait();

                var tRead = tResponse.Result.Content.ReadAsStringAsync();
                tRead.Wait();

                if (tRead.Result != null) result = tRead.Result;
            }
            else
            {
                result = "FILE-EMPTY";
            }

            client.Dispose();

            return result;
        }


    }
}
