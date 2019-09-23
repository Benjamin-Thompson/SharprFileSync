using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Permissions;
using System.Web;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.Workflow;
using Newtonsoft.Json;
using SharprFileSync.Services;

namespace SharprFileSync.DocumentHandler
{
    /// <summary>
    /// List Item Events
    /// </summary>
    public class DocumentHandler : SPItemEventReceiver
    {
        private List<SharprTransferRecord> currentFileList;
        public DocumentHandler()
        {
            currentFileList = new List<SharprTransferRecord>() ;
        }

        /// <summary>
        /// An item was added.
        /// </summary>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Started.");
            
            try
            {
                if ((properties.AfterProperties["vti_sourcecontrolcheckedoutby"] == null) &&
                    (properties.BeforeProperties["vti_sourcecontrolcheckedoutby"] != null))
                {
                    SharprSettings settings = GetSharprSettingValues(properties);
                    if (properties.ListItem.Level == SPFileLevel.Published)
                    {
                        if (properties.List.Title == settings.DocumentListName)
                        {
                            SendFile(properties);
                        }
                    }

                }
                Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Completed.");
            }
            catch (Exception ex)
            {
                Logger.WriteLog(Logger.Category.Unexpected, "ItemAdded", ex.Message + Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                base.ItemAdded(properties);
            }
        }


        /// <summary>
        /// An item was updated.
        /// </summary>
        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Started.");

            try
            {
                if ((properties.AfterProperties["vti_sourcecontrolcheckedoutby"] == null) &&
                    (properties.BeforeProperties["vti_sourcecontrolcheckedoutby"] != null))
                {
                    SharprSettings settings = GetSharprSettingValues(properties);
                    if (properties.ListItem.Level == SPFileLevel.Published)
                    {
                        if (properties.List.Title == settings.DocumentListName)
                        {
                            SendFile(properties);
                        }
                    }

                }

                Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Completed.");
            }
            catch (Exception ex)
            {
                Logger.WriteLog(Logger.Category.Unexpected, "ItemUpdated", ex.Message + Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                base.ItemUpdated(properties);
            }
        }




        private void SendFile(SPItemEventProperties properties)
        {
            //get settings from the appropriate list
            SharprSettings settings = GetSharprSettingValues(properties);

            SharprSyncService sss = SharprSyncService.Instance;
            sss.InitSettings(settings.SharprURL, settings.SharprUser, settings.SharprPass, settings.DocumentListName, settings.FileMetadata);

            if (settings.InitialExportDate == null)
            {
                try
                {
                    SharprInitResults result = sss.InitialListLoad(properties.Web);
                    WriteSharprSettingValue(properties, "Sharpr Service Init Date", DateTime.UtcNow.ToShortDateString());
                    WriteSharprSettingValue(properties, "Sharpr Export Results", Newtonsoft.Json.JsonConvert.SerializeObject(result));
                }
                catch { }              
            } else
            {
                SPFile sFile = properties.ListItem.File;

                //WriteSharprSettingValue(properties, "Currently Processing Files", settings.CurrentlyProcessingFile + "," + sFile.Name);
                this.currentFileList.Add(new SharprTransferRecord { FileName = sFile.Name, TimeStamp = DateTime.UtcNow, Result = "Started" });
                string contentType = MimeMapping.GetMimeMapping(sFile.Name);

                byte[] fileContents = sFile.OpenBinary();
                MemoryStream mStream = new MemoryStream();
                mStream.Write(fileContents, 0, fileContents.Length);

                List<SharprFileMetadata> metadata = new List<SharprFileMetadata>();
                metadata.AddRange(settings.FileMetadata);
                foreach (SharprFileMetadata m in metadata)
                {
                    try { m.PropertyValue = ((string)sFile.Item[m.SharePointPropertyName].ToString()); }
                    catch (Exception ex)
                    {
                        //do nothing
                    }
                }

                //now that we have the contents, upload to Sharpr
                string result = sss.UploadFileToSharpr(sFile.UniqueId.ToString(), sFile.Name, contentType, mStream, metadata);           
            }
           
        }

        private static void WriteSharprSettingValue(SPItemEventProperties properties, string title, string value)
        {
            //by convention, we're going to assume settings are stored in a list within the same site called "Sharpr Settings"
            using (SPWeb web = properties.Site.OpenWeb())
            {
                SPList list = web.Lists["Sharpr Settings"];
                string[] fields = { "Title", "Value" };

                foreach (SPListItem li in list.GetItems(fields))
                {
                    if (((string)li["Title"]) == title)
                    {
                        li["Value"] = value;
                        li.Update();
                    }
                }
            }
        }

        private static SharprSettings GetSharprSettingValues(SPItemEventProperties properties)
        {
            SharprSettings result = new SharprSettings();
            result.NotSet = true;

            //by convention, we're going to assume settings are stored in a list within the same site called "Sharpr Settings"
            using (SPWeb web = properties.Site.OpenWeb())
            {
                SPList list = web.Lists["Sharpr Settings"];
                string[] fields = { "Title", "Value" };

                foreach (SPListItem li in list.GetItems(fields))
                {
                    if (((string)li["Title"]) == "Sharpr Service URL") result.SharprURL = (string)li["Value"];
                    else if (((string)li["Title"]) == "Sharpr Service User") result.SharprUser = (string)li["Value"];
                    else if (((string)li["Title"]) == "Sharpr Service Password") result.SharprPass = (string)li["Value"];
                    else if (((string)li["Title"]) == "Local Document List") result.DocumentListName = (string)li["Value"];
                    //else if (((string)li["Title"]) == "Currently Processing Files") result.CurrentlyProcessingFile = (string)li["Value"];
                    else if (((string)li["Title"]) == "Sharpr File Metadata")
                    {
                        result.FileMetadata = new List<SharprFileMetadata>();
                        string stringMetadata = System.Web.HttpUtility.HtmlDecode((string)li["Value"]);
                        stringMetadata = stringMetadata.TrimEnd("</div>".ToCharArray()).TrimStart(stringMetadata.Substring(0, stringMetadata.IndexOf('[')).ToCharArray());  //the <div></div> container is added if a multiline control is used. if that's the case, we need to strip that off. 
                        result.FileMetadata = JsonConvert.DeserializeObject<List<SharprFileMetadata>>(stringMetadata);
                    }
                    else if (((string)li["Title"]) == "Sharpr Service Init Date")
                    {
                        string tmpDate = (string)li["Value"];
                        DateTime InitDate;
                        if (DateTime.TryParse(tmpDate, out InitDate))
                        {
                            result.InitialExportDate = InitDate;
                        }
                    }
                    result.NotSet = false;
                }
            }
            return result;
        }

        /// <summary>
        /// An item is being deleted
        /// </summary>
        public override void ItemDeleting(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemDeleting", "Started.");
            try
            {

                //get settings from the appropriate list
                SharprSettings settings = GetSharprSettingValues(properties);
                if (properties.List.Title == settings.DocumentListName)
                {
                    SharprSyncService sss = SharprSyncService.Instance;
                    sss.InitSettings(settings.SharprURL, settings.SharprUser, settings.SharprPass, settings.DocumentListName, settings.FileMetadata);

                    SPFile sFile = properties.ListItem.File;

                    sss.RemoveFileFromSharpr(sFile.UniqueId.ToString());

                    Logger.WriteLog(Logger.Category.Information, "ItemDeleting", "Completed.");
                }

            }
            catch (Exception ex)
            {
                Logger.WriteLog(Logger.Category.Unexpected, "ItemDeleting", ex.Message + Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                base.ItemDeleting(properties);
            }
            
        }
    }
}