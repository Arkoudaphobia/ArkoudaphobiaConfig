using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingBlocker", "Vlad-00003", "2.1.1", ResourceId = 2456)]
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
        //private Dictionary<string, ItemDefinition> PrefabToItem = new Dictionary<string, ItemDefinition>();
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
        void OnServerInitialized()
        {
            //foreach(var item in ItemManager.GetItemDefinitions())
            //{
            //    var itemdeployable = item?.GetComponent<ItemModDeployable>();
            //    if (itemdeployable == null) continue;

            //    if (!PrefabToItem.ContainsKey(itemdeployable.entityPrefab.resourcePath)) PrefabToItem.Add(itemdeployable.entityPrefab.resourcePath, item);
            //}
        }
        #endregion

        #region Main
        object CanBuild(Planner plan, Construction prefab, Vector3 location)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            if (!player) return null;
            if (permission.UserHasPermission(player.UserIDString, BypassPrivilage)) return null;
            if (LadderBuilding && prefab.fullName.Contains("ladder.wooden")) return null;

            Vector3 pos = new Vector3(0, 0, 0);
            if(location == pos)
            {
                pos = player.ServerPosition;
            }else
            {
                pos = location;
            }

            if (!CanBuildHere(player, pos))
            {
                SendToChat(player, GetMsg("Building blocked", player.UserIDString));
                return false;
            }
            return null;
        }
        //void OnEntityBuilt(Planner planner, GameObject go)
        //{
        //    var player = planner.GetOwnerPlayer();
        //    if (player == null) return;
        //    if (permission.UserHasPermission(player.UserIDString, BypassPrivilage)) return;
        //    Vector3 pos = go.transform.position;
        //    if (!CanBuildHere(player, pos) && !go.name.Contains("cupboard.tool.deployed"))
        //    {
        //        SendToChat(player, GetMsg("Building blocked", player.UserIDString));
        //        BaseEntity entity = go.GetComponent<BaseEntity>();
        //        if (entity == null)
        //        {
        //            PrintWarning($"Refund for player {player.displayName} has faild. Entity is null.\nPlease report this error to the plugin thread http://oxidemod.org/threads/building-blocker.25289");
        //            return;
        //        }
        //        Refund(player, entity);
        //        entity.KillMessage();
        //        return;
        //    }
        //}
        private bool CanBuildHere(BasePlayer player, Vector3 targetLocation)
        {
            var colliders = Pool.GetList<Collider>();
            var newpos = targetLocation + Vector3.up * 1.09f;
            Vis.Colliders(newpos, CupRadius, colliders, triggerLayer);
            foreach (var collider in colliders)
            {
                var cup = collider.GetComponentInParent<BuildingPrivlidge>();
                if (cup == null) continue;
                
                if (cup.IsAuthed(player))
                {
                    Pool.FreeList(ref colliders);
                    return true;
                }else
                {
                    Pool.FreeList(ref colliders);
                    return false;
                }
            }
            Pool.FreeList(ref colliders);
            return true;
        }
        //private void Refund(BasePlayer player, BaseEntity entity)
        //{
        //    var buildingblock = entity.GetComponent<BuildingBlock>();
        //    if (buildingblock != null)
        //    {
        //        var grade = buildingblock.grade;
        //        var blockdef = buildingblock.blockDefinition;
        //        if(blockdef == null)
        //        {
        //            PrintWarning($"BlockDef for entity {buildingblock.PrefabName} not found and the player {player.displayName} didn't get refund.\nPlease report this error to the plugin thread http://oxidemod.org/threads/building-blocker.25289");
        //            return;
        //        }
        //        var curGrade = blockdef.grades[(int)grade];
        //        foreach (var item in curGrade.costToBuild)
        //        {
        //            int amount = (int)item.amount;
        //            if (amount < 1) continue;
        //            player.GiveItem(ItemManager.Create(item.itemDef, amount), BaseEntity.GiveItemReason.Generic);
        //        }
        //    }
        //    else
        //    {
        //        var itemDef = PrefabToItem[entity.PrefabName];
        //        if (itemDef == null)
        //        {
        //            PrintWarning($"Item defenition for item {entity.PrefabName} not found and the player {player.displayName} didn't get refund.\nPlease report this error to the plugin thread http://oxidemod.org/threads/building-blocker.25289");
        //            return;
        //        }
        //        foreach (var item in itemDef.Blueprint.ingredients)
        //        {
        //            int amount = (int)item.amount;
        //            if (amount < 1) continue;
        //            player.GiveItem(ItemManager.Create(item.itemDef, amount), BaseEntity.GiveItemReason.Generic);
        //        }
        //    }
        //}
        #endregion

        #region Helpers
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }

        //Перезгрузка функции отправки собщения в чат - отправляет сообщение всем пользователям
        private void SendToChat(string Message)
        {
            PrintToChat("<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
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