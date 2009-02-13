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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Permissions
{
    public class PermissionsModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
                
        protected Scene m_scene;

        #region Constants
        // These are here for testing.  They will be taken out

        //private uint PERM_ALL = (uint)2147483647;
        private uint PERM_COPY = (uint)32768;
        //private uint PERM_MODIFY = (uint)16384;
        private uint PERM_MOVE = (uint)524288;
        //private uint PERM_TRANS = (uint)8192;
        private uint PERM_LOCKED = (uint)540672;  
        
        /// <value>
        /// Different user set names that come in from the configuration file.
        /// </value>
        enum UserSet
        {
            All,
            Administrators
        };
        
        #endregion   

        #region Bypass Permissions / Debug Permissions Stuff

        // Bypasses the permissions engine
        private bool m_bypassPermissions = true;
        private bool m_bypassPermissionsValue = true;
        private bool m_propagatePermissions = false;
        private bool m_debugPermissions = false;
        private bool m_allowGridGods = false;
        private bool m_RegionOwnerIsGod = false;
        private bool m_ParcelOwnerIsGod = false;
        
        /// <value>
        /// The set of users that are allowed to create scripts.  This is only active if permissions are not being
        /// bypassed.  This overrides normal permissions.
        /// </value>
        private UserSet m_allowedScriptCreators = UserSet.All;

        /// <value>
        /// The set of users that are allowed to edit (save) scripts.  This is only active if 
        /// permissions are not being bypassed.  This overrides normal permissions.-
        /// </value>        
        private UserSet m_allowedScriptEditors = UserSet.All;

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;

            IConfig myConfig = config.Configs["Startup"];

            string permissionModules = myConfig.GetString("permissionmodules", "DefaultPermissionsModule");

            List<string> modules=new List<string>(permissionModules.Split(','));

            if (!modules.Contains("DefaultPermissionsModule"))
                return;

            m_allowGridGods = myConfig.GetBoolean("allow_grid_gods", false);
            m_bypassPermissions = !myConfig.GetBoolean("serverside_object_permissions", true);
            m_propagatePermissions = myConfig.GetBoolean("propagate_permissions", true);
            m_RegionOwnerIsGod = myConfig.GetBoolean("region_owner_is_god", true);
            m_ParcelOwnerIsGod = myConfig.GetBoolean("parcel_owner_is_god", true);
            
            m_allowedScriptCreators 
                = ParseUserSetConfigSetting(myConfig, "allowed_script_creators", m_allowedScriptCreators);
            m_allowedScriptEditors
                = ParseUserSetConfigSetting(myConfig, "allowed_script_editors", m_allowedScriptEditors);

            if (m_bypassPermissions)
                m_log.Info("[PERMISSIONS]: serviceside_object_permissions = false in ini file so disabling all region service permission checks");
            else
                m_log.Debug("[PERMISSIONS]: Enabling all region service permission checks");

            //Register functions with Scene External Checks!
            m_scene.Permissions.AddBypassPermissionsHandler(BypassPermissions); //FULLY IMPLEMENTED
            m_scene.Permissions.AddSetBypassPermissionsHandler(SetBypassPermissions); //FULLY IMPLEMENTED
            m_scene.Permissions.AddPropagatePermissionsHandler(PropagatePermissions); //FULLY IMPLEMENTED
            m_scene.Permissions.AddGenerateClientFlagsHandler(GenerateClientFlags); //NOT YET FULLY IMPLEMENTED
            m_scene.Permissions.AddAbandonParcelHandler(CanAbandonParcel); //FULLY IMPLEMENTED
            m_scene.Permissions.AddReclaimParcelHandler(CanReclaimParcel); //FULLY IMPLEMENTED
            m_scene.Permissions.AddIsGodHandler(IsGod); //FULLY IMPLEMENTED
            m_scene.Permissions.AddDuplicateObjectHandler(CanDuplicateObject); //FULLY IMPLEMENTED
            m_scene.Permissions.AddDeleteObjectHandler(CanDeleteObject); //MAYBE FULLY IMPLEMENTED
            m_scene.Permissions.AddEditObjectHandler(CanEditObject);//MAYBE FULLY IMPLEMENTED
            m_scene.Permissions.AddEditParcelHandler(CanEditParcel); //FULLY IMPLEMENTED            
            m_scene.Permissions.AddInstantMessageHandler(CanInstantMessage); //FULLY IMPLEMENTED
            m_scene.Permissions.AddInventoryTransferHandler(CanInventoryTransfer); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddIssueEstateCommandHandler(CanIssueEstateCommand); //FULLY IMPLEMENTED
            m_scene.Permissions.AddMoveObjectHandler(CanMoveObject); //HOPEFULLY FULLY IMPLEMENTED
            m_scene.Permissions.AddObjectEntryHandler(CanObjectEntry); //FULLY IMPLEMENTED
            m_scene.Permissions.AddReturnObjectHandler(CanReturnObject); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddRezObjectHandler(CanRezObject); //HOPEFULLY FULLY IMPLEMENTED
            m_scene.Permissions.AddRunConsoleCommandHandler(CanRunConsoleCommand); //FULLY IMPLEMENTED
            m_scene.Permissions.AddRunScriptHandler(CanRunScript); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddSellParcelHandler(CanSellParcel); //FULLY IMPLEMENTED
            m_scene.Permissions.AddTakeObjectHandler(CanTakeObject); //FULLY IMPLEMENTED
            m_scene.Permissions.AddTakeCopyObjectHandler(CanTakeCopyObject); //FULLY IMPLEMENTED
            m_scene.Permissions.AddTerraformLandHandler(CanTerraformLand); //FULL IMPLEMENTED (POINT ONLY!!! NOT AREA!!!)
            m_scene.Permissions.AddCanLinkObjectHandler(CanLinkObject); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanDelinkObjectHandler(CanDelinkObject); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanBuyLandHandler(CanBuyLand); //NOT YET IMPLEMENTED
            
            m_scene.Permissions.AddViewNotecardHandler(CanViewNotecard); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddViewScriptHandler(CanViewScript); //NOT YET IMPLEMENTED                       
            m_scene.Permissions.AddEditNotecardHandler(CanEditNotecard); //NOT YET IMPLEMENTED            
            m_scene.Permissions.AddEditScriptHandler(CanEditScript); //NOT YET IMPLEMENTED            
            
            m_scene.Permissions.AddCanCreateObjectInventoryHandler(CanCreateObjectInventory); //NOT IMPLEMENTED HERE 
            m_scene.Permissions.AddEditObjectInventoryHandler(CanEditObjectInventory);//MAYBE FULLY IMPLEMENTED            
            m_scene.Permissions.AddCanCopyObjectInventoryHandler(CanCopyObjectInventory); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanDeleteObjectInventoryHandler(CanDeleteObjectInventory); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddResetScriptHandler(CanResetScript);
            
            m_scene.Permissions.AddCanCreateUserInventoryHandler(CanCreateUserInventory); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanCopyUserInventoryHandler(CanCopyUserInventory); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanEditUserInventoryHandler(CanEditUserInventory); //NOT YET IMPLEMENTED
            m_scene.Permissions.AddCanDeleteUserInventoryHandler(CanDeleteUserInventory); //NOT YET IMPLEMENTED
            
            m_scene.Permissions.AddCanTeleportHandler(CanTeleport); //NOT YET IMPLEMENTED

            m_scene.AddCommand(this, "bypass permissions",
                    "bypass permissions <true / false>",
                    "Bypass permission checks",
                    HandleBypassPermissions);

            m_scene.AddCommand(this, "force permissions",
                    "force permissions <true / false>",
                    "Force permissions on or off",
                    HandleForcePermissions);

            m_scene.AddCommand(this, "debug permissions",
                    "debug permissions <true / false>",
                    "Enable permissions debugging",
                    HandleDebugPermissions);
        }

        public void HandleBypassPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissions = val;

                m_log.InfoFormat(
                    "[PERMISSIONS]: Set permissions bypass to {0} for {1}", 
                    m_bypassPermissions, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleForcePermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (!m_bypassPermissions)
            {
                m_log.Error("[PERMISSIONS] Permissions can't be forced unless they are bypassed first");
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissionsValue = val;

                m_log.InfoFormat("[PERMISSIONS] Forced permissions to {0} in {1}", m_bypassPermissionsValue, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleDebugPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_debugPermissions = val;

                m_log.InfoFormat("[PERMISSIONS] Set permissions debugging to {0} in {1}", m_debugPermissions, m_scene.RegionInfo.RegionName);
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PermissionsModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Helper Functions
        protected void SendPermissionError(UUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }
        
        protected void DebugPermissionInformation(string permissionCalled)
        {
            if (m_debugPermissions)
                m_log.Debug("[PERMISSIONS]: " + permissionCalled + " was called from " + m_scene.RegionInfo.RegionName);
        }
        
        /// <summary>
        /// Parse a user set configuration setting
        /// </summary>
        /// <param name="config"></param>
        /// <param name="settingName"></param>
        /// <param name="defaultValue">The default value for this attribute</param>
        /// <returns>The parsed value</returns>
        private static UserSet ParseUserSetConfigSetting(IConfig config, string settingName, UserSet defaultValue)
        {
            UserSet userSet = defaultValue;
            
            string rawSetting = config.GetString(settingName, defaultValue.ToString());
            
            // Temporary measure to allow 'gods' to be specified in config for consistency's sake.  In the long term
            // this should disappear.
            if ("gods" == rawSetting.ToLower())
                rawSetting = UserSet.Administrators.ToString();
            
            // Doing it this was so that we can do a case insensitive conversion
            try
            {
                userSet = (UserSet)Enum.Parse(typeof(UserSet), rawSetting, true);
            }
            catch 
            {
                m_log.ErrorFormat(
                    "[PERMISSIONS]: {0} is not a valid {1} value, setting to {2}",
                    rawSetting, settingName, userSet);
            }            
            
            m_log.DebugFormat("[PERMISSIONS]: {0} {1}", settingName, userSet);
            
            return userSet;
        }

        /// <summary>
        /// Is the given user an administrator (in other words, a god)?
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected bool IsAdministrator(UUID user)
        {
            if (m_scene.RegionInfo.MasterAvatarAssignedUUID != UUID.Zero)
            {
                if (m_RegionOwnerIsGod && (m_scene.RegionInfo.MasterAvatarAssignedUUID == user))
                    return true;
            }
            
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
            {
                if (m_scene.RegionInfo.EstateSettings.EstateOwner == user)
                    return true;
            }
            
            if (m_allowGridGods)
            {
                CachedUserInfo profile = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(user);
                if (profile != null && profile.UserProfile != null)
                {
                    if (profile.UserProfile.GodLevel >= 200)
                        return true;
                }
                //else
                //{
                //    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for administrator check", user);
                //}
            }

            return false;
        }

        protected bool IsEstateManager(UUID user)
        {
            return m_scene.RegionInfo.EstateSettings.IsEstateManager(user);
        }
#endregion

        public bool PropagatePermissions()
        {
            if (m_bypassPermissions)
                return false;

            return m_propagatePermissions;
        }

        public bool BypassPermissions()
        {
            return m_bypassPermissions;
        }

        public void SetBypassPermissions(bool value)
        {
            m_bypassPermissions=value;
        }

        #region Object Permissions

        public uint GenerateClientFlags(UUID user, UUID objID)
        {
            // Here's the way this works,
            // ObjectFlags and Permission flags are two different enumerations
            // ObjectFlags, however, tells the client to change what it will allow the user to do.
            // So, that means that all of the permissions type ObjectFlags are /temporary/ and only
            // supposed to be set when customizing the objectflags for the client.

            // These temporary objectflags get computed and added in this function based on the
            // Permission mask that's appropriate!
            // Outside of this method, they should never be added to objectflags!
            // -teravus

            SceneObjectPart task = m_scene.GetSceneObjectPart(objID);

            // this shouldn't ever happen..     return no permissions/objectflags.
            if (task == null)
                return (uint)0;

            uint objflags = task.GetEffectiveObjectFlags();
            UUID objectOwner = task.OwnerID;


            // Remove any of the objectFlags that are temporary.  These will get added back if appropriate
            // in the next bit of code

            // libomv will moan about PrimFlags.ObjectYouOfficer being
            // deprecated
            #pragma warning disable 0612 
            objflags &= (uint)
                ~(PrimFlags.ObjectCopy | // Tells client you can copy the object
                  PrimFlags.ObjectModify | // tells client you can modify the object
                  PrimFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
                  PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
                  PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
                  PrimFlags.ObjectAnyOwner | // Tells client that someone owns the object
                  PrimFlags.ObjectOwnerModify | // Tells client that you're the owner of the object
                  PrimFlags.ObjectYouOfficer // Tells client that you've got group object editing permission. Used when ObjectGroupOwned is set
                    );
            #pragma warning restore 0612

            // Creating the three ObjectFlags options for this method to choose from.
            // Customize the OwnerMask
            uint objectOwnerMask = ApplyObjectModifyMasks(task.OwnerMask, objflags);
            objectOwnerMask |= (uint)PrimFlags.ObjectYouOwner | (uint)PrimFlags.ObjectAnyOwner | (uint)PrimFlags.ObjectOwnerModify;

            // Customize the GroupMask
            // uint objectGroupMask = ApplyObjectModifyMasks(task.GroupMask, objflags);

            // Customize the EveryoneMask
            uint objectEveryoneMask = ApplyObjectModifyMasks(task.EveryoneMask, objflags);


            // Hack to allow collaboration until Groups and Group Permissions are implemented
            if ((objectEveryoneMask & (uint)PrimFlags.ObjectMove) != 0)
                objectEveryoneMask |= (uint)PrimFlags.ObjectModify;

            if (m_bypassPermissions)
                return objectOwnerMask;

            // Object owners should be able to edit their own content
            if (user == objectOwner)
            {
                return objectOwnerMask;
            }

            // Users should be able to edit what is over their land.
            ILandObject parcel = m_scene.LandChannel.GetLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);
            if (parcel != null && parcel.landData.OwnerID == user && m_ParcelOwnerIsGod)
                return objectOwnerMask;

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
                return objectEveryoneMask;

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(user) && m_RegionOwnerIsGod)
                return objectOwnerMask;

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                return objectOwnerMask;


            return objectEveryoneMask;
        }

        private uint ApplyObjectModifyMasks(uint setPermissionMask, uint objectFlagsMask)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the
            // permission flag given.  These change the F flags on the client.

            if ((setPermissionMask & (uint)PermissionMask.Copy) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectCopy;
            }

            if ((setPermissionMask & (uint)PermissionMask.Move) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectMove;
            }

            if ((setPermissionMask & (uint)PermissionMask.Modify) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectModify;
            }

            if ((setPermissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectTransfer;
            }

            return objectFlagsMask;
        }

        /// <summary>
        /// General permissions checks for any operation involving an object.  These supplement more specific checks
        /// implemented by callers.
        /// </summary>
        /// <param name="currentUser"></param>
        /// <param name="objId"></param>
        /// <param name="denyOnLocked"></param>
        /// <returns></returns>
        protected bool GenericObjectPermission(UUID currentUser, UUID objId, bool denyOnLocked)
        {
            // Default: deny
            bool permission = false;
            bool locked = false;

            if (!m_scene.Entities.ContainsKey(objId))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if ((!(m_scene.Entities[objId] is SceneObjectGroup)))
            {
                return false;
            }

            SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objId];

            UUID objectOwner = group.OwnerID;
            locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);

            // People shouldn't be able to do anything with locked objects, except the Administrator
            // The 'set permissions' runs through a different permission check, so when an object owner
            // sets an object locked, the only thing that they can do is unlock it.
            //
            // Nobody but the object owner can set permissions on an object
            //

            if (locked && (!IsAdministrator(currentUser)) && denyOnLocked)
            {
                return false;
            }

            // Object owners should be able to edit their own content
            if (currentUser == objectOwner)
            {
                permission = true;
            }
            else if (group.IsAttachment)
            {
                permission = false;
            }

            // Users should be able to edit what is over their land.
            ILandObject parcel = m_scene.LandChannel.GetLandObject(group.AbsolutePosition.X, group.AbsolutePosition.Y);
            if ((parcel != null) && (parcel.landData.OwnerID == currentUser))
            {
                permission = true;
            }

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(currentUser))
            {
                permission = true;
            }

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
            {
                permission = false;
            }

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(currentUser))
            {
                permission = true;
            }

            return permission;
        }

        #endregion

        #region Generic Permissions
        protected bool GenericCommunicationPermission(UUID user, UUID target)
        {
            // Setting this to true so that cool stuff can happen until we define what determines Generic Communication Permission
            bool permission = true;
            string reason = "Only registered users may communicate with another account.";

            // Uhh, we need to finish this before we enable it..   because it's blocking all sorts of goodies and features
            if (IsAdministrator(user))
                permission = true;

            if (IsEstateManager(user))
                permission = true;

            if (!permission)
                SendPermissionError(user, reason);

            return permission;
        }

        public bool GenericEstatePermission(UUID user)
        {
            // Default: deny
            bool permission = false;

            // Estate admins should be able to use estate tools
            if (IsEstateManager(user))
                permission = true;

            // Administrators always have permission
            if (IsAdministrator(user))
                permission = true;

            return permission;
        }

        protected bool GenericParcelPermission(UUID user, ILandObject parcel)
        {
            bool permission = false;

            if (parcel.landData.OwnerID == user)
            {
                permission = true;
            }

            if (parcel.landData.IsGroupOwned)
            {
                // TODO: Need to do some extra checks here. Requires group code.
            }

            if (IsEstateManager(user))
            {
                permission = true;
            }

            if (IsAdministrator(user))
            {
                permission = true;
            }

            return permission;
        }

        protected bool GenericParcelPermission(UUID user, Vector3 pos)
        {
            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel == null) return false;
            return GenericParcelPermission(user, parcel);
        }
