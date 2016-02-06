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
    [Info("Entity Owner", "Calytic @ cyclone.network", "2.0.2", ResourceId = 1255)]
    [Description("Tracks ownership of placed constructions and deployables")]
    class EntityOwner : RustPlugin
    {
        #region Data & Config
        private Dictionary<string, string> messages = new Dictionary<string, string>();
        public List<ulong> KnownPlayers = new List<ulong>();
        public Dictionary<ulong, OwnerProfile> Players = new Dictionary<ulong, OwnerProfile>();
        protected Dictionary<string, ulong> OwnerData = new Dictionary<string, ulong>();
        private int layerMasks = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");

        private FieldInfo serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

        private int EntityLimit = 8000;
        private float DistanceThreshold = 3f;
        private float CupboardDistanceThreshold = 20f;

        private bool useSubdirectory = false;
        private string subDirectory = "owners";
        private bool debug = false;
        private int SaveVersion2;
        public bool AutomaticWipeOwners;

        [PluginReference]
        Plugin DeadPlayersList;

        #endregion

        #region Profile
        public class OwnerProfile
        {
            public ulong playerID = 0;

            public List<string> Constructions = new List<string>();
            public List<string> Deployables = new List<string>();

            [JsonIgnore]
            public bool dirty = true;

            [JsonIgnore]
            public int Count
            {
                get
                {
                    return Constructions.Count + Deployables.Count;
                }
                private set { }
            }

            [JsonConstructor]
            public OwnerProfile(ulong playerID = 0, List<string> Constructions = null, List<string> Deployables = null)
            {
                if (playerID != 0)
                {
                    this.playerID = playerID;
                }

                if (Constructions is List<string>)
                {
                    this.Constructions = Constructions;
                }
                if (Deployables is List<string>)
                {
                    this.Deployables = Deployables;
                }
            }

            public OwnerProfile(BasePlayer player, List<string> Constructions = null, List<string> Deployables = null)
            {
                this.Player = player;

                if (Constructions != null)
                {
                    this.Constructions = Constructions;
                }

                if (Deployables != null)
                {
                    this.Deployables = Deployables;
                }

                this.dirty = true;
            }

            [JsonIgnore]
            public BasePlayer Player
            {
                get
                {
                    return FindPlayerByPartialName(this.playerID.ToString());
                }
                protected set
                {
                    this.playerID = value.userID;
                }
            }

            [JsonIgnore]
            public ulong PlayerID
            {
                get { return this.playerID; }
                private set { }
            }

            public string Add(BaseEntity entity, ulong playerID = 0)
            {
                if (entity.transform == null)
                {
                    return null;
                }

                if (entity.name.ToString() == "player/player")
                {
                    return null;
                }

                //if (entity.LookupPrefabName().Contains("cupboard.tool.deployed"))
                //{
                //    return null;
                //}

                string eid = GetEntityID(entity);

                if (entity is BuildingBlock)
                {
                    if (!Constructions.Contains(eid))
                    {
                        this.Constructions.Add(eid);
                    }
                }
                else
                {
                    if (!Deployables.Contains(eid))
                    {
                        this.Deployables.Add(eid);
                    }

                }

                this.dirty = true;

                return eid;
            }

            public string Remove(BaseEntity entity)
            {
                if (entity.transform == null)
                {
                    return null;
                }
                string eid = GetEntityID(entity);

                if (this.Constructions.Contains(eid))
                {
                    this.dirty = true;
                    this.Constructions.Remove(eid);
                }
                else if (this.Deployables.Contains(eid))
                {
                    this.dirty = true;
                    this.Deployables.Remove(eid);
                }

                return eid;
            }
        }
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
            "Prodding turrets.."
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
            Config["useSubdirectory"] = false;
            Config["subDirectory"] = "owners";
            Config["VERSION"] = this.Version.ToString();
            Config["EntityLimit"] = 8000;
            Config["DistanceThreshold"] = 3.0f;
            Config["CupboardDistanceThreshold"] = 20f;
            Config["SaveVersion2"] = Protocol.save;
            Config["AutomaticWipeOwners"] = true;
            Config["Debug"] = false;

            this.PopulatePlayerList();

            this.SavePlayerList();
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
            this.SaveConfig();
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

        void OnServerSave()
        {
            this.SaveData();
        }

        void OnServerShutdown()
        {
            this.SaveData();
        }

        //void Unload()
        //{
        //    this.SaveData();
        //}

        void OnServerInitialized()
        {
            try
            {
                LoadConfig();


                this.debug = GetConfig<bool>("Debug", false);
                this.EntityLimit = GetConfig<int>("EntityLimit", 8000);
                this.DistanceThreshold = GetConfig<float>("DistanceThreshold", 3f);
                this.CupboardDistanceThreshold = GetConfig<float>("CupboardDistanceThreshold", 20f);
                this.SaveVersion2 = GetConfig<int>("SaveVersion2", Protocol.save);
                this.AutomaticWipeOwners = GetConfig<bool>("AutomaticWipeOwners", true);

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

                this.useSubdirectory = GetConfig<bool>("useSubdirectory", false);
                this.subDirectory = GetConfig<string>("subDirectory", "owners");

                if (!permission.PermissionExists("entityowner.canwipeowners")) permission.RegisterPermission("entityowner.canwipeowners", this);
                if (!permission.PermissionExists("entityowner.cancheckowners")) permission.RegisterPermission("entityowner.cancheckowners", this);
                if (!permission.PermissionExists("entityowner.canchangeowners")) permission.RegisterPermission("entityowner.canchangeowners", this);

                LoadData();

                CheckVersion();
            }
            catch (Exception ex)
            {
                PrintError("OnServerInitialized failed: " + ex.Message);
            }
        }

        private void BuildServerTags(IList<string> tags)
        {
            tags.Add("ownership");
        }

        void CheckVersion()
        {
            if (this.SaveVersion2 != (int)Protocol.save)
            {
                if (this.AutomaticWipeOwners)
                {
                    PrintWarning("Running automatic wipe of ownership ("+SaveVersion2+" to "+Protocol.save+")");
                    Config["SaveVersion2"] = (int)Protocol.save;
                    Config.Save();
                    WipeOwners();
                }
                else
                {
                    PrintWarning("It is recommended to run 'owners.wipe' after a map wipe");
                }
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

            int t = 0;
            try
            {
                KnownPlayers = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("owners_list");
            }
            catch (Exception e)
            {
                if (debug)
                {
                    PrintError("KnownPlayer initialization failed: " + e.Message);
                    throw e;
                }
                else
                {
                    KnownPlayers = new List<ulong>();
                }
            }
            finally
            {
                this.PopulatePlayerList();
            }

            foreach (ulong playerID in KnownPlayers.ToList())
            {
                if (PlayerExists(playerID))
                {
                    this.LoadProfile(playerID);
                    t++;
                }
            }

            if (t > 0)
            {
                PrintToConsole("Loaded " + t.ToString() + " profiles");
            }
        }

        int SaveData()
        {
            this.SavePlayerList();
            int t = 0;

            foreach (KeyValuePair<ulong, OwnerProfile> kvp in Players.ToList())
            {
                if (kvp.Value.dirty)
                {
                    this.SaveProfile(kvp.Key, kvp.Value);
                    t++;
                }
            }

            return t;
        }

        private void PopulatePlayerList()
        {
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (!KnownPlayers.Contains(activePlayer.userID))
                {
                    if (activePlayer.userID != 0)
                    {
                        KnownPlayers.Add(activePlayer.userID);
                    }
                }
            }
        }

        private void SavePlayerList()
        {
            if (KnownPlayers.Count > 0)
            {
                Interface.Oxide.DataFileSystem.WriteObject<List<ulong>>("owners_list", KnownPlayers);
            }
        }

        protected bool LoadProfile(ulong playerID)
        {
            if (playerID == 0)
            {
                return false;
            }

            if (Players.ContainsKey(playerID))
            {
                return true;
            }

            string path = "entityowner_" + playerID.ToString();
            if (this.useSubdirectory)
            {
                path = this.subDirectory + "/" + path;
            }

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
            {
                if (debug)
                {
                    PrintWarning("No data file: creating "+path);
                }
                return false;
            }

            OwnerProfile profile = null;
            try
            {
                //profile = ReadOwnerProfile(path, playerID);
                Players[playerID] = profile = Interface.Oxide.DataFileSystem.ReadObject<OwnerProfile>(path);
                profile.playerID = playerID;
            }
            catch (Exception exception)
            {
                if (debug)
                {
                    PrintError("Profile loading failed: " + playerID + " " + exception.Message);
                }

                return false;
            }

            if (!(profile is OwnerProfile))
            {
                if (debug)
                {
                    PrintWarning("Something weird " + path);
                }
                return false;
            }

            if (profile.Count == 0)
            {
                return true;
            }

            foreach (string eid in profile.Deployables)
            {
                if (!OwnerData.ContainsKey(eid))
                {
                    OwnerData.Add(eid, playerID);
                }
                else
                {
                    OwnerData[eid] = playerID;
                }
            }

            foreach (string eid in profile.Constructions)
            {
                if (!OwnerData.ContainsKey(eid))
                {
                    OwnerData.Add(eid, playerID);
                }
                else
                {
                    OwnerData[eid] = playerID;
                }
            }

            return true;
        }

        OwnerProfile ReadOwnerProfile(string path, ulong playerID)
        {
            var data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            if (data["profile"] != null)
            {
                OwnerProfile profile = this.CreateDefaultProfile(playerID);
                Dictionary<string, object> profileData = data["profile"] as Dictionary<string, object>;
                List<string> constructions = new List<string>();
                foreach (var construction in profileData["Constructions"] as List<object>)
                {
                    profile.Constructions.Add((string)construction);
                }

                foreach (var deployable in profileData["Deployables"] as List<object>)
                {
                    profile.Deployables.Add((string)deployable);
                }

                return profile;
            }

            return null;
        }

        void SaveProfile(ulong playerID, OwnerProfile profile)
        {
            string path = "entityowner_" + playerID.ToString();
            if (this.useSubdirectory)
            {
                path = this.subDirectory + "/" + path;
            }

            //WriteOwnerProfile(path, profile);

            Interface.Oxide.DataFileSystem.WriteObject<OwnerProfile>(path, profile, false);

            profile.dirty = false;
        }

        void WriteOwnerProfile(string path, OwnerProfile profile)
        {
            DynamicConfigFile data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            Dictionary<string, object> profileData = new Dictionary<string, object>();

            profileData.Add("Constructions", profile.Constructions);
            profileData.Add("Deployables", profile.Deployables);

            data["profile"] = profileData;

            Interface.Oxide.DataFileSystem.SaveDatafile(path);
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            var sb = new StringBuilder();
            if (this.canCheckOwners(player) || this.canChangeOwners(player) || this.canWipeOwners(player))
            {
                sb.Append("<size=18>EntityOwner</size> by <color=#ce422b>Calytic</color> at <color=#ce422b>http://cyclone.network</color>\n");
            }

            if (this.canCheckOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/owner</color> - Check ownership of entity you are looking at").Append("\n");
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
                //sb.Append("  ").Append("<color=\"#ffd479\">/authclean PlayerName</color> - Remove all building privileges on a player").Append("\n");
            }

            if (this.canWipeOwners(player))
            {
                sb.Append("  ").Append("<color=\"#ffd479\">/wipeowners</color> - Wipes all ownership data").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/expireowners</color> - Expires all registered players without wiping ownership - helps performance").Append("\n");
                sb.Append("  ").Append("<color=\"#ffd479\">/saveowners</color> - Saves all changes to all loaded profiles").Append("\n");
            }

            player.ChatMessage(sb.ToString());
        }

        #endregion

        #region Game Hooks

        [HookMethod("OnItemDeployed")]
        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (deployer == null)
            {
                return;
            }
            if (entity == null)
            {
                return;
            }
            BasePlayer player = deployer.ownerPlayer;
            if (!(player is BasePlayer))
            {
                return;
            }

            OwnerProfile profile = this.GetOwnerProfile(player);

            if (profile is OwnerProfile)
            {
                AddEntityToProfile(profile, entity);
            }
        }

        [HookMethod("OnEntityBuilt")]
        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BaseEntity entity = gameObject.ToBaseEntity(); ;
            if (!(entity is BaseEntity))
            {
                return;
            }

            BasePlayer player = planner.ownerPlayer;
            if (player == null)
            {
                return;
            }

            OwnerProfile profile = this.GetOwnerProfile(player);

            if (profile is OwnerProfile)
            {
                if (entity is BuildingPrivlidge)
                {
                    timer.In(0.15f, delegate()
                    {
                        AddEntityToProfile(profile, entity);
                    });
                }
                else
                {
                    AddEntityToProfile(profile, entity);
                }
            }
        }

        [HookMethod("OnEntityDeath")]
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity.transform == null)
            {
                return;
            }

            string eid = GetEntityID(entity);
            if (OwnerData.ContainsKey(eid))
            {
                ulong playerID = OwnerData[eid];
                OwnerData.Remove(eid);

                if (this.LoadProfile(playerID))
                {
                    OwnerProfile profile = this.GetOwnerProfileByID(playerID);
                    RemoveEntityFromProfile(profile, entity);
                }
            }
        }

        [HookMethod("OnEntityGroundMissing")]
        private void OnEntityGroundMissing(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return;
            }
            string eid = GetEntityID(entity);
            if (OwnerData.ContainsKey(eid))
            {
                ulong playerID = OwnerData[eid];
                OwnerData.Remove(eid);

                if (this.LoadProfile(playerID))
                {
                    OwnerProfile profile = this.GetOwnerProfileByID(playerID);
                    RemoveEntityFromProfile(profile, entity);
                }
            }
        }

        [HookMethod("OnPlayerInit")]
        private void OnPlayerInit(BasePlayer player)
        {
            if (player.userID != 0)
            {
                this.GetOwnerProfile(player);
            }
        }

        //[HookMethod("OnPlayerDisconnected")]
        //void OnPlayerDisconnected(BasePlayer player)
        //{
        //    OwnerProfile profile = this.GetOwnerProfile(player);
        //    if (profile.Count > 0)
        //    {
        //        this.SaveProfile(player.userID, profile, true);
        //        this.SavePlayerList();
        //    }
        //}

        #endregion

        #region API

        object FindEntityData(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return false;
            }

            string eid = GetEntityID(entity);

            if (OwnerData.ContainsKey(eid))
                return OwnerData[eid].ToString();

            return false;
        }

        Dictionary<ulong, OwnerProfile> GetOwners()
        {
            return Players;
        }

        Dictionary<string, ulong> GetOwnersData()
        {
            return OwnerData;
        }

        void AddEntityOwner(BaseEntity entity, BasePlayer player)
        {
            OwnerProfile profile = this.GetOwnerProfile(player);
            AddEntityToProfile(profile, entity);
        }

        void RemoveEntityOwner(BaseEntity entity, BasePlayer player)
        {
            OwnerProfile profile = this.GetOwnerProfile(player);
            RemoveEntityFromProfile(profile, entity);
        }

        #endregion

        #region Chat Commands

        [ChatCommand("saveowners")]
        void cmdOwnerSave(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection != null)
            {
                if (player.net.connection.authLevel < 1)
                {
                    SendReply(player, messages["You are not allowed to use this command"]);
                    return;
                }
            }

            int t = this.SaveData();

            if (t > 0)
            {
                SendReply(player, "EntityOwner: Changes (" + t + ") saved.");
            }
            else
            {
                SendReply(player, "EntityOwner: No changes (" + t + ").");
            }
        }

        [ChatCommand("wipeowners")]
        void cmdOwnerWipe(BasePlayer player, string command, string[] args)
        {
            if (!this.canWipeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            this.WipeOwners();
            SendReply(player, messages["Ownership data wiped!"]);
        }

        [ChatCommand("expireowners")]
        void cmdOwnerExpire(BasePlayer player, string command, string[] args)
        {
            if (!this.canWipeOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }

            ExpireOwners();

            SendReply(player, messages["Ownership data expired!"]);
        }

        [ConsoleCommand("owners.save")]
        void ccOwnerSave(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, messages["You are not allowed to use this command"]);
                    return;
                }
            }

            int t = this.SaveData();

            if (t > 0)
            {
                SendReply(arg, "Changes (" + t + ") saved.");
            }
            else
            {
                SendReply(arg, "No changes (" + t + ").");
            }
        }

        [ConsoleCommand("owners.wipe")]
        void ccOwnerWipe(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, messages["You are not allowed to use this command"]);
                    return;
                }
            }

            WipeOwners();
            SendReply(arg, messages["Ownership data wiped!"]);
        }

        [ConsoleCommand("owners.expire")]
        void ccOwnerExpire(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, messages["You are not allowed to use this command"]);
                    return;
                }
            }

            ExpireOwners();

            SendReply(arg, messages["Ownership data expired!"]);
        }

        [ChatCommand("owner")]
        void cmdOwner(BasePlayer player, string command, string[] args)
        {
            if (!this.canCheckOwners(player))
            {
                SendReply(player, messages["You are not allowed to use this command"]);
                return;
            }
            if (args == null || (args != null && args.Length == 0))
            {
                var input = serverinput.GetValue(player) as InputState;
                var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                var target = RaycastAll<BaseEntity>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
                if (target is bool)
                {
                    SendReply(player, messages["No target found"]);
                    return;
                }
                if (target is BaseEntity)
                {
                    BaseEntity targetEntity = target as BaseEntity;
                    string owner = this.GetOwnerName((BaseEntity)target);
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
                var input = serverinput.GetValue(player) as InputState;
                var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                var priv = RaycastAll<BuildingPrivlidge>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
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
                var input = serverinput.GetValue(player) as InputState;
                var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                var turret = RaycastAll<AutoTurret>(player.transform.position + new Vector3(0f, 1.5f, 0f), currentRot);
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

        bool canWipeOwners(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.UserIDString, "entityowner.canwipeowners");
        }

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

                    OwnerProfile profile = this.GetOwnerProfile(target);
                    if (profile is OwnerProfile)
                    {
                        this.SaveProfile(target.userID, profile);
                        this.SavePlayerList();
                    }
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
                T entity = entityObject as T;
                if (entity.transform == null)
                {
                    SendReply(player, messages["No entities found."]);
                }

                SendReply(player, messages["Prodding structure.."]);

                List<T> entityList = new List<T>();
                List<Vector3> checkFrom = new List<Vector3>();

                entityList.Add((T)entity);
                checkFrom.Add(entity.transform.position);

                string eid = GetEntityID(entity);

                int total = 0;
                if (OwnerData.ContainsKey(eid))
                {
                    prodOwners.Add(OwnerData[eid], 1);
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

                    List<T> hits = this.FindEntities<T>(checkFrom[current - 1], this.DistanceThreshold);

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
                            eid = EntityOwner.GetEntityID(fentity);
                            if (OwnerData.ContainsKey(eid))
                            {
                                ulong pid = OwnerData[eid];
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
                                    player.SetInsideBuildingPrivilege(priv, true);

                                    total++;
                                }
                            }
                        }
                    }
                }

                SendReply(player, string.Format(messages["Authorized {0} on {1} cupboards"], target.displayName, total.ToString()));
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
            foreach (ProtoBuf.PlayerNameID pnid in cupboard.authorizedPlayers)
            {
                if (pnid.userid == player.userID)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasTurretAccess(AutoTurret turret, BasePlayer player)
        {
            foreach (ProtoBuf.PlayerNameID pnid in turret.authorizedPlayers)
            {
                if (pnid.userid == player.userID)
                {
                    return true;
                }
            }

            return false;
        }

        ulong GetOwnerID(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return 0;
            }

            string eid = GetEntityID(entity);

            if (OwnerData.ContainsKey(eid))
            {
                return OwnerData[eid];
            }

            return 0;
        }

        string GetOwnerName(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return null;
            }

            string eid = GetEntityID(entity);

            if (OwnerData.ContainsKey(eid))
            {
                ulong playerID = OwnerData[eid];
                return this.FindPlayerName(playerID);
            }

            return null;
        }

        BasePlayer GetOwnerPlayer(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return null;
            }

            string eid = GetEntityID(entity);

            if (OwnerData.ContainsKey(eid))
            {
                ulong playerID = OwnerData[eid];
                OwnerProfile profile = this.GetOwnerProfileByID(playerID);
                if (profile is OwnerProfile)
                {
                    return profile.Player;
                }
            }

            return null;
        }

        void RemoveOwner(BaseEntity entity, BasePlayer player = null)
        {
            if (player == null)
            {
                this.ClearOwner(entity);
            }
            else
            {
                OwnerProfile profile = this.GetOwnerProfile(player);
                if (profile is OwnerProfile)
                {
                    RemoveEntityFromProfile(profile, entity);
                }
            }
        }

        void ClearOwner(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return;
            }
            string eid = GetEntityID(entity);

            if (OwnerData.ContainsKey(eid))
            {
                ulong oldOwner = OwnerData[eid];
                OwnerProfile oldProfile = this.GetOwnerProfileByID(oldOwner);
                if (oldProfile is OwnerProfile)
                {
                    RemoveEntityFromProfile(oldProfile, entity);
                }
            }
        }

        void ChangeOwner(BaseEntity entity, BasePlayer player)
        {
            if (entity.transform == null)
            {
                return;
            }
            string eid = GetEntityID(entity);

            OwnerProfile profile = this.GetOwnerProfile(player);

            if (!(profile is OwnerProfile))
            {
                return;
            }

            if (OwnerData.ContainsKey(eid))
            {
                ulong oldOwner = OwnerData[eid];
                if (oldOwner != player.userID)
                {
                    OwnerProfile oldProfile = this.GetOwnerProfileByID(oldOwner);
                    if (oldProfile is OwnerProfile)
                    {
                        RemoveEntityFromProfile(oldProfile, entity);
                    }
                }
                else
                {
                    return;
                }
            }

            AddEntityToProfile(profile, entity);
        }

        void AddEntityToProfile(OwnerProfile profile, BaseEntity entity)
        {
            string eid = profile.Add(entity);

            if (eid != null)
            {
                if (OwnerData.ContainsKey(eid))
                {
                    OwnerData[eid] = profile.PlayerID;
                }
                else
                {
                    OwnerData.Add(eid, profile.PlayerID);
                }
            }
        }

        void RemoveEntityFromProfile(OwnerProfile profile, BaseEntity entity)
        {
            string eid = profile.Remove(entity);

            if (OwnerData.ContainsKey(eid))
            {
                OwnerData.Remove(eid);
            }
        }

        List<string> GetProfileConstructions(BasePlayer player)
        {
            OwnerProfile profile = GetOwnerProfile(player);

            if (profile is OwnerProfile)
            {
                return profile.Constructions;
            }

            return null;
        }

        List<string> GetProfileDeployables(BasePlayer player)
        {
            OwnerProfile profile = GetOwnerProfile(player);

            if (profile is OwnerProfile)
            {
                return profile.Deployables;
            }

            return null;
        }

        void ClearProfile(BasePlayer player)
        {
            OwnerProfile profile = GetOwnerProfile(player);

            if (profile is OwnerProfile)
            {
                profile.Constructions.Clear();
                profile.Deployables.Clear();
            }
        }

        string FindID(BaseEntity entity)
        {
            if (entity.transform == null)
            {
                return "0";
            }
            return EntityOwner.GetEntityID(entity);
        }

        private OwnerProfile CreateDefaultProfile(ulong playerID)
        {
            OwnerProfile profile = new OwnerProfile(playerID); ;

            if (!KnownPlayers.Contains(playerID))
            {
                KnownPlayers.Add(playerID);
            }


            if (Players.ContainsKey(playerID))
            {
                Players[playerID] = profile;
            }
            else
            {
                Players.Add(playerID, profile);
            }

            return profile;
        }

        public OwnerProfile GetOwnerProfile(BasePlayer player)
        {
            if (player.userID == 0)
            {
                return null;
            }
            OwnerProfile profile = null;
            if (!Players.ContainsKey(player.userID))
            {
                if (!this.LoadProfile(player.userID))
                {
                    profile = this.CreateDefaultProfile(player.userID);
                }
            }
            else
            {
                profile = Players[player.userID];
            }

            return profile;
        }

        public OwnerProfile GetOwnerProfileByID(ulong playerID)
        {
            OwnerProfile profile = null;
            if (!Players.ContainsKey(playerID))
            {
                if (KnownPlayers.Contains(playerID))
                {
                    this.LoadProfile(playerID);
                }
            }

            if (Players.ContainsKey(playerID))
            {
                profile = Players[playerID];
            }

            return profile;
        }

        void WipeOwners()
        {
            Players = new Dictionary<ulong, OwnerProfile>();
            OwnerData = new Dictionary<string, ulong>();
            KnownPlayers.Clear();

            PrintToConsole("Ownership wipe completed");
            SaveData();
        }

        void ExpireOwners()
        {
            KnownPlayers.Clear();

            this.PopulatePlayerList();

            SaveData();
        }

        #endregion

        #region Utility Methods

        public static string GetEntityID(BaseEntity entity)
        {
            Vector3 position = entity.transform.position;
            if (entity is BuildingBlock && entity.prefabID == 2051113843)
            {
                position.y += 0.01f;
            }
            return "x" + position.x + "y" + position.y + "z" + position.z;
        }

        private object RaycastAll<T>(Vector3 Pos, Vector3 Aim) where T : BaseEntity
        {
            var hits = UnityEngine.Physics.RaycastAll(Pos, Aim);
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
            List<T> list = Pool.GetList<T>();
            Vis.Entities<T>(position, distance, list, layerMasks);

            if (list.Count > 0)
            {
                return list[0];
            }

            return null;
        }

        List<T> FindEntities<T>(Vector3 position, float distance = 3f) where T : BaseEntity
        {
            List<T> list = Pool.GetList<T>();
            Vis.Entities<T>(position, distance, list, layerMasks);
            return list;
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
                return player.displayName + " [<color=lime>Online</color>]";
            }
                

            player = FindPlayerByPartialName(playerID.ToString());
            if (player)
            {
                return player.displayName + " [<color=lightblue>Sleeping</color>]";
            }

            var p = covalence.Players.GetPlayer(playerID.ToString());
            if (p != null)
            {
                return p.Nickname;
            }

            string name = DeadPlayersList?.Call("GetPlayerName", playerID) as string;
            if (name != null)
            {
                return name + " [<color=red>Dead</color>]";
            }

            return "Unknown : "+playerID.ToString();
        }

        private bool PlayerExists(ulong playerID)
        {
            BasePlayer player = FindPlayerByPartialName(playerID.ToString());
            if (player) 
            {
                return true;
            }
                

            player = FindPlayerByPartialName(playerID.ToString());
            if (player)
            {
                return true;
            }

            var p = covalence.Players.GetPlayer(playerID.ToString());
            if (p != null)
            {
                return true;
            }

            string name = DeadPlayersList?.Call("GetPlayerName", playerID) as string;
            if (name != null)
            {
                return true;
            }

            return false;
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
