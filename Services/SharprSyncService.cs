using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SharprFileSync.Services
{
    public class SharprSyncService
    {
        private static CredentialCache _credentialCache;
        private  string _sharprUser = "1xnTyjWyD0s5BtVZpZCN";
        private  string _sharprPass = "1jnvrrfmvpFnBYZxx8DNVknFZmthQqpYRB7q3L09 ";
        private  string _sharprURL = "https://sharprua.com/api/";
        private string _localDocumentList = "Documents";
        private List<SharprFileMetadata> _metadata = new List<SharprFileMetadata>();

        public SharprSyncService()
        {

        }

        public SharprSyncService (string url, string user, string pass, string documentList, List<SharprFileMetadata> metadata)
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


        public void InitialListLoad(SPWeb web)
        {

            int successFileCount = 0;
            int failedFileCount = 0;
            int totalFilecount = 0;

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

                if (fResult == "Created") successFileCount++;
                else failedFileCount++;
                totalFilecount++;
            }

      

        }

        public  string UploadFileToSharpr(string fileGUID, string fileName, string contentType, MemoryStream fileContents, List<SharprFileMetadata> metadata)
        {
            string result = "PENDING";
            HttpClient client = CreateSharprRequest();

            if (fileContents.CanRead && fileContents.Length > 0)
            {
                string fileDataString = contentType + Convert.ToBase64String(fileContents.ToArray());
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ref\":\"" + fileGUID + "\",");
                sb.Append("\"filename\":\"" + fileName + "\",");
                sb.Append("\"data\":\"data:" + contentType + ";base64, " + fileDataString + "\",");
                sb.Append("\"file_size\":\"" + fileDataString.Length.ToString() + "\",");

                foreach(SharprFileMetadata m in metadata)
                {
                    sb.Append("\"" + m.SharprPropertyName + "\":\"" + m.PropertyValue + "\",");
                }

                sb.Remove(sb.Length - 1, 1); //trim off the last ','

                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");

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
