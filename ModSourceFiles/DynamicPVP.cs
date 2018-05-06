using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("DynamicPVP", "CatMeat", "3.0.2", ResourceId = 2728)]
    [Description("Create temporary PVP zones around SupplyDrops, LockedCrates, APC and/or Heli")]

    public class DynamicPVP : RustPlugin
    {
        #region References
        [PluginReference]
        Plugin ZoneManager, TruePVE, ZoneDomes, BotSpawn;
        #endregion

        #region Declarations
        ConfigFileStructure Settings = new ConfigFileStructure();
        public static MonumentSettings BotSpawnProfileSettings = new MonumentSettings();
        public static string BotSpawnProfileName = "DynamicPVP";

        bool starting = true;
        bool validcommand;

        float compareRadius = 50;
        float zoneRadius;
        float zoneDuration;
        string botProfile;

        string msg;
        static string PluginVersion;
        string debugfilename = "debug";

        List<BaseEntity> activeSupplySignals = new List<BaseEntity>();
        Dictionary<string, Vector3> ActiveDynamicZones = new Dictionary<string, Vector3>();
        ConsoleSystem.Arg arguments;

        #endregion

        #region Plugin Initialization
        #region Check Allows
        private bool ZoneCreateAllowed()
        {
            Plugin ZoneManager = (Plugin)plugins.Find("ZoneManager");
            Plugin TruePVE = (Plugin)plugins.Find("TruePVE");

            if ((TruePVE != null) && (ZoneManager != null))
                if (Settings.Global.PluginEnabled)
                    return true;
            return false;
        }

        private bool BotSpawnAllowed()
        {
            Plugin BotSpawn = (Plugin)plugins.Find("BotSpawn");

            if (BotSpawn != null && Settings.Global.BotsEnabled) return true;
            return false;
        }

        private bool DomeCreateAllowed()
        {
            Plugin ZoneDomes = (Plugin)plugins.Find("ZoneDomes");

            if (ZoneDomes != null && Settings.Global.DomesEnabled) return true;
            return false;
        }
        #endregion

        void Init()
        {
            LoadConfigVariables();
        }

        void Unload()
        {
            List<string> keys = new List<string>(ActiveDynamicZones.Keys);

            if (keys.Count > 0) DebugPrint($"Deleting {keys.Count} ActiveZones", false);
            foreach (string key in keys) DeleteDynZone(key);
        }
        #endregion

        #region Commands
        [ChatCommand("dynpvp")]
        private void CmdChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player?.net?.connection != null && player.net.connection.authLevel > 0)
                if (args.Count() > 0) ProcessCommand(player, args);
        }

        [ConsoleCommand("dynpvp")]
        private void CmdConsoleCommand(ConsoleSystem.Arg arg)
        {
            arguments = arg; //save for responding later
            if (arg.Connection != null && arg.IsAdmin)
                if (arg.Args.Count() > 0) ProcessCommand(null, arg.Args);
        }

        private void ProcessCommand(BasePlayer player, string[] args)
        {
            var command = args[0];
            var value = "";

            if (args.Count() > 1) value = args[1];

            var commandToLower = command.Trim().ToLower();
            var valueToLower = value.Trim().ToLower();
            float numberValue;
            var number = Single.TryParse(value, out numberValue);

            validcommand = true;

            switch (commandToLower)
            {
                case "debug":
                    switch (valueToLower)
                    {
                        case "true":
                            Settings.Global.DebugEnabled = true;
                            break;
                        case "false":
                            Settings.Global.DebugEnabled = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand) SaveConfig(Settings);
                    break;
                default:
                    validcommand = false;
                    break;
            }
            if (validcommand)
                RespondWith(player, "DynamicPVP: " + command + " set to: " + value);
            else
                RespondWith(player, $"Syntax error! ({command}:{value})");
        }
        #endregion

        #region OxideHooks
        void OnServerInitialized()
        {
            starting = false;
            if (BotSpawnAllowed()) BotSpawnProfileCreate();
            DeleteOldZones();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!Settings.Global.PluginEnabled || starting || entity == null || entity.IsDestroyed) return;
            switch (entity.ShortPrefabName)
            {
                case "supply_drop":
                    if (IsProbablySupplySignal(entity.transform.position))
                    {
                        if (!Settings.Events.SupplySignal.Enabled) return;
                        CreateDynZone("Signal",
                            entity.transform.position,
                            Settings.Events.SupplySignal.Radius,
                            Settings.Events.SupplySignal.Duration,
                            Settings.Events.SupplySignal.BotProfile
                            );
                        break;
                    }
                    else
                    {
                        if (!Settings.Events.TimedDrop.Enabled) return;
                        CreateDynZone("AirDrop",
                            entity.transform.position,
                            Settings.Events.TimedDrop.Radius,
                            Settings.Events.TimedDrop.Duration,
                            Settings.Events.TimedDrop.BotProfile
                            );
                        break;
                    }
                case "codelockedhackablecrate":
                    if (!Settings.Events.TimedCrate.Enabled) return;
                    CreateDynZone("Crate",
                        entity.transform.position,
                        Settings.Events.TimedCrate.Radius,
                        Settings.Events.TimedCrate.Duration,
                        Settings.Events.TimedCrate.BotProfile
                        );
                    break;
                default:
                    return;
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!Settings.Global.PluginEnabled || starting || entity == null || entity.IsDestroyed) return;
            switch (entity.ShortPrefabName)
            {
                case "patrolhelicopter":
                    if (!Settings.Events.PatrolHelicopter.Enabled) return;
                    CreateDynZone("Heli",
                        entity.transform.position,
                        Settings.Events.PatrolHelicopter.Radius,
                        Settings.Events.PatrolHelicopter.Duration,
                        Settings.Events.PatrolHelicopter.BotProfile
                        );
                    break;
                case "bradleyapc":
                    if (!Settings.Events.BradleyAPC.Enabled) return;
                    CreateDynZone("APC",
                        entity.transform.position,
                        Settings.Events.BradleyAPC.Radius,
                        Settings.Events.BradleyAPC.Duration,
                        Settings.Events.BradleyAPC.BotProfile
                        );
                    break;
                default:
                    return;
            }
        }
        #endregion

        #region ZoneHandling
        void CreateDynZone(string EventID, Vector3 DynPosition, float _radius, float _duration, string _profile)
        {
            DynPosition.y = TerrainMeta.HeightMap.GetHeight(DynPosition);

            if (ZoneCreateAllowed())
            {
                string DynZoneID = DateTime.Now.ToString("HHmmssff");

                List<string> DynArgs = new List<string>();
                DynArgs.Add("name");
                DynArgs.Add("DynamicPVP");
                DynArgs.Add("radius");
                DynArgs.Add(_radius.ToString());
                DynArgs.Add("enter_message");
                DynArgs.Add("Entering a PVP area!");
                DynArgs.Add("leave_message");
                DynArgs.Add("Leaving a PVP area.");
                DynArgs.Add("undestr");
                DynArgs.Add("true");
                if (Settings.Global.BlockTeleport)
                {
                    DynArgs.Add("notp");
                    DynArgs.Add("true");
                }
                if (!String.IsNullOrEmpty(Settings.Global.ExtraZoneFlags))
                {
                    List<string> _xtraArgs = Settings.Global.ExtraZoneFlags.Split(' ').ToList();

                    foreach (var _arg in _xtraArgs)
                    {
                        DynArgs.Add(_arg);
                    }
                }

                string[] DynZoneArgs = DynArgs.ToArray();
                DebugPrint($"EventID {DynZoneID} {EventID}{DynPosition} {_radius.ToString()}m {_duration.ToString()}s", false);
                bool ZoneAdded = AddZone(DynZoneID, DynZoneArgs, DynPosition);

                if (ZoneAdded)
                {
                    string successString = "";
                    ActiveDynamicZones.Add(DynZoneID, DynPosition);
                    bool MappingAdded = AddMapping(DynZoneID);

                    if (!MappingAdded) DebugPrint("ERROR: PVP Mapping failed.", true);
                    else successString = successString + " Mapping,";
                    if (DomeCreateAllowed())
                    {
                        bool DomeAdded = AddDome(DynZoneID);

                        if (!DomeAdded) DebugPrint("ERROR: Dome NOT added for Zone: " + DynZoneID, true);
                        else successString = successString + " Dome,";
                    }
                    if (BotSpawnAllowed())
                    {
                        string[] result = SpawnBots(DynPosition, _profile, DynZoneID);

                        if (result[0] == "false")
                        {
                            DebugPrint($"ERROR: Bot spawn failed with profile `{_profile}` : {result[1]}", true);
                            result = SpawnBots(DynPosition, "DynamicPVP", DynZoneID);
                            if (result[0] == "false") DebugPrint($"ERROR: Bot spawn failed with default profile.", true);
                            else
                            {
                                DebugPrint($"Spawned bots using default profile.", true);
                                successString = successString + " Bots,";
                            }
                        }
                        else successString = successString + " Bots,";
                    }
                    timer.Once(_duration, () => { DeleteDynZone(DynZoneID); });
                    if (successString.EndsWith(",")) successString = successString.Substring(0, successString.Length - 1);
                    DebugPrint($"Created Zone {DynZoneID} ({successString.Trim()})", false);
                }
                else DebugPrint("ERROR: Zone creation failed.", true);
            }
        }

        bool DeleteDynZone(string DynZoneID)
        {
            if (ZoneCreateAllowed())
            {
                string successString = "";

                if (BotSpawnAllowed())
                {
                    string[] result = RemoveBots(DynZoneID);

                    if (result[0] == "false") DebugPrint($"ERROR: Bot delete failed: {result[1]}", true);
                    else successString = successString + " Bots,";
                }
                if (DomeCreateAllowed())
                {
                    bool DomeRemoved = RemoveDome(DynZoneID);

                    if (!DomeRemoved) DebugPrint("ERROR: Dome NOT removed for Zone: " + DynZoneID, true);
                    else successString = successString + " Dome,";
                }

                bool MappingRemoved = RemoveMapping(DynZoneID);

                if (!MappingRemoved) DebugPrint("ERROR: PVP NOT disabled for Zone: " + DynZoneID, true);
                else successString = successString + " Mapping,";

                bool ZoneRemoved = RemoveZone(DynZoneID);

                if (!ZoneRemoved) DebugPrint("ERROR: Zone removal failed.", true);
                else
                {
                    if (successString.EndsWith(",")) successString = successString.Substring(0, successString.Length - 1);
                    DebugPrint($"Deleted Zone {DynZoneID} ({successString.Trim()})", false);
                    ActiveDynamicZones.Remove(DynZoneID);
                    return true;
                }
            }
            return false;
        }

        void DeleteOldZones()
        {
            int _attempts = 0;
            int _sucesses = 0;
            string[] ZoneIDs = (string[])ZoneManager?.Call("GetZoneIDs");

            if (ZoneIDs != null)
            {
                for (int i = 0; i < ZoneIDs.Length; i++)
                {
                    string zoneName = (string)ZoneManager?.Call("GetZoneName", ZoneIDs[i]);

                    if (zoneName == "DynamicPVP")
                    {
                        _attempts++;

                        bool _success = DeleteDynZone(ZoneIDs[i]);

                        if (_success) _sucesses++;
                    }
                }
                DebugPrint($"Deleted {_sucesses} of {_attempts} existing DynamicPVP zones", true);
            }
        }
        #endregion

        #region ExternalHooks
        // TruePVE API
        bool AddMapping(string zoneID) => (bool)TruePVE?.Call("AddOrUpdateMapping", zoneID, "exclude");

        bool RemoveMapping(string zoneID) => (bool)TruePVE?.Call("RemoveMapping", zoneID);

        // ZoneDomes API
        bool AddDome(string zoneID) => (bool)ZoneDomes?.Call("AddNewDome", null, zoneID);

        bool RemoveDome(string zoneID) => (bool)ZoneDomes?.Call("RemoveExistingDome", null, zoneID);

        // ZoneManager API
        bool AddZone(string zoneID, string[] zoneArgs, Vector3 zoneLocation) => (bool)ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneArgs, zoneLocation);

        bool RemoveZone(string zoneID) => (bool)ZoneManager?.Call("EraseZone", zoneID);

        // BotSpawn API
        string[] SpawnBots(Vector3 zoneLocation, string zoneProfile, string zoneGroupID) => (String[])BotSpawn?.CallHook("AddGroupSpawn", zoneLocation, zoneProfile, zoneGroupID);

        string[] RemoveBots(string zoneGroupID) => (string[])BotSpawn?.CallHook("RemoveGroupSpawn", zoneGroupID);

        string[] CheckProfile(string profile) => (string[])BotSpawn?.CallHook("ProfileExists", profile);

        string[] AddProfile(MonumentSettings profile) => (string[])BotSpawn?.CallHook("CreateNewProfile", "DynamicPVP", profile);

        #endregion

        #region Messaging
        void DebugPrint(string msg, bool warning)
        {
            if (Settings.Global.DebugEnabled)
            {
                switch (warning)
                {
                    case true:
                        PrintWarning(msg);
                        break;
                    case false:
                        Puts(msg);
                        break;
                }
            }

            LogToFile(debugfilename, "[" + DateTime.Now.ToString() + "] | " + msg, this, true);
        }

        void RespondWith(BasePlayer player, string msg)
        {
            if (player == null)
                arguments.ReplyWith(msg);
            else
                SendReply(player, msg);
            return;
        }
        #endregion

        #region SupplySignals
        bool IsProbablySupplySignal(Vector3 landingposition)
        {
            bool probable = false;

            // potential issues with signals thrown near each other (<40m)
            // definite issues with modifications that create more than one supply drop per cargo plane.
            // potential issues with player moving while throwing signal.

            //DebugPrint($"Checking {activeSupplySignals.Count()} active supply signals", false);
            if (activeSupplySignals.Count() > 0)
            {
                foreach (BaseEntity supplysignal in activeSupplySignals.ToList())
                {
                    if (supplysignal == null)
                    {
                        activeSupplySignals.Remove(supplysignal);
                        continue;
                    }

                    Vector3 thrownposition = supplysignal.transform.position;
                    float xdiff = Math.Abs(thrownposition.x - landingposition.x);
                    float zdiff = Math.Abs(thrownposition.z - landingposition.z);

                    //DebugPrint($"Known SupplySignal at {thrownposition} differing by {xdiff}, {zdiff}", false);

                    if (xdiff < compareRadius && zdiff < compareRadius)
                    {
                        probable = true;
                        activeSupplySignals.Remove(supplysignal);
                        DebugPrint("Found matching SupplySignal.", false);
                        DebugPrint($"Active supply signals remaining: {activeSupplySignals.Count()}", false);

                        break;
                    }
                }
                if (!probable)
                    //DebugPrint($"No matches found, probably from a timed event cargo_plane", false);
                    return probable;
            }
            //DebugPrint($"No active signals, must be from a timed event cargo_plane", false);
            return false;
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal))
                return;
            if (entity.net == null)
                entity.net = Network.Net.sv.CreateNetworkable();

            Vector3 position = entity.transform.position;

            if (activeSupplySignals.Contains(entity))
                return;
            SupplyThrown(player, entity, position);
            return;
        }

        void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal)) return;
            if (activeSupplySignals.Contains(entity)) return;

            Vector3 position = entity.transform.position;
            SupplyThrown(player, entity, position);
            return;
        }

        void SupplyThrown(BasePlayer player, BaseEntity entity, Vector3 position)
        {
            Vector3 thrownposition = player.transform.position;

            timer.Once(2.0f, () =>
            {
                if (entity == null)
                {
                    activeSupplySignals.Remove(entity);
                    return;
                }
            });

            timer.Once(2.3f, () =>
            {
                if (entity == null) return;
                activeSupplySignals.Add(entity);
                //DebugPrint($"SupplySignal position of {position}", false);
            });
        }

        #endregion

        #region Classes
        private class ActiveZone
        {
            public string DynZoneID { get; set; }
        }

        private void BotSpawnProfileCreate()
        {
            string[] result = (string[])BotSpawn?.CallHook("ProfileExists", "DynamicPVP");

            if (result[0] == "false")
            {
                DebugPrint("BotsSpawn Does not contain custom profile `DynamicPVP`.", true);

                var _profile = JsonConvert.SerializeObject(new MonumentSettings());

                result = (string[])BotSpawn?.CallHook("CreateNewProfile", BotSpawnProfileName, _profile);
                if (result[0] == "false") DebugPrint($"BotsSpawn failed to add `DynamicPVP`.\n{result[1]}", true);
                else
                {
                    result = (string[])BotSpawn?.CallHook("ProfileExists", "DynamicPVP");

                    if (result[0] == "false") DebugPrint($"Added but failed show `DynamicPVP`.\n{result[1]}", true);
                    else DebugPrint("Succesfully added custom profile `DynamicPVP`.", true);
                }
            }
            else DebugPrint("Custom profile `DynamicPVP` already exists.", true);
        }

        public class MonumentSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 2;
            public int BotHealth = 100;
            public int Radius = 20;
            public List<string> Kit = new List<string>();
            public string BotName = "randomname";
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;
            public int Respawn_Timer = 0;
            public bool Disable_Radio = false;
            public float LocationX;
            public float LocationY;
            public float LocationZ;
            public int Roam_Range = 20;
            public bool Peace_Keeper = true;
            public bool Weapon_Drop = true;
            public bool Keep_Default_Loadout = false;
            public bool Wipe_Belt = false;
            public bool Wipe_Clothing = false;
            public bool Allow_Rust_Loot = true;
            public int Suicide_Timer = 1200;
        }

        #endregion

        #region NewConfig

        private void LoadConfigVariables()
        {
            Settings = Config.ReadObject<ConfigFileStructure>();
            SaveConfig(Settings);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");

            var config = new ConfigFileStructure();
            SaveConfig(config);
        }

        void SaveConfig(ConfigFileStructure config)
        {
            Config.WriteObject(config, true);
        }

        public class ConfigFileStructure
        {
            public GlobalOptions Global = new GlobalOptions() { };
            public SpecificEventOptions Events = new SpecificEventOptions() { };
        }

        public class GlobalOptions
        {
            public string ConfigVersion = "Version 3.0.2";
            public bool PluginEnabled = true;
            public bool DebugEnabled = false;

            public string ExtraZoneFlags = "";
            public string MsgEnter = "Entering a PVP area!";
            public string MsgLeave = "Leaving a PVP area.";

            public bool BlockTeleport = true;
            public bool BotsEnabled = true;
            public bool DomesEnabled = true;
        }

        public class SpecificEventOptions
        {
            public StandardEventOptions BradleyAPC = new StandardEventOptions() { };
            public StandardEventOptions PatrolHelicopter = new StandardEventOptions() { };
            public StandardEventOptions SupplySignal = new StandardEventOptions() { Enabled = false };
            public StandardEventOptions TimedCrate = new StandardEventOptions() { Duration = 1200 };
            public StandardEventOptions TimedDrop = new StandardEventOptions() { };
        }

        public class StandardEventOptions
        {
            public string BotProfile = "DynamicPVP";
            public float Duration = 600;
            public bool Enabled = true;
            public float Radius = 100;
        }

        #endregion
    }
}