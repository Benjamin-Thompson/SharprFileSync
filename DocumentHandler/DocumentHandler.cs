using System;
using System.IO;
using System.Net;
using System.Security.Permissions;
using System.Web;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.Workflow;
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
                SendFile(properties);

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
                SendFile(properties);

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

                SharprSyncService sss = new SharprSyncService(settings.SharprURL, settings.SharprUser, settings.SharprPass);

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

            SharprSyncService sss = new SharprSyncService(settings.SharprURL, settings.SharprUser, settings.SharprPass);

            SPFile sFile = properties.ListItem.File;

            string contentType = MimeMapping.GetMimeMapping(sFile.Name);

            byte[] fileContents = sFile.OpenBinary();
            MemoryStream mStream = new MemoryStream();
            mStream.Write(fileContents, 0, fileContents.Length);

            string[] tags = null;
            var pTags = sFile.GetProperty("tags");
            if (pTags != null)
            {
                //split comma delimited tags into an array of strings
                tags = ((string)pTags).Split(',');
            }

            string classification = "test"; //for now

            //now that we have the contents, upload to Sharpr
            sss.UploadFileToSharpr(sFile.UniqueId.ToString(), sFile.Name, classification, tags, contentType, mStream);
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
                    else if (((string)li["Title"]) == "Local Document List") result.LocalDocumentListName = (string)li["Value"];
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