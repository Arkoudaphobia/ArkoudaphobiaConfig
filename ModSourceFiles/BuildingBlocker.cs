using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlocker", "Vlad-00003", "2.1.3", ResourceId = 2456)]
    [Description("Blocks building in the building privilage zone. Deactivates raids update.")]

    class BuildingBlocker : RustPlugin
    {
		private readonly int triggerLayer = LayerMask.GetMask("Trigger");
		
        #region Config setup
        private string BypassPrivilage = "buildingblocker.bypass";
        private string Prefix = "[BuildingBlocker]";
        private string PrefixColor = "#FF3047";
        private bool LadderBuilding = false;
        #endregion

        #region Vars
        private static float CupRadius = 1.8f;
        #endregion

        #region Localization
        private string BypassPrivilageCfg = "Bypass block privilage";
        private string PrefixCfg = "Chat prefix";
        private string PrefixColorCfg = "Prefix color";
        private string LadderBuildingCfg = "Allow building ladders in the privilage zone";
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building blocked"] = "Building is blocked."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building blocked"] = "Строительство заблокировано."
            }, this, "ru");
        }
        #endregion

        #region Init
        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
        }
        private void LoadConfigValues()
        {
            GetConfig(BypassPrivilageCfg, ref BypassPrivilage);
            GetConfig(PrefixCfg, ref Prefix);
            GetConfig(PrefixColorCfg, ref PrefixColor);
            GetConfig(LadderBuildingCfg, ref LadderBuilding);
            SaveConfig();
        }
        void Loaded()
        {
            LoadConfigValues();
            LoadMessages();
            permission.RegisterPermission(BypassPrivilage, this);

        }
        #endregion

        #region Main
        object CanBuild(Planner plan, Construction prefab, Vector3 location)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!player) return null;
            if (permission.UserHasPermission(player.UserIDString, BypassPrivilage)) return null;
            if (LadderBuilding && prefab.fullName.Contains("ladder.wooden")) return null;
            
            var pos = player.ServerPosition;
            pos.y += player.GetHeight();
            var buildPos = pos + (player.eyes.BodyForward() * 4f);

            if (!CanBuildHere(player, buildPos))
            {
                SendToChat(player, GetMsg("Building blocked", player.UserIDString));
                return false;
            }
            return null;
        }

        private bool CanBuildHere(BasePlayer player, Vector3 targetLocation)
        {
            var entities = Pool.GetList<BaseCombatEntity>();
            Vis.Entities(targetLocation, CupRadius, entities, triggerLayer);
            if (entities.Count > 0)
            {
                foreach (var entity in entities)
                {
                    var cup = entity.GetComponentInParent<BuildingPrivlidge>();
                    if (cup == null) continue;

                    if (cup.IsAuthed(player))
                    {
                        Pool.FreeList(ref entities);
                        return true;
                    }
                    else
                    {
                        Pool.FreeList(ref entities);
                        return false;
                    }
                }
            }
            Pool.FreeList(ref entities);
            return true;
        }
        #endregion

        #region Helpers
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        #endregion
    }
}