#endregion

        #region Permission Checks
        private bool CanAbandonParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelPermission(user, parcel);
        }

        private bool CanReclaimParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelPermission(user, parcel);
        }

        private bool IsGod(UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return IsAdministrator(user);
        }

        private bool CanDuplicateObject(int objectCount, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (!GenericObjectPermission(owner, objectID, true))
            {
                //They can't even edit the object
                return false;
            }

            SceneObjectPart part = scene.GetSceneObjectPart(objectID);
            if (part == null)
                return false;

            if ((part.OwnerMask & PERM_COPY) == 0)
                return false;

            if ((part.ParentGroup.GetEffectivePermissions() & PERM_COPY) == 0)
                return false;

            //If they can rez, they can duplicate
            return CanRezObject(objectCount, owner, objectPosition, scene);
        }

        private bool CanDeleteObject(UUID objectID, UUID deleter, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(deleter, objectID, false);
        }

        private bool CanEditObject(UUID objectID, UUID editorID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;


            return GenericObjectPermission(editorID, objectID, false);
        }

        private bool CanEditObjectInventory(UUID objectID, UUID editorID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);

            // If we selected a sub-prim to edit, the objectID won't represent the object, but only a part.
            // We have to check the permissions of the group, though.
            if (part.ParentID != 0)
            {
                objectID = part.ParentUUID;
                part = m_scene.GetSceneObjectPart(objectID);
            }

            // TODO: add group support!
            //
            if (part.OwnerID != editorID)
                return false;

            return GenericObjectPermission(editorID, objectID, false);
        }

        private bool CanEditParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelPermission(user, parcel);
        }

        /// <summary>
        /// Check whether the specified user can edit the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;
                            
            if (m_allowedScriptEditors == UserSet.Administrators && !IsAdministrator(user))
                return false;   
            
            // Ordinarily, if you can view it, you can edit it
            // There is no viewing a no mod script
            //
            return CanViewScript(script, objectID, user, scene);
        }

        /// <summary>
        /// Check whether the specified user can edit the given notecard
        /// </summary>
        /// <param name="notecard"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>        
        private bool CanEditNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserProfileCacheService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for edit notecard check", user);
                    return false;
                }                                

                if (userInfo.RootFolder == null)
                    return false;

                InventoryItemBase assetRequestItem = userInfo.RootFolder.FindItem(notecard);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = scene.CommsManager.UserProfileCacheService.LibraryRoot.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards must be both mod and copy to be saveable
                // This is because of they're not copy, you can't read
                // them, and if they're not mod, well, then they're
                // not mod. Duh.
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;

                if (part.OwnerID != user)
                    return false;

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                    return false;

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }

            return true;
        }

        private bool CanInstantMessage(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // If the sender is an object, check owner instead
            //
            SceneObjectPart part = startScene.GetSceneObjectPart(user);
            if (part != null)
                user = part.OwnerID;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanInventoryTransfer(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanIssueEstateCommand(UUID user, Scene requestFromScene, bool ownerCommand)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (IsAdministrator(user))
                return true;

            if (m_scene.RegionInfo.EstateSettings.IsEstateOwner(user))
                return true;

            if (ownerCommand)
                return false;

            return GenericEstatePermission(user);
        }

        private bool CanMoveObject(UUID objectID, UUID moverID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);
                if (part.OwnerID != moverID)
                {
                    if (part.ParentGroup != null && !part.ParentGroup.IsDeleted)
                    {
                        if (part.ParentGroup.IsAttachment)
                            return false;
                    }
                }
                return m_bypassPermissionsValue;
            }

            bool permission = GenericObjectPermission(moverID, objectID, true);
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // The client
                // may request to edit linked parts, and therefore, it needs
                // to also check for SceneObjectPart

                // If it's not an object, we cant edit it.
                if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                {
                    return false;
                }


                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];


                // UUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for
                // the administrator object permissions to take effect.
                // UUID objectOwner = task.OwnerID;

                // Anyone can move
                if ((task.RootPart.EveryoneMask & PERM_MOVE) != 0)
                    permission = true;

                // Locked
                if ((task.RootPart.OwnerMask & PERM_LOCKED) == 0)
                    permission = false;
            }
            else
            {
                bool locked = false;
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                {
                    return false;
                }

                SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objectID];

                UUID objectOwner = group.OwnerID;
                locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);

                // This is an exception to the generic object permission.
                // Administrators who lock their objects should not be able to move them,
                // however generic object permission should return true.
                // This keeps locked objects from being affected by random click + drag actions by accident
                // and allows the administrator to grab or delete a locked object.

                // Administrators and estate managers are still able to click+grab locked objects not
                // owned by them in the scene
                // This is by design.

                if (locked && (moverID == objectOwner))
                    return false;
            }
            return permission;
        }

        private bool CanObjectEntry(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((newPoint.X > 257f || newPoint.X < -1f || newPoint.Y > 257f || newPoint.Y < -1f))
            {
                return true;
            }

            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

            ILandObject land = m_scene.LandChannel.GetLandObject(newPoint.X, newPoint.Y);

            if (!enteringRegion)
            {
                ILandObject fromland = m_scene.LandChannel.GetLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);

                if (fromland == land) // Not entering
                    return true;
            }

            if (land == null)
            {
                return false;
            }

            if ((land.landData.Flags & ((int)Parcel.ParcelFlags.AllowAPrimitiveEntry)) != 0)
            {
                return true;
            }

            //TODO: check for group rights

            if (!m_scene.Entities.ContainsKey(objectID))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objectID] is SceneObjectGroup))
            {
                return false;
            }


            if (GenericParcelPermission(task.OwnerID, newPoint))
            {
                return true;
            }

            //Otherwise, false!
            return false;
        }

        private bool CanReturnObject(UUID objectID, UUID returnerID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(returnerID, objectID, false);
        }

        private bool CanRezObject(int objectCount, UUID owner, Vector3 objectPosition, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            bool permission = false;

            ILandObject land = m_scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);
            if (land == null) return false;

            if ((land.landData.Flags & ((int)Parcel.ParcelFlags.CreateObjects)) ==
                (int)Parcel.ParcelFlags.CreateObjects)
                permission = true;

            //TODO: check for group rights

            if (IsAdministrator(owner))
            {
                permission = true;
            }

            if (GenericParcelPermission(owner, objectPosition))
            {
                permission = true;
            }

            return permission;
        }

        private bool CanRunConsoleCommand(UUID user, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;


            return IsAdministrator(user);
        }

        private bool CanRunScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanSellParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelPermission(user, parcel);
        }

        private bool CanTakeObject(UUID objectID, UUID stealer, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(stealer,objectID, false);
        }

        private bool CanTakeCopyObject(UUID objectID, UUID userID, Scene inScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            bool permission = GenericObjectPermission(userID, objectID,false);
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if (!(m_scene.Entities[objectID] is SceneObjectGroup))
                {
                    return false;
                }

                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];
                // UUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for
                // the administrator object permissions to take effect.
                // UUID objectOwner = task.OwnerID;

                if ((task.RootPart.EveryoneMask & PERM_COPY) != 0)
                    permission = true;

                if ((task.GetEffectivePermissions() & PERM_COPY) == 0)
                    permission = false;
            }
            else
            {
                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

                if ((task.GetEffectivePermissions() & PERM_COPY) == 0)
                    permission = false;
            }
            
            return permission;
        }

        private bool CanTerraformLand(UUID user, Vector3 position, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // Estate override
            if (GenericEstatePermission(user))
                return true;

            float X = position.X;
            float Y = position.Y;

            if (X > 255)
                X = 255;
            if (Y > 255)
                Y = 255;
            if (X < 0)
                X = 0;
            if (Y < 0)
                Y = 0;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(X, Y);
            if (parcel == null)
                return false;

            // Others allowed to terraform?
            if ((parcel.landData.Flags & ((int)Parcel.ParcelFlags.AllowTerraform)) != 0)
                return true;

            // Land owner can terraform too
            if (parcel != null && GenericParcelPermission(user, parcel))
                return true;

            return false;
        }

        /// <summary>
        /// Check whether the specified user can view the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>        
        private bool CanViewScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;           

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserProfileCacheService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for administrator check", user);
                    return false;
                }                  

                if (userInfo.RootFolder == null)
                    return false;

                InventoryItemBase assetRequestItem = userInfo.RootFolder.FindItem(script);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = m_scene.CommsManager.UserProfileCacheService.LibraryRoot.FindItem(script);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // SL is rather harebrained here. In SL, a script you
                // have mod/copy no trans is readable. This subverts
                // permissions, but is used in some products, most
                // notably Hippo door plugin and HippoRent 5 networked
                // prim counter.
                // To enable this broken SL-ism, remove Transfer from
                // the below expressions.
                // Trying to improve on SL perms by making a script
                // readable only if it's really full perms
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;

                if (part.OwnerID != user)
                    return false;

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(script);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                    return false;

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check whether the specified user can view the given notecard
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>         
        private bool CanViewNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                CachedUserInfo userInfo =
                        scene.CommsManager.UserProfileCacheService.GetUserDetails(user);
            
                if (userInfo == null)
                {
                    m_log.ErrorFormat("[PERMISSIONS]: Could not find user {0} for view notecard check", user);
                    return false;
                }    

                if (userInfo.RootFolder == null)
                    return false;

                InventoryItemBase assetRequestItem = userInfo.RootFolder.FindItem(notecard);
                if (assetRequestItem == null) // Library item
                {
                    assetRequestItem = m_scene.CommsManager.UserProfileCacheService.LibraryRoot.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards are always readable unless no copy
                //
                if ((assetRequestItem.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;

                if (part.OwnerID != user)
                    return false;

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                    return false;

                // Notecards are always readable unless no copy
                //
                if ((ti.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }

            return true;
        }

    #endregion

        private bool CanLinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanDelinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanBuyLand(UUID userID, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to directly create the given inventory type in a prim's
        /// inventory (e.g. the New Script button in the 1.21 Linden Lab client).
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="objectID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((int)InventoryType.LSL == invType)
                if (m_allowedScriptCreators == UserSet.Administrators && !IsAdministrator(userID))
                    return false;
            
            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to create the given inventory type in their inventory.
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanCreateUserInventory(int invType, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((int)InventoryType.LSL == invType)
                if (m_allowedScriptCreators == UserSet.Administrators && !IsAdministrator(userID))
                    return false;
            
            return true;            
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to copy the given inventory type in their inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanCopyUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }        
        
        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanEditUserInventory(UUID itemID, UUID userID)
        {            
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to delete the given inventory item from their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>           
        private bool CanDeleteUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;            
        }        

        private bool CanTeleport(UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanResetScript(UUID prim, UUID script, UUID agentID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(prim);

            // If we selected a sub-prim to reset, prim won't represent the object, but only a part.
            // We have to check the permissions of the object, though.
            if (part.ParentID != 0) prim = part.ParentUUID;

            // You can reset the scripts in any object you can edit
            return GenericObjectPermission(agentID, prim, false);
        }
    }
}