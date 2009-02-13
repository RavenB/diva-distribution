/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.World.Archiver;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{        
    public class InventoryArchiveWriteRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);      
        
        protected TarArchiveWriter archive = new TarArchiveWriter();
        protected CommunicationsManager commsManager;
        protected Dictionary<UUID, int> assetUuids = new Dictionary<UUID, int>();
        
        private CachedUserInfo m_userInfo;
        private string m_invPath;
        
        /// <value>
        /// The stream to which the inventory archive will be saved.
        /// </value>
        private Stream m_saveStream;
        
        /// <summary>
        /// Constructor
        /// </summary>        
        public InventoryArchiveWriteRequest(
            CachedUserInfo userInfo, string invPath, string savePath, CommunicationsManager commsManager)
            : this(
                userInfo,
                invPath, 
                new GZipStream(new FileStream(savePath, FileMode.Create), CompressionMode.Compress),
                commsManager)
        {
        }
        
        /// <summary>
        /// Constructor
        /// </summary>        
        public InventoryArchiveWriteRequest(
             CachedUserInfo userInfo, string invPath, Stream saveStream, CommunicationsManager commsManager)
        {
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_saveStream = saveStream;                        
            this.commsManager = commsManager;
        }

        protected void ReceivedAllAssets(IDictionary<UUID, AssetBase> assetsFound, ICollection<UUID> assetsNotFoundUuids)
        {
            AssetsArchiver assetsArchiver = new AssetsArchiver(assetsFound);
            assetsArchiver.Archive(archive);
            archive.WriteTar(m_saveStream);
        }

        protected void saveInvItem(InventoryItemBase inventoryItem, string path)
        {
            string filename
                    = string.Format("{0}{1}_{2}.xml",
                        path, inventoryItem.Name, inventoryItem.ID);
            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw);
            writer.WriteStartElement("InventoryObject");
            writer.WriteStartElement("Name");
            writer.WriteString(inventoryItem.Name);
            writer.WriteEndElement();
            writer.WriteStartElement("ID");
            writer.WriteString(inventoryItem.ID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("InvType");
            writer.WriteString(inventoryItem.InvType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CreatorUUID");
            writer.WriteString(inventoryItem.Creator.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CreationDate");
            writer.WriteString(inventoryItem.CreationDate.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Owner");
            writer.WriteString(inventoryItem.Owner.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Description");
            if (inventoryItem.Description.Length > 0)
                writer.WriteString(inventoryItem.Description);
            else writer.WriteString("No Description");
            writer.WriteEndElement();
            writer.WriteStartElement("AssetType");
            writer.WriteString(inventoryItem.AssetType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("AssetID");
            writer.WriteString(inventoryItem.AssetID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SaleType");
            writer.WriteString(inventoryItem.SaleType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SalePrice");
            writer.WriteString(inventoryItem.SalePrice.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("BasePermissions");
            writer.WriteString(inventoryItem.BasePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CurrentPermissions");
            writer.WriteString(inventoryItem.CurrentPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("EveryOnePermssions");
            writer.WriteString(inventoryItem.EveryOnePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("NextPermissions");
            writer.WriteString(inventoryItem.NextPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Flags");
            writer.WriteString(inventoryItem.Flags.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupID");
            writer.WriteString(inventoryItem.GroupID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupOwned");
            writer.WriteString(inventoryItem.GroupOwned.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("ParentFolderID");
            writer.WriteString(inventoryItem.Folder.ToString());
            writer.WriteEndElement();
            writer.WriteEndElement();

            archive.AddFile(filename, sw.ToString());

            assetUuids[inventoryItem.AssetID] = 1;
        }

        protected void saveInvDir(InventoryFolderImpl inventoryFolder, string path)
        {
            List<InventoryFolderImpl> inventories = inventoryFolder.RequestListOfFolderImpls();
            List<InventoryItemBase> items = inventoryFolder.RequestListOfItems();
            string newPath = path + inventoryFolder.Name + InventoryFolderImpl.PATH_DELIMITER;
            archive.AddDir(newPath);
            foreach (InventoryFolderImpl folder in inventories)
            {
                saveInvDir(folder, newPath);
            }
            foreach (InventoryItemBase item in items)
            {
                saveInvItem(item, newPath);
            }
        }

        public void Execute()
        {
            InventoryFolderImpl inventoryFolder = null;
            InventoryItemBase inventoryItem = null;

            if (!m_userInfo.HasReceivedInventory)
            {            
                // If the region server has access to the user admin service (by which users are created), 
                // then we'll assume that it's okay to fiddle with the user's inventory even if they are not on the 
                // server.
                //
                // FIXME: FetchInventory should probably be assumed to by async anyway, since even standalones might
                // use a remote inventory service, though this is vanishingly rare at the moment.
                if (null == commsManager.UserAdminService)
                {
                    m_log.ErrorFormat(
                        "[INVENTORY ARCHIVER]: Have not yet received inventory info for user {0} {1}",
                        m_userInfo.UserProfile.Name, m_userInfo.UserProfile.ID);

                    return;
                }
                else
                {
                    m_userInfo.FetchInventory();
                }
            }
            
            // Eliminate double slashes and any leading / on the path.  This might be better done within InventoryFolderImpl
            // itself (possibly at a small loss in efficiency).
            string[] components
                = m_invPath.Split(new string[] { InventoryFolderImpl.PATH_DELIMITER }, StringSplitOptions.RemoveEmptyEntries);
            m_invPath = String.Empty;
            foreach (string c in components)
            {
                m_invPath += c + InventoryFolderImpl.PATH_DELIMITER;
            }

            // Annoyingly Split actually returns the original string if the input string consists only of delimiters
            // Therefore if we still start with a / after the split, then we need the root folder
            if (m_invPath.Length == 0)
            {
                inventoryFolder = m_userInfo.RootFolder;
            }
            else
            {
                m_invPath = m_invPath.Remove(m_invPath.LastIndexOf(InventoryFolderImpl.PATH_DELIMITER));
                inventoryFolder = m_userInfo.RootFolder.FindFolderByPath(m_invPath);
            }

            // The path may point to an item instead
            if (inventoryFolder == null)
            {
                inventoryItem = m_userInfo.RootFolder.FindItemByPath(m_invPath);
            }

            if (null == inventoryFolder)
            {
                if (null == inventoryItem)
                {
                    m_log.ErrorFormat("[INVENTORY ARCHIVER]: Could not find inventory entry at path {0}", m_invPath);
                    return;
                }
                else
                {
                    m_log.InfoFormat(
                        "[INVENTORY ARCHIVER]: Found item {0} {1} at {2}", 
                        inventoryItem.Name, inventoryItem.ID, m_invPath);
                    
                    //get and export item info
                    saveInvItem(inventoryItem, m_invPath);
                }
            }
            else
            {
                m_log.InfoFormat(
                    "[INVENTORY ARCHIVER]: Found folder {0} {1} at {2}", 
                    inventoryFolder.Name, inventoryFolder.ID, m_invPath);
                
                //recurse through all dirs getting dirs and files
                saveInvDir(inventoryFolder, "");
            }

            new AssetsRequest(assetUuids.Keys, commsManager.AssetCache, ReceivedAllAssets).Execute();
        }
    }
}