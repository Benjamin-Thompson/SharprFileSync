﻿using System;
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
        /// <summary>
        /// An item was added.
        /// </summary>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemAdded", "Started.");
            
            try
            {
                if (properties.ListItem.Level == SPFileLevel.Published) SendFile(properties);

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
                if (properties.ListItem.Level == SPFileLevel.Published) SendFile(properties);

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

        /// <summary>
        /// An item was deleted.
        /// </summary>
        public override void ItemDeleted(SPItemEventProperties properties)
        {
            Logger.WriteLog(Logger.Category.Information, "ItemDeleted", "Started.");
            try
            {
                    //get settings from the appropriate list
                    SharprSettings settings = GetSharprSettingValues(properties);

                    SharprSyncService sss = new SharprSyncService(settings.SharprURL, settings.SharprUser, settings.SharprPass, settings.DocumentListName, new List<SharprFileMetadata>());

                    SPFile sFile = properties.ListItem.File;

                    sss.RemoveFileFromSharpr(sFile.UniqueId.ToString());

                    Logger.WriteLog(Logger.Category.Information, "ItemDeleted", "Completed.");
            }
            catch (Exception ex)
            {
                Logger.WriteLog(Logger.Category.Unexpected, "ItemDeleted", ex.Message + Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                base.ItemDeleted(properties);
            }
            
        }

        private static void SendFile(SPItemEventProperties properties)
        {
            //get settings from the appropriate list
            SharprSettings settings = GetSharprSettingValues(properties);

            SharprSyncService sss = new SharprSyncService(settings.SharprURL, settings.SharprUser, settings.SharprPass, settings.DocumentListName, settings.FileMetadata);

            if (settings.InitialExportDate == null)
            {
                try
                {
                    sss.InitialListLoad(properties.Web);
                    WriteSharprSettingValue(properties, "Sharpr Service Init Date", DateTime.UtcNow.ToShortDateString());
                }
                catch { }              
            }

            SPFile sFile = properties.ListItem.File;

            string contentType = MimeMapping.GetMimeMapping(sFile.Name);

            byte[] fileContents = sFile.OpenBinary();
            MemoryStream mStream = new MemoryStream();
            mStream.Write(fileContents, 0, fileContents.Length);

            List<SharprFileMetadata> metadata = new List<SharprFileMetadata>();
            metadata.AddRange(settings.FileMetadata);
            foreach(SharprFileMetadata m in metadata)
            {
                try { m.PropertyValue = ((string)sFile.Item[m.SharePointPropertyName]); } 
                catch(Exception ex)
                {
                    //do nothing
                }
            }

            //now that we have the contents, upload to Sharpr
            sss.UploadFileToSharpr(sFile.UniqueId.ToString(), sFile.Name, contentType, mStream, metadata);
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
                    else if (((string)li["Title"]) == "Sharpr File Metadata")
                    {
                        result.FileMetadata = new List<SharprFileMetadata>();
                        string stringMetadata = (string)li["Value"];
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
    }
}