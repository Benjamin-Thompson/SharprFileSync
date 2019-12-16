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
            Logger.WriteLog(Logger.Category.Information, "Init", "Completed.");
        }

        /// <summary>
        /// An item was added.
        /// </summary>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Started.");
            
            try
            {
                //if ((properties.AfterProperties["vti_sourcecontrolcheckedoutby"] == null) &&
                //    (properties.BeforeProperties["vti_sourcecontrolcheckedoutby"] != null))
                //{
                    Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Getting Settings");
                    SharprSettings settings = GetSharprSettingValues(properties);
                    //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Retrieved Settings.");
                    //if (properties.ListItem.Level == SPFileLevel.Published)
                    //{
                        Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Item is published");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "properties.List.Title is '" + properties.List.Title + "'");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "settings.DocumentListName is '" + settings.DocumentListName + "'");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "properties.List.ID is '" + properties.List.ID + "'");
                if (properties.List.ID.ToString().Equals(settings.DocumentListName))
                {
                            //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Calling SendFile");
                            SendFile(properties);
                            //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "SendFile completed.");
                        }
                    //}

                //}
                Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Completed.");
            }
            catch (Exception ex)
            {
                string innerEx = "";
                if (ex.InnerException != null)
                {
                    innerEx = ex.InnerException.Message + Environment.NewLine + ex.InnerException.StackTrace;
                }
                Logger.WriteLog(Logger.Category.Unexpected, "ItemAdded", ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + innerEx);
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
                //Logger.WriteLog(Logger.Category.Information, "ItemUpdated AfterProperties['vti_sourcecontrolcheckedoutby'] :", properties.AfterProperties["vti_sourcecontrolcheckedoutby"].ToString());
                //Logger.WriteLog(Logger.Category.Information, "ItemUpdated BeforeProperties['vti_sourcecontrolcheckedoutby'] ", properties.BeforeProperties["vti_sourcecontrolcheckedoutby"].ToString());
                //if ((properties.AfterProperties["vti_sourcecontrolcheckedoutby"] == null) &&
                //    (properties.BeforeProperties["vti_sourcecontrolcheckedoutby"] != null))
                //{
                    Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Getting settings");
                    SharprSettings settings = GetSharprSettingValues(properties);
                    //Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Retrieved settings.");
                    //if (properties.ListItem.Level == SPFileLevel.Published)
                    //{
                        Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Item is published");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "properties.List.Title is '" + properties.List.Title + "'");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "settings.DocumentListName is '" + settings.DocumentListName + "'");
                //Logger.WriteLog(Logger.Category.Information, "ItemAdded", "properties.List.ID is '" + properties.List.ID + "'");
                if (properties.List.ID.ToString().Equals(settings.DocumentListName))
                        {
                            //Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Calling SendFile");
                            SendFile(properties);
                            //Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "SendFile completed.");
                        }
                    //}

                //}

                Logger.WriteLog(Logger.Category.Information, "ItemUpdated", "Completed.");
            }
            catch (Exception ex)
            {
                string innerEx = "";
                if (ex.InnerException != null)
                {
                    innerEx = ex.InnerException.Message + Environment.NewLine + ex.InnerException.StackTrace;
                }
                Logger.WriteLog(Logger.Category.Unexpected, "ItemUpdated", ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + innerEx);
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
                    Logger.WriteLog(Logger.Category.Information, "SendFile", "performing initial export.");
                    SharprInitResults result = sss.InitialListLoad(properties.Web);
                    Logger.WriteLog(Logger.Category.Information, "SendFile", "Initial export complete, writing results.");
                    WriteSharprSettingValue(properties, "Sharpr Service Init Date", DateTime.UtcNow.ToShortDateString());
                    WriteSharprSettingValue(properties, "Sharpr Export Results", Newtonsoft.Json.JsonConvert.SerializeObject(result));
                }
                catch { }              
            } else
            {
                SPFile sFile = properties.ListItem.File;
                Logger.WriteLog(Logger.Category.Information, "SendFile", "Got file for export : " + sFile.Name);
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
                Logger.WriteLog(Logger.Category.Information, "SendFile", "Calling UploadFileToSharpr.");
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
                string[] fields = { "Propertylabel", "Value" };

                //Logger.WriteLog(Logger.Category.Information, "GetSharprSettingValues", "Got list of fields.");
                foreach (SPListItem li in list.GetItems(fields))
                {
                    //Logger.WriteLog(Logger.Category.Information, "GetSharprSettingValues", "Looping through list");
                    //Logger.WriteLog(Logger.Category.Information, "GetSharprSettingValues", "Setting '" + (string)li["Propertylabel"] + "' value is '" + (string)li["Value"] + "'");
                    if (((string)li["Propertylabel"]) == "Sharpr Service URL") result.SharprURL = (string)li["Value"];
                    else if (((string)li["Propertylabel"]) == "Sharpr Service User") result.SharprUser = (string)li["Value"];
                    else if (((string)li["Propertylabel"]) == "Sharpr Service Password") result.SharprPass = (string)li["Value"];
                    else if (((string)li["Propertylabel"]) == "Local Document List")
                    {
                        result.DocumentListName = TrimDiv((string)li["Value"]);
                    }
                    //else if (((string)li["Propertylabel"]) == "Currently Processing Files") result.CurrentlyProcessingFile = (string)li["Value"];
                    else if (((string)li["Propertylabel"]) == "Sharpr File Metadata")
                    {
                        result.FileMetadata = new List<SharprFileMetadata>();
                        string stringMetadata = System.Web.HttpUtility.HtmlDecode((string)li["Value"]);
                        stringMetadata = stringMetadata.TrimEnd("</div>".ToCharArray()).TrimStart(stringMetadata.Substring(0, stringMetadata.IndexOf('[')).ToCharArray());  //the <div></div> container is added if a multiline control is used. if that's the case, we need to strip that off. 
                        result.FileMetadata = JsonConvert.DeserializeObject<List<SharprFileMetadata>>(stringMetadata);
                    }
                    else if (((string)li["Propertylabel"]) == "Sharpr Service Init Date")
                    {
                        //Logger.WriteLog(Logger.Category.Information, "GetSharprSettingValues", "Setting Service Init Date");
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
            //Logger.WriteLog(Logger.Category.Information, "GetSharprSettingValues", "Completed getting settings.");
            return result;
        }

        private static string TrimDiv(string input)
        {
            string output = input;
            try
            {
                output = input.TrimEnd("</div>".ToCharArray()).TrimStart(input.Substring(0, input.IndexOf('>')).ToCharArray()).TrimStart('>');  //the <div></div> container is added if a multiline control is used. if that's the case, we need to strip that off.
            }
            catch {
                //swallow the error
            }

            return output;
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