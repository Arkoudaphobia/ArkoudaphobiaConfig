// Reference: Newtonsoft.Json
// Reference: Rust.Data
using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System.Reflection;
using System.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System.Text;
using Newtonsoft.Json;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Entity Owner", "Calytic @ cyclone.network", "3.0.1", ResourceId = 1255)]
    [Description("Modify entity ownership and cupboard/turret authorization")]
    class EntityOwner : RustPlugin
    {
        #region Data & Config
        private Dictionary<string, string> messages = new Dictionary<string, string>();
        private int layerMasks = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");

        private int EntityLimit = 8000;
        private float DistanceThreshold = 3f;
        private float CupboardDistanceThreshold = 20f;

        private bool debug = false;

        #endregion

        #region Data Handling & Initialization

        private List<string> texts = new List<string>() {
            "You are not allowed to use this command",
            "Ownership data wiped!",
            "No target found",
            "Owner: {0}",
            "Target player not found",
            "Invalid syntax: /owner",
            "Invalid Syntax. \n/own type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/own player",
            "Invalid Syntax. \n/unown type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/unown player",
            "Invalid Syntax. \n/prod2 type \nTypes:\n all/block/entity/storage/cupboard/sign/sleepingbag/plant/oven/door/turret",
            "Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth",
            "No building or entities found.",
            "Changing ownership..",
            "Removing ownership..",
            "Exceeded entity limit.",
            "Counted {0} entities ({1}/{2})",
            "New owner of all around is: {0}",
            "Owner: You were given ownership of this house and nearby deployables",
            "No entities found.",
            "Prodding structure..",
            "Prodding cupboards..",
            "Count ({0})",
            "Unknown player",
            "Unknown: {0}%",
            "Authorizing cupboards..",
            "Authorized {0} on {1} cupboards",
            "({0}) Authorized",
            "Ownership data expired!",
            "Authorized {0} on {1} turrets",
            "Authorizing turrets..",
            "Prodding turrets..",
            "Deauthorized {0} on {1} turrets",
            "Deauthorizing turrets..",
            "Deauthorizing cupboards..",
            "Deauthorized {0} on {1} cupboards"
        };

        // Loads the default configuration
        protected override void LoadDefaultConfig()
        {
            PrintToConsole("Creating new configuration file");

            Dictionary<string, object> messages = new Dictionary<string, object>();

            foreach (string text in texts)
            {
                if (messages.ContainsKey(text))
                {
                    PrintWarning("Duplicate translation string: " + text);
                }
                else
                {
                    messages.Add(text, text);
                }
            }

            Config["messages"] = messages;
            Config["VERSION"] = this.Version.ToString();
            Config["EntityLimit"] = 8000;
            Config["DistanceThreshold"] = 3.0f;
            Config["CupboardDistanceThreshold"] = 20f;

            Config.Save();
        }

        protected void ReloadConfig()
        {
            Dictionary<string, object> messages = new Dictionary<string, object>();

            foreach (string text in texts)
            {
                if (!messages.ContainsKey(text))
                {
                    messages.Add(text, text);
                }
            }

            Config["messages"] = messages;
            Config["VERSION"] = this.Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole("Upgrading Configuration File");
            SaveConfig();
        }

        // Gets a config value of a specific type
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        void OnServerInitialized()
        {
            try
            {
                LoadConfig();


                this.debug = GetConfig<bool>("Debug", false);
                this.EntityLimit = GetConfig<int>("EntityLimit", 8000);
                this.DistanceThreshold = GetConfig<float>("DistanceThreshold", 3f);
                this.CupboardDistanceThreshold = GetConfig<float>("CupboardDistanceThreshold", 20f);

                if (this.DistanceThreshold >= 5)
                {
                    PrintWarning("ALERT: Distance threshold configuration option is ABOVE 5.  This may cause serious performance degradation (lag) when using EntityOwner commands");
                }

                Dictionary<string, object> customMessages = GetConfig<Dictionary<string, object>>("messages", null);
                if (customMessages != null)
                {
                    foreach (KeyValuePair<string, object> kvp in customMessages.ToList())
                    {
                        messages[kvp.Key] = kvp.Value.ToString();
                    }
                }

                if (!permission.PermissionExists("entityowner.cancheckowners")) permission.RegisterPermission("entityowner.cancheckowners", this);
                if (!permission.PermissionExists("entityowner.canchangeowners")) permission.RegisterPermission("entityowner.canchangeowners", this);

                LoadData();
            }
            catch (Exception ex)
            {
                PrintError("OnServerInitialized failed: " + ex.Message);
            }
        }

        void LoadData()
        {
            if (this.Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                this.ReloadConfig();
            }
            else if (this.GetConfig<string>("VERSION", this.Version.ToString()) != this.Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                this.ReloadConfig();
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            var sb = new StringBuilder();
            if (this.canCheckOwners(player) || this.canChangeOwners(player))
            {
                sb.Append("<size=18>EntityOwner</size> by <color=#ce422b>Calytic</color> at <color=#ce422b>http://cyclone.network</color>\n");
            }

            if (this.canCheckOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/prod</color> - Check ownership of entity you are looking at").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2</color> - Check ownership of entire structure/all deployables").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2 block</color> - Check ownership structure only").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/prod2 cupboard</color> - Check authorization on all nearby cupboards").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/auth</color> - Check authorization list of tool cupboard you are looking at").Append("\n");
            }

            if (this.canChangeOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/own [all/block]</color> - Take ownership of entire structure").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/own [all/block] PlayerName</color> - Give ownership of entire structure to specified player").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/unown [all/block]</color> - Remove ownership from entire structure").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/auth PlayerName</color> - Authorize specified player on all nearby cupboards").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/authclean PlayerName</color> - Remove all building privileges on a player").Append("\n");
            }

            player.ChatMessage(sb.ToString());
        }

        #endregion



        #region Chat Commands

        [ChatCommand("prod")]
        void cmdProd(BasePlayer player, string command, string[] args)
        {
            if (!this.canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }
            if (args == null || (args != null && args.Length == 0))
            {
                //var input = serverinput.GetValue(player) as InputState;
                //var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                //var target = RaycastAll<BaseEntity>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
                var target = RaycastAll<BaseEntity>(player.eyes.HeadRay());
                if (target is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (target is BaseEntity)
                {
                    BaseEntity targetEntity = target as BaseEntity;
                    string owner = GetOwnerName((BaseEntity)target);
                    if (owner == null || owner == String.Empty)
                    {
                        owner = "N/A";
                    }

                    SendReply(player, string.Format(messages["Owner: {0}"], owner) + "\n<color=lightgrey>"+targetEntity.LookupShortPrefabName()+"</color>");
                }
            }
            else
            {
                SendReply(player, messages["Invalid syntax: /owner"]);
            }
        }

        [ChatCommand("own")]
        void cmdOwn(BasePlayer player, string command, string[] args)
        {
            if (!this.canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            bool massTrigger = false;
            string type = null;
            BasePlayer target = null;

            if (args.Length == 0)
            {
                args = new string[1] { "all" };
            }

            if (args.Length > 2)
            {
                SendReply(player, messages["Invalid Syntax. \n/own type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/own player"]);
                return;
            }
            else if (args.Length == 1)
            {
                type = args[0].ToString();
                if (type == "all" || type == "storage" || type == "block" || type == "cupboard" || type == "sign" || type == "sleepingbag" || type == "plant" || type == "oven" || type == "door" || type == "turret")
                {
                    massTrigger = true;
                    target = player;
                }
                else
                {
                    target = FindPlayerByPartialName(type);
                    type = "all";
                    if (target == null)
                    {
                        SendReply(player, messages["Target player not found"]);
                    }
                    else
                    {
                        massTrigger = true;
                    }
                }

            }
            else if (args.Length == 2)
            {
                type = args[0].ToString();
                target = FindPlayerByPartialName(args[1].ToString());
                if (target == null)
                {
                    SendReply(player, messages["Target player not found"]);
                }
                else
                {
                    massTrigger = true;
                }
            }

            if (massTrigger && type != null && target is BasePlayer)
            {
                switch (type)
                {
                    case "all":
                        this.massChangeOwner<BaseEntity>(player, target);
                        break;
                    case "block":
                        this.massChangeOwner<BuildingBlock>(player, target);
                        break;
                    case "storage":
                        this.massChangeOwner<StorageContainer>(player, target);
                        break;
                    case "sign":
                        this.massChangeOwner<Signage>(player, target);
                        break;
                    case "sleepingbag":
                        this.massChangeOwner<SleepingBag>(player, target);
                        break;
                    case "plant":
                        this.massChangeOwner<PlantEntity>(player, target);
                        break;
                    case "oven":
                        this.massChangeOwner<BaseOven>(player, target);
                        break;
                    case "turret":
                        this.massChangeOwner<AutoTurret>(player, target);
                        break;
                    case "door":
                        this.massChangeOwner<Door>(player, target);
                        break;
                    case "cupboard":
                        this.massChangeOwner<BuildingPrivlidge>(player, target);
                        break;
                }
            }
        }

        [ChatCommand("unown")]
        void cmdUnown(BasePlayer player, string command, string[] args)
        {
            if (!this.canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            if (args.Length == 0)
            {
                args = new string[1] { "all" };
            }

            if (args.Length > 1)
            {
                SendReply(player, messages["Invalid Syntax. \n/unown type player\nTypes: all/block/storage/cupboard/sign/sleepingbag/plant/oven/door/turret\n/unown player"]);
                return;
            }
            else if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "all":
                        this.massChangeOwner<BaseEntity>(player);
                        break;
                    case "block":
                        this.massChangeOwner<BuildingBlock>(player);
                        break;
                    case "storage":
                        this.massChangeOwner<StorageContainer>(player);
                        break;
                    case "sign":
                        this.massChangeOwner<Signage>(player);
                        break;
                    case "sleepingbag":
                        this.massChangeOwner<SleepingBag>(player);
                        break;
                    case "plant":
                        this.massChangeOwner<PlantEntity>(player);
                        break;
                    case "oven":
                        this.massChangeOwner<BaseOven>(player);
                        break;
                    case "turret":
                        this.massChangeOwner<AutoTurret>(player);
                        break;
                    case "door":
                        this.massChangeOwner<Door>(player);
                        break;
                    case "cupboard":
                        this.massChangeOwner<BuildingPrivlidge>(player);
                        break;
                }
            }
        }

        [ChatCommand("auth")]
        void cmdAuth(BasePlayer player, string command, string[] args)
        {
            if (!this.canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            bool massCupboard = false;
            bool massTurret = false;
            bool checkCupboard = false;
            bool checkTurret = false;
            bool error = false;
            BasePlayer target = null;

            if (args.Length > 2)
            {
                error = true;
            }
            else if (args.Length == 1)
            {
                if (args[0] == "cupboard")
                {
                    checkCupboard = true;
                }
                else if (args[0] == "turret")
                {
                    checkTurret = true;
                }
                else
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[0]);
                }
            }
            else if (args.Length == 0)
            {
                checkCupboard = true;
            }
            else if (args.Length == 2)
            {
                if (args[0] == "cupboard")
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else if (args[0] == "turret")
                {
                    massTurret = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else
                {
                    error = true;
                }
            }

            if (massTurret || massCupboard)
            {
                if (target == null || target.net == null || target.net.connection == null)
                {
                    SendReply(player, messages["Target player not found"]);
                    return;
                }
            }

            if (error)
            {
                SendReply(player, messages["Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth"]);
                return;
            }

            if (massCupboard && target != null)
            {
                this.massCupboardAuthorize(player, target);
            }

            if (checkCupboard)
            {
                var priv = RaycastAll<BuildingPrivlidge>(player.eyes.HeadRay());
                if (priv is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (priv is BuildingPrivlidge)
                {
                    this.ProdCupboard(player, (BuildingPrivlidge)priv);
                }
            }

            if (massTurret && target != null)
            {
                this.massTurretAuthorize(player, target);
            }

            if (checkTurret)
            {
                var turret = RaycastAll<AutoTurret>(player.eyes.HeadRay());
                if (turret is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (turret is AutoTurret)
                {
                    this.ProdTurret(player, (AutoTurret)turret);
                }
            }
        }

        [ChatCommand("deauth")]
        void cmdDeauth(BasePlayer player, string command, string[] args)
        {
            if (!this.canChangeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            bool massCupboard = false;
            bool massTurret = false;
            bool error = false;
            BasePlayer target = null;

            if (args.Length > 2)
            {
                error = true;
            }
            else if (args.Length == 1)
            {
                if (args[0] == "cupboard")
                {
                    SendReply(player, "Invalid Syntax. /deauth cupboard PlayerName");
                    return;
                }
                else if (args[0] == "turret")
                {
                    SendReply(player, "Invalid Syntax. /deauth turret PlayerName");
                    return;
                }
                else
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[0]);
                }
            }
            else if (args.Length == 0)
            {
                SendReply(player, "Invalid Syntax. /deauth PlayerName\n/deauth turret/cupboard PlayerName");
                return;
            }
            else if (args.Length == 2)
            {
                if (args[0] == "cupboard")
                {
                    massCupboard = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else if (args[0] == "turret")
                {
                    massTurret = true;
                    target = FindPlayerByPartialName(args[1]);
                }
                else
                {
                    error = true;
                }
            }

            if (massTurret || massCupboard)
            {
                if (target == null || target.net == null || target.net.connection == null)
                {
                    SendReply(player, messages["Target player not found"]);
                    return;
                }
            }

            if (error)
            {
                SendReply(player, messages["Invalid Syntax. \n/auth turret player\n/auth cupboard player/auth player\n/auth"]);
                return;
            }

            if (massCupboard && target != null)
            {
                this.massCupboardDeauthorize(player, target);
            }

            if (massTurret && target != null)
            {
                this.massTurretDeauthorize(player, target);
            }
        }

        [ConsoleCommand("authclean")]
        void ccAuthClean(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null && arg.connection.authLevel < 1)
            {
                SendReply(arg, "No permission");
                return;
            }

            BasePlayer target = null;
            if (arg.Args.Length == 1)
            {
                target = FindPlayerByPartialName(arg.Args[0]);
                if (target == null)
                {
                    SendReply(arg, messages["Target player not found"]);
                    return;
                }
            }
            else
            {
                SendReply(arg, "Invalid Syntax. authclean PlayerName");
            }

            this.SetValue(target, "buildingPrivlidges", new List<BuildingPrivlidge>());
            target.SetPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege, false);
            target.SetPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege, false);
        }

        [ChatCommand("authclean")]
        void cmdAuthClean(BasePlayer player, string command, string[] args)
        {
            if (!this.canChangeOwners(player))
            {
                return;
            }

            BasePlayer target = null;
            if (args.Length == 1)
            {
                target = FindPlayerByPartialName(args[0]);
                if (target == null)
                {
                    SendReply(player, messages["Target player not found"]);
                    return;
                }
            }
            else
            {
                target = player;
            }

            this.SetValue(target, "buildingPrivlidges", new List<BuildingPrivlidge>());
            target.SetPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege, false);
            target.SetPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege, false);
        }

        [ChatCommand("prod2")]
        void cmdProd2(BasePlayer player, string command, string[] args)
        {
            if (!this.canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "all":
                        this.massProd<BaseEntity>(player);
                        break;
                    case "block":
                        this.massProd<BuildingBlock>(player);
                        break;
                    case "storage":
                        this.massProd<StorageContainer>(player);
                        break;
                    case "sign":
                        this.massProd<Signage>(player);
                        break;
                    case "sleepingbag":
                        this.massProd<SleepingBag>(player);
                        break;
                    case "plant":
                        this.massProd<PlantEntity>(player);
                        break;
                    case "oven":
                        this.massProd<BaseOven>(player);
                        break;
                    case "turret":
                        this.massProdTurret(player);
                        break;
                    case "door":
                        this.massProd<Door>(player);
                        break;
                    case "cupboard":
                        this.massProdCupboard(player);
                        break;
                }
            }
            else if (args.Length == 0)
            {
                this.massProd<BaseEntity>(player);
            }
            else
            {
                SendReply(player, messages["Invalid Syntax. \n/prod2 type \nTypes:\n all/block/entity/storage/cupboard/sign/sleepingbag/plant/oven/door/turret"]);
            }
        }

        #endregion

        #region Permission Checks

        bool canCheckOwners(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.UserIDString, "entityowner.cancheckowners");
        }

        bool canChangeOwners(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.UserIDString, "entityowner.canchangeowners");
        }

        #endregion

        #region Ownership Methods

        private void massChangeOwner<T>(BasePlayer player, BasePlayer target = null) where T : BaseEntity
        {
            object entityObject = false;

            if (typeof(T) == typeof(BuildingBlock))
            {
                entityObject = FindBuilding(player.transform.position, this.DistanceThreshold);
            }
            else
            {
                entityObject = FindEntity(player.transform.position, this.DistanceThreshold);
            }

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                if (target == null)
                {
                    SendReply(player, messages["Removing ownership.."]);
                }
                else
                {
                    SendReply(player, messages["Changing ownership.."]);
                }

                T entity = entityObject as T;
                List<T> entityList = new List<T>();
                List<Vector3> checkFrom = new List<Vector3>();
                entityList.Add((T)entity);
                checkFrom.Add(entity.transform.position);
                int c = 1;
                if (target == null)
                {
                    RemoveOwner(entity);
                }
                else
                {
                    ChangeOwner(entity, target);
                }
                var current = 0;
                int bbs = 0;
                int ebs = 0;
                if (entity is BuildingBlock)
                {
                    bbs++;
                }
                else
                {
                    ebs++;
                }
                while (true)
                {
                    current++;
                    if (current > this.EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Counted {0} entities ({1}/{2})"], c, bbs, ebs));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Counted {0} entities ({1}/{2})"], c, bbs, ebs));
                        break;
                    }

                    List<T> hits = this.FindEntities<T>(checkFrom[current - 1], this.DistanceThreshold);

                    foreach (T entityComponent in hits)
                    {
                        if (!entityList.Contains(entityComponent))
                        {
                            c++;
                            entityList.Add(entityComponent);
                            checkFrom.Add(entityComponent.transform.position);

                            if (entityComponent is BuildingBlock)
                            {
                                bbs++;
                            }
                            else
                            {
                                ebs++;
                            }

                            if (target == null)
                            {
                                RemoveOwner(entityComponent);
                            }
                            else
                            {
                                ChangeOwner(entityComponent, target);
                            }
                        }
                    }
                }

                if (target != null)
                {
                    SendReply(player, string.Format(messages["New owner of all around is: {0}"], target.displayName));
                    SendReply(target, messages["Owner: You were given ownership of this house and nearby deployables"]);
                }
                else
                {
                    SendReply(player, string.Format(messages["New owner of all around is: {0}"], "No one"));
                }
            }
        }

        private void massProd<T>(BasePlayer player) where T : BaseEntity
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);
            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                Dictionary<ulong, int> prodOwners = new Dictionary<ulong, int>();
                BaseEntity entity = entityObject as BaseEntity;
                if (entity.transform == null)
                {
                    SendReply(player, messages["No entities found."]);
                    return;
                }

                SendReply(player, messages["Prodding structure.."]);

                List<T> entityList = new List<T>();
                List<Vector3> checkFrom = new List<Vector3>();

                if (entity is T)
                {
                    entityList.Add((T)entity);
                }
                checkFrom.Add(entity.transform.position);

                int total = 0;
                if (entity is T)
                {
                    prodOwners.Add(entity.OwnerID, 1);
                    total++;
                }

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > this.EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, "Count (" + total + ")");
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, "Count (" + total + ")");
                        break;
                    }

                    float distanceThreshold = this.DistanceThreshold;
                    if(typeof(T) != typeof(BuildingBlock) && typeof(T) != typeof(BaseEntity)) {
                        distanceThreshold += 30;
                    }

                    List<T> hits = this.FindEntities<T>(checkFrom[current - 1], distanceThreshold);

                    foreach (T fentity in hits)
                    {
                        if (!(entityList.Contains(fentity)))
                        {
                            if (fentity.name.ToString() == "player/player")
                            {
                                continue;
                            }
                            if (fentity.transform == null)
                            {
                                continue;
                            }
                            total++;
                            entityList.Add(fentity);
                            checkFrom.Add(fentity.transform.position);
                            ulong pid = fentity.OwnerID;
                            if (prodOwners.ContainsKey(pid))
                            {
                                prodOwners[pid]++;
                            }
                            else
                            {
                                prodOwners.Add(pid, 1);
                            }
                        }
                    }
                }

                Dictionary<ulong, int> percs = new Dictionary<ulong, int>();
                int unknown = 100;
                if (total > 0)
                {
                    foreach (KeyValuePair<ulong, int> kvp in prodOwners)
                    {
                        int perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        string n = this.FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }
                }

                if (unknown > 0)
                {
                    SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));
                }
            }
        }

        private void ProdCupboard(BasePlayer player, BuildingPrivlidge cupboard)
        {
            List<string> authorizedUsers = this.GetToolCupboardUserNames(cupboard);

            StringBuilder sb = new StringBuilder();

            if (authorizedUsers.Count == 0)
            {
                sb.Append(string.Format(messages["({0}) Authorized"], 0));
            }
            else
            {
                sb.AppendLine(string.Format(messages["({0}) Authorized"], authorizedUsers.Count));
                foreach (string n in authorizedUsers)
                {
                    sb.AppendLine(n);
                }
            }

            SendReply(player, sb.ToString());
        }

        private void ProdTurret(BasePlayer player, AutoTurret turret)
        {
            List<string> authorizedUsers = this.GetTurretUserNames(turret);

            StringBuilder sb = new StringBuilder();

            if (authorizedUsers.Count == 0)
            {
                sb.Append(string.Format(messages["({0}) Authorized"], 0));
            }
            else
            {
                sb.AppendLine(string.Format(messages["({0}) Authorized"], authorizedUsers.Count));
                foreach (string n in authorizedUsers)
                {
                    sb.AppendLine(n);
                }
            }

            SendReply(player, sb.ToString());
        }

        private void massProdCupboard(BasePlayer player)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                Dictionary<ulong, int> prodOwners = new Dictionary<ulong, int>();
                SendReply(player, messages["Prodding cupboards.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BuildingPrivlidge> entities = this.FindEntities<BuildingPrivlidge>(checkFrom[current - 1], this.CupboardDistanceThreshold);

                    foreach (BuildingPrivlidge e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            foreach (ProtoBuf.PlayerNameID pnid in e.authorizedPlayers)
                            {
                                if (prodOwners.ContainsKey(pnid.userid))
                                {
                                    prodOwners[pnid.userid]++;
                                }
                                else
                                {
                                    prodOwners.Add(pnid.userid, 1);
                                }
                            }

                            total++;
                        }
                    }
                }

                Dictionary<ulong, int> percs = new Dictionary<ulong, int>();
                int unknown = 100;
                if (total > 0)
                {
                    foreach (KeyValuePair<ulong, int> kvp in prodOwners)
                    {
                        int perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        string n = this.FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                    {
                        SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));
                    }
                }
            }
        }

        private void massProdTurret(BasePlayer player)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                Dictionary<ulong, int> prodOwners = new Dictionary<ulong, int>();
                SendReply(player, messages["Prodding turrets.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BaseEntity> entities = this.FindEntities<BaseEntity>(checkFrom[current - 1], this.DistanceThreshold);

                    foreach (BaseEntity e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            if (e is AutoTurret)
                            {
                                AutoTurret turret = (AutoTurret)e;
                                foreach (ProtoBuf.PlayerNameID pnid in turret.authorizedPlayers)
                                {
                                    if (prodOwners.ContainsKey(pnid.userid))
                                    {
                                        prodOwners[pnid.userid]++;
                                    }
                                    else
                                    {
                                        prodOwners.Add(pnid.userid, 1);
                                    }
                                }

                                total++;
                            }
                        }
                    }
                }

                Dictionary<ulong, int> percs = new Dictionary<ulong, int>();
                int unknown = 100;
                if (total > 0)
                {
                    foreach (KeyValuePair<ulong, int> kvp in prodOwners)
                    {
                        int perc = kvp.Value * 100 / total;
                        percs.Add(kvp.Key, perc);
                        string n = this.FindPlayerName(kvp.Key);

                        if (n != messages["Unknown player"])
                        {
                            SendReply(player, n + ": " + perc + "%");
                            unknown -= perc;
                        }
                    }

                    if (unknown > 0)
                    {
                        SendReply(player, string.Format(messages["Unknown: {0}%"], unknown));
                    }
                }
            }
        }

        private void massCupboardAuthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                SendReply(player, messages["Authorizing cupboards.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BuildingPrivlidge> entities = this.FindEntities<BuildingPrivlidge>(checkFrom[current - 1], this.CupboardDistanceThreshold);

                    foreach (BuildingPrivlidge e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            if (e is BuildingPrivlidge)
                            {
                                BuildingPrivlidge priv = (BuildingPrivlidge)e;
                                if (!this.HasCupboardAccess(priv, target))
                                {
                                    priv.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                                    {
                                        userid = target.userID,
                                        username = target.displayName
                                    });

                                    priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                    target.SetInsideBuildingPrivilege(priv, true);

                                    total++;
                                }
                            }
                        }
                    }
                }

                SendReply(player, string.Format(messages["Authorized {0} on {1} cupboards"], target.displayName, total.ToString()));
            }
        }

        private void massCupboardDeauthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                SendReply(player, messages["Deauthorizing cupboards.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BuildingPrivlidge> entities = this.FindEntities<BuildingPrivlidge>(checkFrom[current - 1], this.CupboardDistanceThreshold);

                    foreach (BuildingPrivlidge e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            if (e is BuildingPrivlidge)
                            {
                                BuildingPrivlidge priv = (BuildingPrivlidge)e;
                                if (this.HasCupboardAccess(priv, target))
                                {
                                    foreach (ProtoBuf.PlayerNameID p in priv.authorizedPlayers.ToList())
                                    {
                                        if (p.userid == target.userID)
                                        {
                                            priv.authorizedPlayers.Remove(p);
                                            priv.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                            target.SetInsideBuildingPrivilege(priv, false);
                                        }
                                    }

                                    total++;
                                }
                            }
                        }
                    }
                }

                SendReply(player, string.Format(messages["Deauthorized {0} on {1} cupboards"], target.displayName, total.ToString()));
            }
        }

        private void massTurretAuthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                SendReply(player, messages["Authorizing turrets.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BaseEntity> entities = this.FindEntities<BaseEntity>(checkFrom[current - 1], this.DistanceThreshold);

                    foreach (BaseEntity e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            if (e is AutoTurret)
                            {
                                AutoTurret turret = (AutoTurret)e;
                                if (!this.HasTurretAccess(turret, target))
                                {
                                    turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                                    {
                                        userid = target.userID,
                                        username = target.displayName
                                    });

                                    turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                    turret.SetTarget(null);
                                    total++;
                                }
                            }
                        }
                    }
                }

                SendReply(player, string.Format(messages["Authorized {0} on {1} turrets"], target.displayName, total.ToString()));
            }
        }

        private void massTurretDeauthorize(BasePlayer player, BasePlayer target)
        {
            object entityObject = false;

            entityObject = FindEntity(player.transform.position, this.DistanceThreshold);

            if (entityObject is bool)
            {
                SendReply(player, messages["No entities found."]);
            }
            else
            {
                int total = 0;
                SendReply(player, messages["Deauthorizing turrets.."]);
                BaseEntity entity = entityObject as BaseEntity;
                List<BaseEntity> entityList = new List<BaseEntity>();
                List<Vector3> checkFrom = new List<Vector3>();

                checkFrom.Add(entity.transform.position);

                var current = 0;
                while (true)
                {
                    current++;
                    if (current > EntityLimit)
                    {
                        if (this.debug)
                        {
                            SendReply(player, messages["Exceeded entity limit."] + " " + EntityLimit.ToString());
                        }
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }
                    if (current > checkFrom.Count)
                    {
                        SendReply(player, string.Format(messages["Count ({0})"], total.ToString()));
                        break;
                    }

                    List<BaseEntity> entities = this.FindEntities<BaseEntity>(checkFrom[current - 1], this.DistanceThreshold);

                    foreach (BaseEntity e in entities)
                    {
                        if (!entityList.Contains(e))
                        {
                            entityList.Add(e);
                            checkFrom.Add(e.transform.position);

                            if (e is AutoTurret)
                            {
                                AutoTurret turret = (AutoTurret)e;
                                if (this.HasTurretAccess(turret, target))
                                {
                                    foreach (ProtoBuf.PlayerNameID p in turret.authorizedPlayers.ToList())
                                    {
                                        if (p.userid == target.userID)
                                        {
                                            turret.authorizedPlayers.Remove(p);
                                            turret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                                            turret.SetTarget(null);
                                            total++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                SendReply(player, string.Format(messages["Deauthorized {0} on {1} turrets"], target.displayName, total.ToString()));
            }
        }

        private List<string> GetToolCupboardUserNames(BuildingPrivlidge cupboard)
        {
            List<string> names = new List<string>();
            if (cupboard.authorizedPlayers.Count == 0)
            {
                return names;
            }

            foreach (ProtoBuf.PlayerNameID pnid in cupboard.authorizedPlayers)
            {
                names.Add(this.FindPlayerName(pnid.userid) + " - " + pnid.userid);
            }

            return names;
        }

        private List<string> GetTurretUserNames(AutoTurret turret)
        {
            List<string> names = new List<string>();
            if (turret.authorizedPlayers.Count == 0)
            {
                return names;
            }

            foreach (ProtoBuf.PlayerNameID pnid in turret.authorizedPlayers)
            {
                names.Add(this.FindPlayerName(pnid.userid) + " - " + pnid.userid);
            }

            return names;
        }

        private bool HasCupboardAccess(BuildingPrivlidge cupboard, BasePlayer player)
        {
            return cupboard.IsAuthed(player);
        }

        private bool HasTurretAccess(AutoTurret turret, BasePlayer player)
        {
            return turret.IsAuthed(player);
        }

        ulong GetOwnerID(BaseEntity entity)
        {
            return entity.OwnerID;
        }

        string GetOwnerName(BaseEntity entity)
        {
            return FindPlayerName(entity.OwnerID);
        }

        BasePlayer GetOwnerPlayer(BaseEntity entity)
        {
            return BasePlayer.FindByID(entity.OwnerID);
        }

        void RemoveOwner(BaseEntity entity)
        {
            entity.OwnerID = 0;
        }

        void ChangeOwner(BaseEntity entity, BasePlayer player)
        {
            entity.OwnerID = player.userID;
        }

        object FindEntityData(BaseEntity entity)
        {
            if (entity.OwnerID == 0)
            {
                return false;
            }

            return entity.OwnerID.ToString();
        }

        #endregion

        #region Utility Methods

        private object RaycastAll<T>(Vector3 Pos, Vector3 Aim) where T : BaseEntity
        {
            var hits = UnityEngine.Physics.RaycastAll(Pos, Aim);
            GamePhysics.Sort(hits);
            float distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                BaseEntity ent = hit.GetEntity();
                if (ent != null && ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            var hits = UnityEngine.Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            float distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                BaseEntity ent = hit.GetEntity();
                if (ent != null && ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        object FindBuilding(Vector3 position, float distance = 3f)
        {
            BuildingBlock hit = FindEntity<BuildingBlock>(position, distance);

            if (hit is BuildingBlock)
            {
                return hit;
            }

            return false;
        }

        object FindEntity(Vector3 position, float distance = 3f)
        {
            BaseEntity hit = FindEntity<BaseEntity>(position, distance);

            if (hit is BaseEntity)
            {
                return hit;
            }

            return false;
        }

        T FindEntity<T>(Vector3 position, float distance = 3f) where T : BaseEntity
        {
            List<T> list = new List<T>();
            Vis.Entities<T>(position, distance, list, layerMasks);

            if (list.Count > 0)
            {
                return list[0];
            }

            return null;
        }

        List<T> FindEntities<T>(Vector3 position, float distance = 3f) where T : BaseEntity
        {
            List<T> list = new List<T>();
            Vis.Entities<T>(position, distance, list, layerMasks);
            return list;
        }

        List<BuildingBlock> GetProfileConstructions(BasePlayer player)
        {
            List<BuildingBlock> result = new List<BuildingBlock>();
            BuildingBlock[] blocks = UnityEngine.Object.FindObjectsOfType<BuildingBlock>();
            foreach (BuildingBlock block in blocks) {
                if (block.OwnerID == player.userID)
                {
                    result.Add(block);
                }
            }

            return result;
        }

        List<BaseEntity> GetProfileDeployables(BasePlayer player)
        {
            List<BaseEntity> result = new List<BaseEntity>();
            BaseEntity[] entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach (BaseEntity entity in entities)
            {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock))
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        void ClearProfile(BasePlayer player)
        {
            BaseEntity[] entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach (BaseEntity entity in entities)
            {
                if (entity.OwnerID == player.userID && !(entity is BuildingBlock))
                {
                    RemoveOwner(entity);
                }
            }
        }

        private void SendChatMessage(BasePlayer player, string message)
        {
            player.ChatMessage(message);
        }

        private string FindPlayerName(ulong playerID)
        {
            BasePlayer player = FindPlayerByPartialName(playerID.ToString());
            if (player) 
            {
                if(player.IsSleeping()) {
                    return player.displayName + " [<color=lightblue>Sleeping</color>]";
                } else {
                    return player.displayName + " [<color=lime>Online</color>]";
                }
            }

            var p = covalence.Players.GetPlayer(playerID.ToString());
            if (p != null)
            {
                return p.Nickname + " [<color=red>Offline</color>]";
            }

            return "Unknown : "+playerID.ToString();
        }

        void SetValue(object inputObject, string propertyName, object propertyVal)
        {
            //find out the type
            Type type = inputObject.GetType();

            //get the property information based on the type
            System.Reflection.FieldInfo propertyInfo = type.GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            //find the property type
            Type propertyType = propertyInfo.FieldType;

            //Convert.ChangeType does not handle conversion to nullable types
            //if the property type is nullable, we need to get the underlying type of the property
            var targetType = IsNullableType(propertyType) ? Nullable.GetUnderlyingType(propertyType) : propertyType;

            //Returns an System.Object with the specified System.Type and whose value is
            //equivalent to the specified object.
            propertyVal = Convert.ChangeType(propertyVal, targetType);

            //Set the value of the property
            propertyInfo.SetValue(inputObject, propertyVal);
        }
        private bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
        }

        protected static BasePlayer FindPlayerByPartialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            BasePlayer player = null;
            name = name.ToLower();
            var allPlayers = BasePlayer.activePlayerList.ToArray();
            // Try to find an exact match first
            foreach (var p in allPlayers)
            {
                if (p == null)
                {
                    continue;
                }
                if (p.UserIDString == name)
                {
                    player = p;
                    break;
                }
                if (p.displayName == name)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            if (player != null)
                return player;
            // Otherwise try to find a partial match
            foreach (var p in allPlayers)
            {
                if (p == null)
                {
                    continue;
                }
                if (p.displayName.ToLower().IndexOf(name) >= 0)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            return player;
        }

        #endregion
    }
}
