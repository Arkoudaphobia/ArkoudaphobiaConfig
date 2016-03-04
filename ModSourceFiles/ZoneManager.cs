// Reference: RustBuild

using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using UnityEngine;

using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

using Rust;

namespace Oxide.Plugins
{
    [Info("ZoneManager", "Reneb / Nogrod", "2.4.1", ResourceId = 739)]
    public class ZoneManager : RustPlugin
    {
        private const string PermZone = "zonemanager.zone";
        private const string PermCanDeploy = "zonemanager.candeploy";
        private const string PermCanBuild = "zonemanager.canbuild";

        [PluginReference]
        Plugin PopupNotifications;

        ////////////////////////////////////////////
        /// Configs
        ////////////////////////////////////////////
        private bool usePopups = false;
        private bool Changed;
        private bool Initialized;
        private float AutolightOnTime;
        private float AutolightOffTime;
        private string prefix;

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private static bool GetBoolValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim().ToLower();
            switch (value)
            {
                case "t":
                case "true":
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;
                default:
                    return false;
            }
        }
        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }
        private void LoadVariables()
        {
            AutolightOnTime = Convert.ToSingle(GetConfig("AutoLights", "Lights On Time", "18.0"));
            AutolightOffTime = Convert.ToSingle(GetConfig("AutoLights", "Lights Off Time", "8.0"));
            prefix = Convert.ToString(GetConfig("Chat", "Prefix", "<color=#FA58AC>ZoneManager:</color> "));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }


        ////////////////////////////////////////////
        /// FIELDS
        ////////////////////////////////////////////

        private readonly Dictionary<string, ZoneDefinition> ZoneDefinitions = new Dictionary<string, ZoneDefinition>();
        private readonly Dictionary<ulong, string> LastZone = new Dictionary<ulong, string>();
        private readonly Dictionary<BasePlayer, HashSet<Zone>> playerZones = new Dictionary<BasePlayer, HashSet<Zone>>();
        private readonly Dictionary<BaseCombatEntity, HashSet<Zone>> buildingZones = new Dictionary<BaseCombatEntity, HashSet<Zone>>();
        private readonly Dictionary<BaseNPC, HashSet<Zone>> npcZones = new Dictionary<BaseNPC, HashSet<Zone>>();
        private readonly Dictionary<ResourceDispenser, HashSet<Zone>> resourceZones = new Dictionary<ResourceDispenser, HashSet<Zone>>();
        private readonly Dictionary<BaseEntity, HashSet<Zone>> otherZones = new Dictionary<BaseEntity, HashSet<Zone>>();
        private readonly Dictionary<BasePlayer, ZoneFlags> playerTags = new Dictionary<BasePlayer, ZoneFlags>();

        private ZoneFlags disabledFlags = ZoneFlags.None;
        private DynamicConfigFile ZoneManagerData;
        private StoredData storedData;
        private Zone[] zoneObjects = new Zone[0];

        private readonly FieldInfo npcNextTick = typeof(NPCAI).GetField("nextTick", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        //private static readonly int triggerLayer = LayerMask.NameToLayer("Trigger");
        private static readonly int playersMask = LayerMask.GetMask("Player (Server)");
        //private static readonly int buildingMask = LayerMask.GetMask("Deployed", "Player (Server)", "Default", "Prevent Building");

        private static readonly Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic))?.GetValue(null);

        /////////////////////////////////////////
        // Zone
        // is a Monobehavior
        // used to detect the colliders with players
        // and created everything on it's own (radiations, locations, etc)
        /////////////////////////////////////////

        private static float GetSkyHour()
        {
            return TOD_Sky.Instance.Cycle.Hour;
        }

        public class Zone : MonoBehaviour
        {
            public ZoneDefinition Info;
            public ZoneManager ZoneManagerPlugin;
            public ZoneFlags disabledFlags = ZoneFlags.None;

            public readonly HashSet<ulong> WhiteList = new HashSet<ulong>();
            public readonly HashSet<ulong> KeepInList = new HashSet<ulong>();

            private HashSet<BasePlayer> players = new HashSet<BasePlayer>();
            private HashSet<BaseCombatEntity> buildings = new HashSet<BaseCombatEntity>();

            private bool lightsOn;

            private readonly FieldInfo InstancesField = typeof(MeshColliderBatch).GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic);

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1; //hack to get all trigger layers...otherwise child zones
                gameObject.name = "Zone Manager";

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }

            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                var boxCollider = gameObject.GetComponent<BoxCollider>();
                if (Info.Size != Vector3.zero)
                {
                    if (sphereCollider != null) Destroy(sphereCollider);
                    if (boxCollider == null)
                    {
                        boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.isTrigger = true;
                    }
                    boxCollider.size = Info.Size;
                }
                else
                {
                    if (boxCollider != null) Destroy(boxCollider);
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = Info.radius;
                }
            }

            public void SetInfo(ZoneDefinition info)
            {
                Info = info;
                if (Info == null) return;
                gameObject.name = $"Zone Manager({Info.ID})";
                transform.position = Info.Location;
                transform.rotation = Quaternion.Euler(Info.Rotation);
                UpdateCollider();
                gameObject.SetActive(Info.enabled);
                enabled = Info.enabled;

                if (ZoneManagerPlugin.HasZoneFlag(this, ZoneFlags.AutoLights))
                {
                    var currentTime = GetSkyHour();

                    if (currentTime > ZoneManagerPlugin.AutolightOffTime && currentTime < ZoneManagerPlugin.AutolightOnTime)
                        lightsOn = true;
                    else
                        lightsOn = false;
                    if (IsInvoking("CheckLights")) CancelInvoke("CheckLights");
                    InvokeRepeating("CheckLights", 5f, 30f);
                }

                var radiation = gameObject.GetComponent<TriggerRadiation>();
                if (Info.radiation > 0)
                {
                    radiation = radiation ?? gameObject.AddComponent<TriggerRadiation>();
                    radiation.RadiationAmount = Info.radiation;
                    radiation.radiationSize = Info.radius;
                    radiation.interestLayers = playersMask;
                    radiation.enabled = Info.enabled;
                }
                else if (radiation != null)
                {
                    radiation.RadiationAmount = 0;
                    radiation.radiationSize = 0;
                    radiation.interestLayers = playersMask;
                    radiation.enabled = false;
                    //Destroy(radiation);
                }
                if (IsInvoking("CheckEntites")) CancelInvoke("CheckEntites");
                InvokeRepeating("CheckEntites", 10f, 10f);
                /*if (HasAnyFlag(info.flags, ZoneFlags.Eject | ZoneFlags.EjectSleepers
                    | ZoneFlags.KillSleepers | ZoneFlags.NoBleed | ZoneFlags.NoBoxLoot | ZoneFlags.NoBuild
                    | ZoneFlags.NoChat | ZoneFlags.NoDeploy | ZoneFlags.NoDrown | ZoneFlags.NoKits
                    | ZoneFlags.NoPlayerLoot | ZoneFlags.NoRemove | ZoneFlags.NoSuicide | ZoneFlags.NoTp
                    | ZoneFlags.NoUpgrade | ZoneFlags.NoWounded | ZoneFlags.PveGod | ZoneFlags.PvpGod
                    | ZoneFlags.SleepGod | ZoneFlags.AutoLights))
                {
                    Interface.Oxide.LogInfo("Mask: Player (Server)");
                }*/
            }

            private void CheckEntites()
            {
                if (ZoneManagerPlugin == null) return;
                var oldPlayers = players;
                players = new HashSet<BasePlayer>();
                int entities;
                if (Info.Size != Vector3.zero)
                    entities = Physics.OverlapBoxNonAlloc(Info.Location, Info.Size, colBuffer, Quaternion.Euler(Info.Rotation), playersMask);
                else
                    entities = Physics.OverlapSphereNonAlloc(Info.Location, Info.radius, colBuffer, playersMask);
                for (var i = 0; i < entities; i++)
                {
                    var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    if (player != null)
                    {
                        if (players.Add(player) && !oldPlayers.Contains(player))
                            ZoneManagerPlugin.OnPlayerEnterZone(this, player);
                    }
                }
                foreach (var player in oldPlayers)
                {
                    if (!players.Contains(player))
                        ZoneManagerPlugin.OnPlayerExitZone(this, player);
                }
            }

            private void OnDestroy()
            {
                CancelInvoke("CheckLights");
                ZoneManagerPlugin.OnZoneDestroy(this);
                ZoneManagerPlugin = null;
                Destroy(gameObject);
            }

            private void CheckLights()
            {
                if (ZoneManagerPlugin == null) return;
                var currentTime = GetSkyHour();
                if (currentTime > ZoneManagerPlugin.AutolightOffTime && currentTime < ZoneManagerPlugin.AutolightOnTime)
                {
                    if (!lightsOn) return;
                    foreach (var building in buildings)
                    {
                        var oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking("Cook"))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, false);
                            continue;
                        }
                        var door = building as Door;
                        if (door != null && door.LookupPrefabName().Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, true);
                    }
                    foreach (var player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue; //only npc
                        var items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner")) continue;
                            item.SwitchOnOff(false, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = false;
                }
                else
                {
                    if (lightsOn) return;
                    foreach (var building in buildings)
                    {
                        var oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking("Cook"))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, true);
                            continue;
                        }
                        var door = building as Door;
                        if (door != null && door.LookupPrefabName().Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, false);
                    }
                    var fuel = ItemManager.FindItemDefinition("lowgradefuel");
                    foreach (var player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue; // only npc
                        var items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner")) continue;
                            if (item.contents == null) item.contents = new ItemContainer();
                            var array = item.contents.itemList.ToArray();
                            for (var i = 0; i < array.Length; i++)
                                array[i].Remove(0f);
                            var newItem = ItemManager.Create(fuel, 100);
                            newItem.MoveToContainer(item.contents);
                            item.SwitchOnOff(true, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = true;
                }
            }

            public void OnEntityKill(BaseCombatEntity entity)
            {
                if (entity is BasePlayer)
                    players.Remove((BasePlayer)entity);
                else if (!(entity is LootContainer) && !(entity is BaseHelicopter) && !(entity is BaseNPC))
                    buildings.Remove(entity);
            }

            private void CheckCollisionEnter(Collider col)
            {
                if (ZoneManagerPlugin.HasZoneFlag(this, ZoneFlags.NoDecay))
                {
                    var decay = col.GetComponentInParent<Decay>();
                    if (decay != null)
                    {
                        decay.CancelInvoke("RunDecay");
                        decay.enabled = false;
                    }
                }
                var resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null) //also BaseCorpse
                {
                    ZoneManagerPlugin.OnResourceEnterZone(this, resourceDispenser);
                    return;
                }
                var entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null) return;
                if (entity is BaseNPC)
                {
                    ZoneManagerPlugin.OnNpcEnterZone(this, (BaseNPC)entity);
                }
                else if (entity is BaseCombatEntity && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Add((BaseCombatEntity) entity);
                    ZoneManagerPlugin.OnBuildingEnterZone(this, (BaseCombatEntity)entity);
                }
                else
                {
                    ZoneManagerPlugin.OnOtherEnterZone(this, entity);
                }
            }

            private void CheckCollisionLeave(Collider col)
            {
                if (ZoneManagerPlugin.HasZoneFlag(this, ZoneFlags.NoDecay))
                {
                    var decay = col.GetComponentInParent<Decay>();
                    if (decay != null) decay.enabled = true;
                }
                var resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null)
                {
                    ZoneManagerPlugin.OnResourceExitZone(this, resourceDispenser);
                    return;
                }
                var entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null) return;
                if (entity is BaseNPC)
                {
                    ZoneManagerPlugin.OnNpcExitZone(this, (BaseNPC)entity);
                }
                else if (entity is BaseCombatEntity && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Remove((BaseCombatEntity) entity);
                    ZoneManagerPlugin.OnBuildingExitZone(this, (BaseCombatEntity)entity);
                }
                else
                {
                    ZoneManagerPlugin.OnOtherExitZone(this, entity);
                }
            }

            private void OnTriggerEnter(Collider col)
            {
                //Interface.Oxide.LogInfo("Enter {0}: {1}", Info.ID, col.name);
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (!players.Add(player)) return;
                    ZoneManagerPlugin.OnPlayerEnterZone(this, player);
                }
                else if (!col.transform.CompareTag("MeshColliderBatch"))
                    CheckCollisionEnter(col);
                else
                {
                    var colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch != null)
                    {
                        var colliders = (ListDictionary<Component, ColliderCombineInstance>) InstancesField.GetValue(colliderBatch);
                        foreach (var instance in colliders.Values)
                            CheckCollisionEnter(instance.collider);
                    }
                }
            }

            private void OnTriggerExit(Collider col)
            {
                //Interface.Oxide.LogInfo("Exit {0}: {1}", Info.ID, col.name);
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    if (!players.Remove(player)) return;
                    ZoneManagerPlugin.OnPlayerExitZone(this, player);
                }
                else if(!col.transform.CompareTag("MeshColliderBatch"))
                    CheckCollisionLeave(col);
                else
                {
                    var colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch != null)
                    {
                        var colliders = (ListDictionary<Component, ColliderCombineInstance>) InstancesField.GetValue(colliderBatch);
                        foreach (var instance in colliders.Values)
                            CheckCollisionLeave(instance.collider);
                    }
                }
            }
        }

        /////////////////////////////////////////
        // ZoneDefinition
        // Stored informations on the zones
        /////////////////////////////////////////
        public class ZoneDefinition
        {

            public string name;
            public float radius;
            public float radiation;
            public Vector3 Location;
            public Vector3 Size;
            public Vector3 Rotation;
            public string ID;
            public string enter_message;
            public string leave_message;
            public bool enabled = true;
            public ZoneFlags flags;

            public ZoneDefinition()
            {

            }

            public ZoneDefinition(Vector3 position)
            {
                radius = 20f;
                Location = position;
            }

        }
        [Flags]
        public enum ZoneFlags// : long
        {
            None = 0,
            AutoLights = 1,
            Eject = 1 << 1,
            PvpGod = 1 << 2,
            PveGod = 1 << 3,
            SleepGod = 1 << 4,
            UnDestr = 1 << 5,
            NoBuild = 1 << 6,
            NoTp = 1 << 7,
            NoChat = 1 << 8,
            NoGather = 1 << 9,
            NoPve = 1 << 10,
            NoWounded = 1 << 11,
            NoDecay = 1 << 12,
            NoDeploy = 1 << 13,
            NoKits = 1 << 14,
            NoBoxLoot = 1 << 15,
            NoPlayerLoot = 1 << 16,
            NoCorpse = 1 << 17,
            NoSuicide = 1 << 18,
            NoRemove = 1 << 19,
            NoBleed = 1 << 20,
            KillSleepers = 1 << 21,
            NpcFreeze = 1 << 22,
            NoDrown = 1 << 23,
            NoStability = 1 << 24,
            NoUpgrade = 1 << 25,
            EjectSleepers = 1 << 26,
            NoPickup = 1 << 27
        }

        private bool HasZoneFlag(Zone zone, ZoneFlags flag)
        {
            if ((disabledFlags & flag) == flag) return false;
            return (zone.Info.flags & ~zone.disabledFlags & flag) == flag;
        }
        private static bool HasAnyFlag(ZoneFlags flags, ZoneFlags flag)
        {
            return (flags & flag) != ZoneFlags.None;
        }
        private static bool HasAnyZoneFlag(Zone zone)
        {
            return (zone.Info.flags & ~zone.disabledFlags) != ZoneFlags.None;
        }
        private static void AddZoneFlag(ZoneDefinition zone, ZoneFlags flag)
        {
            zone.flags |= flag;
        }
        private static void RemoveZoneFlag(ZoneDefinition zone, ZoneFlags flag)
        {
            zone.flags &= ~flag;
        }
        /////////////////////////////////////////
        // Data Management
        /////////////////////////////////////////
        private class StoredData
        {
            public readonly HashSet<ZoneDefinition> ZoneDefinitions = new HashSet<ZoneDefinition>();
        }

        private void SaveData()
        {
            ZoneManagerData.WriteObject(storedData);
        }

        private void LoadData()
        {
            ZoneDefinitions.Clear();
            try
            {
                ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ZoneManagerData.ReadObject<StoredData>();
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Include;
            foreach (var zonedef in storedData.ZoneDefinitions)
                ZoneDefinitions[zonedef.ID] = zonedef;
        }

        private void SetupCollectibleEntity()
        {
            var collectibleEntities = Resources.FindObjectsOfTypeAll<CollectibleEntity>();
            //Puts("Found {0} CollectibleEntities.", collectibleEntities.Length);
            foreach (var collectibleEntity in collectibleEntities)
                if (collectibleEntity.GetComponent<Collider>() == null) collectibleEntity.gameObject.AddComponent<BoxCollider>();
        }

        /////////////////////////////////////////
        // OXIDE HOOKS
        /////////////////////////////////////////

        /////////////////////////////////////////
        // Loaded()
        // Called when the plugin is loaded
        /////////////////////////////////////////
        private void Loaded()
        {
            //Puts("ZoneManager loaded: {0}", GetHashCode());
            ZoneManagerData = Interface.Oxide.DataFileSystem.GetFile("ZoneManager");
            ZoneManagerData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };
            permission.RegisterPermission(PermZone, this);
            permission.RegisterPermission(PermCanDeploy, this);
            permission.RegisterPermission(PermCanBuild, this);
            /* for(int i = 0; i < 25; i ++)
             {
                 Debug.Log(UnityEngine.LayerMask.LayerToName(i));
             }*/
            LoadData();
            LoadVariables();
            /*string[] options = new string[32];
            for (int i = 0; i < 32; i++)
            { // get layer names
                options[i] = i + " : " + LayerMask.LayerToName(i);
            }
            Puts("Layers: {0}", string.Join(", ", options));
            var sb = new StringBuilder();
            sb.AppendLine();
            for (int i = 0; i < 32; i++)
            {
                sb.Append(i + ":\t");
                for (int j = 0; j < 32; j++)
                {
                    sb.Append(Physics.GetIgnoreLayerCollision(i, j) ? "  " : "X ");
                }
                sb.AppendLine();
            }
            Puts(sb.ToString());*/
        }
        /////////////////////////////////////////
        // Unload()
        // Called when the plugin is unloaded
        /////////////////////////////////////////
        private void Unload()
        {
            foreach (var zone in zoneObjects)
                UnityEngine.Object.Destroy(zone);
            NextTick(() =>
            {
                var collectibleEntities = Resources.FindObjectsOfTypeAll<CollectibleEntity>();
                foreach (var collectibleEntity in collectibleEntities)
                    if (collectibleEntity.GetComponent<Collider>() != null) UnityEngine.Object.Destroy(collectibleEntity.GetComponent<Collider>());
            });
        }

        private void OnTerrainInitialized()
        {
            if (Initialized) return;
            SetupCollectibleEntity();
            foreach (var zoneDefinition in ZoneDefinitions.Values)
                NewZone(zoneDefinition);
            Initialized = true;
        }

        private void OnServerInitialized()
        {
            if (Initialized) return;
            NextTick(() => {
                SetupCollectibleEntity();
                foreach (var zoneDefinition in ZoneDefinitions.Values)
                    NewZone(zoneDefinition);
            });
            Initialized = true;
        }

        /////////////////////////////////////////
        // OnEntityBuilt(Planner planner, GameObject gameobject)
        // Called when a buildingblock was created
        /////////////////////////////////////////
        private void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner.ownerPlayer == null) return;
            if (HasPlayerFlag(planner.ownerPlayer, ZoneFlags.NoBuild) && !hasPermission(planner.ownerPlayer, PermCanBuild))
            {
                gameobject.GetComponentInParent<BaseCombatEntity>().Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(planner.ownerPlayer, "You are not allowed to build here");
            }
        }

        private object OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoUpgrade) && !isAdmin(player)) return false;
            return null;
        }

        /////////////////////////////////////////
        // OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        // Called when an item was deployed
        /////////////////////////////////////////
        private void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            if (deployer.ownerPlayer == null) return;
            if (HasPlayerFlag(deployer.ownerPlayer, ZoneFlags.NoDeploy) && !hasPermission(deployer.ownerPlayer, PermCanDeploy))
            {
                deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(deployer.ownerPlayer, "You are not allowed to deploy here");
            }
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            if (!(ownerEntity is BasePlayer)) return;
            if (metabolism.bleeding.value > 0 && HasPlayerFlag((BasePlayer)ownerEntity, ZoneFlags.NoBleed))
                metabolism.bleeding.value = 0f;
            if (metabolism.oxygen.value < 1 && HasPlayerFlag((BasePlayer)ownerEntity, ZoneFlags.NoDrown))
                metabolism.oxygen.value = 1;
        }

        /////////////////////////////////////////
        // OnPlayerChat(ConsoleSystem.Arg arg)
        // Called when a user writes something in the chat, doesn't take in count the commands
        /////////////////////////////////////////
        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (HasPlayerFlag(arg.Player(), ZoneFlags.NoChat))
            {
                SendMessage(arg.Player(), "You are not allowed to chat here");
                return false;
            }
            return null;
        }

        /////////////////////////////////////////
        // OnRunCommand(ConsoleSystem.Arg arg)
        // Called when a user executes a command
        /////////////////////////////////////////
        private object OnRunCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (arg.cmd?.name == null) return null;
            if (arg.cmd.name == "kill" && HasPlayerFlag(arg.Player(), ZoneFlags.NoSuicide))
            {
                SendMessage(arg.Player(), "You are not allowed to suicide here");
                return false;
            }
            return null;
        }

        /////////////////////////////////////////
        // OnPlayerDisconnected(BasePlayer player)
        // Called when a user disconnects
        /////////////////////////////////////////
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.KillSleepers) && !isAdmin(player)) player.Die();
            else if (HasPlayerFlag(player, ZoneFlags.EjectSleepers))
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.EjectSleepers))
                    {
                        EjectPlayer(zone, player);
                        break;
                    }
                }
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            var disp = hitinfo.HitEntity?.GetComponent<ResourceDispenser>();
            if (disp == null) return;
            HashSet<Zone> resourceZone;
            if (!resourceZones.TryGetValue(disp, out resourceZone)) return;
            foreach (var zone in resourceZone)
            {
                if (HasZoneFlag(zone, ZoneFlags.NoGather))
                    hitinfo.HitEntity = null;
            }
        }

        /////////////////////////////////////////
        // OnEntityAttacked(BaseCombatEntity entity, HitInfo hitinfo)
        // Called when any entity is attacked
        /////////////////////////////////////////
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity == null) return;
            var resource = entity.GetComponent<ResourceDispenser>();
            if (resource != null)
            {
            }
            else if (entity is BasePlayer)
            {
                var player = (BasePlayer)entity;
                if (player.IsSleeping() && HasPlayerFlag(player, ZoneFlags.SleepGod))
                {
                    CancelDamage(hitinfo);
                }
                else if (hitinfo.Initiator is BasePlayer)
                {
                    if (((BasePlayer)hitinfo.Initiator).userID < 76560000000000000L) return;
                    if (HasPlayerFlag(player, ZoneFlags.PvpGod))
                        CancelDamage(hitinfo);
                    else if (HasPlayerFlag((BasePlayer)hitinfo.Initiator, ZoneFlags.PvpGod))
                        CancelDamage(hitinfo);
                }
                else if (HasPlayerFlag(player, ZoneFlags.PveGod))
                    CancelDamage(hitinfo);
            }
            else if (entity is BaseNPC)
            {
                var npcai = (BaseNPC)entity;
                HashSet<Zone> zones;
                if (!npcZones.TryGetValue(npcai, out zones)) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.NoPve))
                    {
                        CancelDamage(hitinfo);
                        break;
                    }
                }
            }
            else if (!(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                HashSet<Zone> zones;
                if (!buildingZones.TryGetValue(entity, out zones)) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.UnDestr))
                    {
                        CancelDamage(hitinfo);
                        break;
                    }
                }
            }
            else
            {
                HashSet<Zone> zones;
                if (otherZones.TryGetValue(entity, out zones))
                {
                }
            }
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            var entity = networkable as BaseEntity;
            if (entity == null) return;
            var resource = entity.GetComponent<ResourceDispenser>();
            if (resource != null)
            {
                HashSet<Zone> zones;
                if (resourceZones.TryGetValue(resource, out zones))
                {
                    foreach (var source in zones.ToArray())
                        OnResourceExitZone(source, resource);
                }
            }
            else if (entity is BasePlayer)
            {
                var player = (BasePlayer)entity;
                HashSet<Zone> zones;
                if (playerZones.TryGetValue(player, out zones))
                {
                    foreach (var zone in zones.ToArray())
                        OnPlayerExitZone(zone, player);
                }
            }
            else if (entity is BaseNPC)
            {
                var npc = (BaseNPC)entity;
                HashSet<Zone> zones;
                if (npcZones.TryGetValue(npc, out zones))
                {
                    foreach (var source in zones.ToArray())
                        OnNpcExitZone(source, npc);
                }
            }
            else if (entity is BaseCombatEntity && !(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                var building = (BaseCombatEntity)entity;
                HashSet<Zone> zones;
                if (buildingZones.TryGetValue(building, out zones))
                {
                    foreach (var source in zones.ToArray())
                        OnBuildingExitZone(source, building);
                }
            }
            else
            {
                HashSet<Zone> zones;
                if (otherZones.TryGetValue(entity, out zones))
                {
                    foreach (var source in zones.ToArray())
                        OnOtherExitZone(source, entity);
                }
            }
        }


        /////////////////////////////////////////
        // OnEntitySpawned(BaseNetworkable entity)
        // Called when any kind of entity is spawned in the world
        /////////////////////////////////////////
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseCorpse)
            {
                timer.Once(2f, () =>
                {
                    HashSet<Zone> zones;
                    if (entity.isDestroyed || !resourceZones.TryGetValue(entity.GetComponent<ResourceDispenser>(), out zones)) return;
                    foreach (var zone in zones)
                    {
                        if (HasZoneFlag(zone, ZoneFlags.NoCorpse))
                        {
                            entity.KillMessage();
                            break;
                        }
                    }
                });
            }
            else if (entity is BuildingBlock && zoneObjects != null)
            {
                var block = (BuildingBlock)entity;
                foreach (var zone in zoneObjects)
                {
                    if (HasZoneFlag(zone, ZoneFlags.NoStability))
                    {
                        if (zone.Info.Size != Vector3.zero)
                        {
                            if (!new Bounds(zone.Info.Location, Quaternion.Euler(zone.Info.Rotation) * zone.Info.Size).Contains(block.transform.position))
                                continue;
                        }
                        else if (Vector3.Distance(block.transform.position, zone.Info.Location) > zone.Info.radius)
                            continue;
                        block.grounded = true;
                        break;
                    }
                }
            }
            var npc = entity.GetComponent<NPCAI>();
            if (npc != null)
                npcNextTick.SetValue(npc, Time.time + 10f);
        }

        /////////////////////////////////////////
        // OnPlayerLoot(PlayerLoot lootInventory,  BasePlayer targetPlayer)
        // Called when a player tries to loot another player
        /////////////////////////////////////////
        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            return OnLootPlayerInternal(looter, target) ? null : (object)false;
        }

        private void OnLootPlayer(BasePlayer looter, BasePlayer target)
        {
            OnLootPlayerInternal(looter, target);
        }

        private bool OnLootPlayerInternal(BasePlayer looter, BasePlayer target)
        {
            if (HasPlayerFlag(looter, ZoneFlags.NoPlayerLoot) || target != null && HasPlayerFlag(target, ZoneFlags.NoPlayerLoot))
            {
                NextTick(looter.EndLooting);
                return false;
            }
            return true;
        }

        private void OnLootEntity(BasePlayer looter, BaseEntity target)
        {
            if (target is BaseCorpse)
                OnLootPlayerInternal(looter, null);
            else if (HasPlayerFlag(looter, ZoneFlags.NoBoxLoot))
            {
                if (target is StorageContainer && ((StorageContainer)target).transform.position == Vector3.zero) return;
                timer.Once(0.01f, looter.EndLooting);
            }
        }

        /////////////////////////////////////////
        // CanBeWounded(BasePlayer player)
        // Called from the Kits plugin (Reneb) when trying to redeem a kit
        /////////////////////////////////////////
        private object CanBeWounded(BasePlayer player, HitInfo hitinfo)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoWounded)) { return false; }
            return null;
        }

        /////////////////////////////////////////
        // Outside Plugin Hooks
        /////////////////////////////////////////

        /////////////////////////////////////////
        // canRedeemKit(BasePlayer player)
        // Called from the Kits plugin (Reneb) when trying to redeem a kit
        /////////////////////////////////////////
        private object canRedeemKit(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoKits)) { return "You may not redeem a kit inside this area"; }
            return null;
        }

        /////////////////////////////////////////
        // canTeleport(BasePlayer player)
        // Called from Teleportation System (Mughisi) when a player tries to teleport
        /////////////////////////////////////////
        private object CanTeleport(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoTp)) { return "You may not teleport in this area"; }
            return null;
        }

        /////////////////////////////////////////
        // canRemove(BasePlayer player)
        // Called from Teleportation System (Mughisi) when a player tries to teleport
        /////////////////////////////////////////
        private object canRemove(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoRemove)) { return "You may not use the remover tool in this area"; }
            return null;
        }

        private void UpdateZoneDefinition(ZoneDefinition zone, string[] args, BasePlayer player = null)
        {
            for (var i = 0; i < args.Length; i = i + 2)
            {
                object editvalue;
                switch (args[i].ToLower())
                {
                    case "name":
                        editvalue = zone.name = args[i + 1];
                        break;
                    case "id":
                        editvalue = zone.ID = args[i + 1];
                        break;
                    case "radiation":
                        editvalue = zone.radiation = Convert.ToSingle(args[i + 1]);
                        break;
                    case "radius":
                        editvalue = zone.radius = Convert.ToSingle(args[i + 1]);
                        break;
                    case "rotation":
                        zone.Rotation = player?.GetNetworkRotation() ?? Vector3.zero;/* + Quaternion.AngleAxis(90, Vector3.up).eulerAngles*/
                        zone.Rotation.x = 0;
                        editvalue = zone.Rotation;
                        break;
                    case "size":
                        var size = args[i + 1].Trim().Split(' ');
                        if (size.Length == 3)
                            editvalue = zone.Size = new Vector3(Convert.ToSingle(size[0]), Convert.ToSingle(size[1]), Convert.ToSingle(size[2]));
                        else
                        {
                            if (player != null) SendMessage(player, "Invalid size format, use: \"x y z\"");
                            continue;
                        }
                        break;
                    case "enter_message":
                        editvalue = zone.enter_message = args[i + 1];
                        break;
                    case "leave_message":
                        editvalue = zone.leave_message = args[i + 1];
                        break;
                    case "enabled":
                    case "enable":
                        editvalue = zone.enabled = GetBoolValue(args[i + 1]);
                        break;
                    default:
                        try
                        {
                            var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), args[i], true);
                            var boolValue = GetBoolValue(args[i + 1]);
                            editvalue = boolValue;
                            if (boolValue) AddZoneFlag(zone, flag);
                            else RemoveZoneFlag(zone, flag);
                        }
                        catch
                        {
                            if (player != null) SendMessage(player, $"Unknown zone flag: {args[i]}");
                            continue;
                        }
                        break;
                }
                if (player != null) SendMessage(player, $"{args[i]} set to {editvalue}");
            }
        }

        /////////////////////////////////////////
        // External calls to this plugin
        /////////////////////////////////////////

        /////////////////////////////////////////
        // CreateOrUpdateZone(string ZoneID, object[] args)
        // Create or Update a zone from an external plugin
        // ZoneID should be a name, like Arena (for an arena plugin) (even if it's called an ID :p)
        // args are the same a the /zone command
        // args[0] = "radius" args[1] = "50" args[2] = "Eject" args[3] = "true", etc
        // Third parameter is obviously need if you create a NEW zone (or want to update the position)
        /////////////////////////////////////////
        private bool CreateOrUpdateZone(string zoneId, string[] args, Vector3 position = default(Vector3))
        {
            ZoneDefinition zonedef;
            if (!ZoneDefinitions.TryGetValue(zoneId, out zonedef))
                zonedef = new ZoneDefinition { ID = zoneId, radius = 20 };
            else
                storedData.ZoneDefinitions.Remove(zonedef);
            UpdateZoneDefinition(zonedef, args);

            if (position != default(Vector3))
                zonedef.Location = position;

            ZoneDefinitions[zoneId] = zonedef;
            storedData.ZoneDefinitions.Add(zonedef);
            SaveData();

            if (zonedef.Location == null) return false;
            RefreshZone(zoneId);
            return true;
        }

        private bool EraseZone(string zoneId)
        {
            ZoneDefinition zone;
            if (!ZoneDefinitions.TryGetValue(zoneId, out zone)) return false;

            storedData.ZoneDefinitions.Remove(zone);
            ZoneDefinitions.Remove(zoneId);
            SaveData();
            RefreshZone(zoneId);
            return true;
        }

        private List<string> ZoneFieldListRaw()
        {
            var list = new List<string> { "name", "ID", "radiation", "radius", "rotation", "size", "Location", "enter_message", "leave_message" };
            list.AddRange(Enum.GetNames(typeof(ZoneFlags)));
            return list;
        }

        private Dictionary<string, string> ZoneFieldList(string zoneId)
        {
            var zone = GetZoneByID(zoneId);
            if (zone == null) return null;
            var fieldlistzone = new Dictionary<string, string>
            {
                { "name", zone.Info.name },
                { "ID", zone.Info.ID },
                { "radiation", zone.Info.radiation.ToString() },
                { "radius", zone.Info.radius.ToString() },
                { "rotation", zone.Info.Rotation.ToString() },
                { "size", zone.Info.Size.ToString() },
                { "Location", zone.Info.Location.ToString() },
                { "enter_message", zone.Info.enter_message },
                { "leave_message", zone.Info.leave_message }
            };

            var values = Enum.GetValues(typeof(ZoneFlags));
            foreach (var value in values)
                fieldlistzone[Enum.GetName(typeof(ZoneFlags), value)] = HasZoneFlag(zone, (ZoneFlags)value).ToString();
            return fieldlistzone;
        }

        private List<ulong> GetPlayersInZone(string zoneId)
        {
            var players = new List<ulong>();
            foreach (var pair in playerZones)
                players.AddRange(pair.Value.Where(zone => zone.Info.ID == zoneId).Select(zone => pair.Key.userID));
            return players;
        }

        private bool isPlayerInZone(string zoneId, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return false;
            return zones.Any(zone => zone.Info.ID == zoneId);
        }

        private bool AddPlayerToZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToWhitelist(targetZone, player);
            return true;
        }

        private bool AddPlayerToZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToKeepinlist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromWhitelist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromKeepinlist(targetZone, player);
            return true;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        private void ShowZone(BasePlayer player, string zoneId)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return;
            if (targetZone.Info.Size != Vector3.zero)
            {
                //player.SendConsoleCommand("ddraw.box", 10f, Color.blue, targetZone.Info.Location, targetZone.Info.Size.magnitude);
                var center = targetZone.Info.Location;
                var rotation = Quaternion.Euler(targetZone.Info.Rotation);
                var size = targetZone.Info.Size / 2;
                var point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
                var point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
                var point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
                var point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
                var point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point3);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point5);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point3);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point8);

                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point6);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point7);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point6, point2);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point6);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point7);
                player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point7, point3);
            }
            else
                player.SendConsoleCommand("ddraw.sphere", 10f, Color.blue, targetZone.Info.Location, targetZone.Info.radius);
        }

        /////////////////////////////////////////
        // Random Commands
        /////////////////////////////////////////
        private object GetZoneRadius(string zoneID) => GetZoneByID(zoneID)?.Info.radius;
        private object GetZoneSize(string zoneID) => GetZoneByID(zoneID)?.Info.Size;
        private object CheckZoneID(string zoneID) => GetZoneByID(zoneID)?.Info.ID;
        private object GetZoneIDs() => zoneObjects.ToList().ConvertAll(z => z.Info.ID).ToArray();
        private Vector3 GetZoneLocation(string zoneId) => GetZoneByID(zoneId)?.Info.Location ?? Vector3.zero;
        private void AddToWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Add(player.userID); }
        private void RemoveFromWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Remove(player.userID); }
        private void AddToKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Add(player.userID); }
        private void RemoveFromKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Remove(player.userID); }

        private void AddDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags |= flag;
            }
            catch
            {
            }
        }

        private void RemoveDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags &= ~flag;
            }
            catch
            {
            }
        }

        private void AddZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags |= flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private void RemoveZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags &= ~flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private Zone GetZoneByID(string zoneId)
        {
            return zoneObjects.FirstOrDefault(gameObj => gameObj.Info.ID == zoneId);
        }

        private void NewZone(ZoneDefinition zonedef)
        {
            if (zonedef == null) return;
            var newZone = new GameObject().AddComponent<Zone>();
            newZone.ZoneManagerPlugin = this;
            newZone.SetInfo(zonedef);
            zoneObjects = Resources.FindObjectsOfTypeAll<Zone>();
        }

        private void RefreshZone(string zoneId)
        {
            var zone = GetZoneByID(zoneId);
            if (zone != null)
                UnityEngine.Object.Destroy(zone);
            ZoneDefinition zoneDef;
            if (ZoneDefinitions.TryGetValue(zoneId, out zoneDef))
                NewZone(zoneDef);
        }

        private void UpdateAllPlayers()
        {
            var players = playerTags.Keys.ToArray();
            foreach (var player in players)
                UpdateFlags(player);
        }

        private void UpdateFlags(BasePlayer player)
        {
            playerTags.Remove(player);
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
            var newFlags = ZoneFlags.None;
            foreach (var zone in zones)
                newFlags |= zone.Info.flags & ~zone.disabledFlags;
            playerTags[player] = newFlags;
        }

        private bool HasPlayerFlag(BasePlayer player, ZoneFlags flag)
        {
            if ((disabledFlags & flag) == flag) return false;
            ZoneFlags tags;
            if (!playerTags.TryGetValue(player, out tags)) return false;
            return (tags & flag) == flag;
        }

        private BasePlayer FindPlayerByRadius(Vector3 position, float rad)
        {
            var cachedColliders = Physics.OverlapSphere(position, rad, playersMask);
            return cachedColliders.Select(collider => collider.GetComponentInParent<BasePlayer>()).FirstOrDefault(player => player != null);
        }

        private void CheckExplosivePosition(TimedExplosive explosive)
        {
            if (explosive == null) return;
            foreach (var zone in zoneObjects)
            {
                if (!HasZoneFlag(zone, ZoneFlags.UnDestr)) continue;
                if (Vector3.Distance(explosive.GetEstimatedWorldPosition(), zone.transform.position) > zone.Info.radius) continue;
                explosive.KillMessage();
                break;
            }
        }

        private static void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = new DamageTypeList();
            hitinfo.DoHitEffects = false;
            hitinfo.HitMaterial = 0;
        }

        private void OnPlayerEnterZone(Zone zone, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones))
                playerZones[player] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            UpdateFlags(player);
            if (!string.IsNullOrEmpty(zone.Info.enter_message))
            {
                if (PopupNotifications != null && usePopups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.Info.enter_message, player.displayName), player);
                else
                    SendMessage(player, zone.Info.enter_message, player.displayName);
            }
            if (HasZoneFlag(zone, ZoneFlags.Eject)) EjectPlayer(zone, player);
            Interface.Oxide.CallHook("OnEnterZone", zone.Info.ID, player);
            //Puts("OnPlayerEnterZone: {0}", player.GetType());
        }

        private void OnPlayerExitZone(Zone zone, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (playerZones.TryGetValue(player, out zones))
            {
                zone.OnEntityKill(player);
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) playerZones.Remove(player);
            }
            UpdateFlags(player);
            if (!string.IsNullOrEmpty(zone.Info.leave_message))
            {
                if (PopupNotifications != null && usePopups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.Info.leave_message, player.displayName), player);
                else
                    SendMessage(player, zone.Info.leave_message, player.displayName);
            }
            if (zone.KeepInList.Contains(player.userID)) AttractPlayer(zone, player);
            Interface.Oxide.CallHook("OnExitZone", zone.Info.ID, player);
            //Puts("OnPlayerExitZone: {0}", player.GetType());
        }

        private void OnResourceEnterZone(Zone zone, ResourceDispenser entity)
        {
            HashSet<Zone> zones;
            if (!resourceZones.TryGetValue(entity, out zones))
                resourceZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            //Puts("OnResourceEnterZone: {0}", entity.GetType());
        }

        private void OnResourceExitZone(Zone zone, ResourceDispenser resource)
        {
            HashSet<Zone> zones;
            if (resourceZones.TryGetValue(resource, out zones))
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) resourceZones.Remove(resource);
            }
            //Puts("OnResourceExitZone: {0}", resource.GetType());
        }

        private void OnNpcEnterZone(Zone zone, BaseNPC entity)
        {
            HashSet<Zone> zones;
            if (!npcZones.TryGetValue(entity, out zones))
                npcZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            if (HasZoneFlag(zone, ZoneFlags.NpcFreeze))
                npcNextTick.SetValue(entity, 999999999999f);
            //Puts("OnNpcEnterZone: {0}", entity.GetType());
        }

        private void OnNpcExitZone(Zone zone, BaseNPC entity)
        {
            HashSet<Zone> zones;
            if (npcZones.TryGetValue(entity, out zones))
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) npcZones.Remove(entity);
            }
            if (HasZoneFlag(zone, ZoneFlags.NpcFreeze))
                npcNextTick.SetValue(entity, Time.time);
            //Puts("OnNpcExitZone: {0}", entity.GetType());
        }
        private void OnBuildingEnterZone(Zone zone, BaseCombatEntity entity)
        {
            HashSet<Zone> zones;
            if (!buildingZones.TryGetValue(entity, out zones))
                buildingZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            if (HasZoneFlag(zone, ZoneFlags.NoStability))
            {
                var block = entity as BuildingBlock;
                if (block == null) return;
                block.grounded = true;
            }
            //Puts("OnBuildingEnterZone: {0}", entity.GetType());
        }

        private void OnBuildingExitZone(Zone zone, BaseCombatEntity entity)
        {
            HashSet<Zone> zones;
            if (buildingZones.TryGetValue(entity, out zones))
            {
                zone.OnEntityKill(entity);
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) buildingZones.Remove(entity);
            }
            if (HasZoneFlag(zone, ZoneFlags.NoStability))
            {
                var block = entity as BuildingBlock;
                if (block == null) return;
                var prefab = GameManager.server.FindPrefab(block.blockDefinition.fullName);
                block.grounded = prefab.GetComponent<BuildingBlock>()?.grounded ?? false;
            }
            //Puts("OnBuildingExitZone: {0}", entity.GetType());
        }

        private void OnOtherEnterZone(Zone zone, BaseEntity entity)
        {
            HashSet<Zone> zones;
            if (!otherZones.TryGetValue(entity, out zones))
                otherZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            if (entity is CollectibleEntity && HasZoneFlag(zone, ZoneFlags.NoGather))
            {
                var collectible = (CollectibleEntity) entity;
                collectible.itemList = null;
            }
            if (entity is WorldItem && HasZoneFlag(zone, ZoneFlags.NoPickup))
            {
                var worldItem = (WorldItem)entity;
                worldItem.allowPickup = false;
            }
            //Puts("OnOtherEnterZone: {0}", entity.GetType());
        }

        private void OnOtherExitZone(Zone zone, BaseEntity entity)
        {
            HashSet<Zone> zones;
            if (otherZones.TryGetValue(entity, out zones))
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) otherZones.Remove(entity);
            }
            var collectible = entity as CollectibleEntity;
            if (collectible != null && collectible.itemList == null)
                collectible.itemList = GameManager.server.FindPrefab(entity).GetComponent<CollectibleEntity>().itemList;
            var worldItem = entity as WorldItem;
            if (worldItem != null && !worldItem.allowPickup)
                worldItem.allowPickup = GameManager.server.FindPrefab(entity).GetComponent<WorldItem>().allowPickup;
            //Puts("OnOtherExitZone: {0}", entity.GetType());
        }

        private void OnZoneDestroy(Zone zone)
        {
            HashSet<Zone> zones;
            foreach (var key in playerZones.Keys.ToArray())
                if (playerZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnPlayerExitZone(zone, key);
            foreach (var key in buildingZones.Keys.ToArray())
                if (buildingZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnBuildingExitZone(zone, key);
            foreach (var key in npcZones.Keys.ToArray())
                if (npcZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnNpcExitZone(zone, key);
            foreach (var key in resourceZones.Keys.ToArray())
                if (resourceZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnResourceExitZone(zone, key);
            foreach (var key in otherZones.Keys.ToArray())
            {
                if (!otherZones.TryGetValue(key, out zones))
                {
                    Puts("Zone: {0} Entity: {1} ({2}) {3}", zone.Info.ID, key.GetType(), key.net?.ID, key.isDestroyed);
                    continue;
                }
                if (zones.Contains(zone))
                    OnOtherExitZone(zone, key);
            }
            //UpdateAllPlayers();
        }

        private static void EjectPlayer(Zone zone, BasePlayer player)
        {
            if (isAdmin(player) || zone.WhiteList.Contains(player.userID) || zone.KeepInList.Contains(player.userID)) return;
            var cachedDirection = player.transform.position - zone.transform.position;
            float dist;
            if (zone.Info.Size != Vector3.zero)
                dist = zone.Info.Size.x > zone.Info.Size.z ? zone.Info.Size.x : zone.Info.Size.z;
            else
                dist = zone.Info.radius;
            var newPos = zone.transform.position + (cachedDirection / cachedDirection.magnitude * (dist + 5f));
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.TransformChanged();
            player.SendNetworkUpdateImmediate();
        }

        private static void AttractPlayer(Zone zone, BasePlayer player)
        {
            var cachedDirection = player.transform.position - zone.transform.position;
            float dist;
            if (zone.Info.Size != Vector3.zero)
                dist = zone.Info.Size.x > zone.Info.Size.z ? zone.Info.Size.x : zone.Info.Size.z;
            else
                dist = zone.Info.radius;
            var newPos = zone.transform.position + (cachedDirection / cachedDirection.magnitude * (dist - 5f));
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.TransformChanged();
            player.SendNetworkUpdateImmediate();
        }

        private static bool isAdmin(BasePlayer player)
        {
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        private bool hasPermission(BasePlayer player, string permname)
        {
            return isAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
        }
        //////////////////////////////////////////////////////////////////////////////
        /// Chat Commands
        //////////////////////////////////////////////////////////////////////////////
        [ChatCommand("zone_add")]
        private void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var newzoneinfo = new ZoneDefinition(player.transform.position) { ID = UnityEngine.Random.Range(1, 99999999).ToString() };
            NewZone(newzoneinfo);
            if (ZoneDefinitions.ContainsKey(newzoneinfo.ID)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.ID]);
            ZoneDefinitions[newzoneinfo.ID] = newzoneinfo;
            LastZone[player.userID] = newzoneinfo.ID;
            storedData.ZoneDefinitions.Add(newzoneinfo);
            SaveData();
            ShowZone(player, newzoneinfo.ID);
            SendMessage(player, "New Zone created, you may now edit it: " + newzoneinfo.Location);
        }
        [ChatCommand("zone_reset")]
        private void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            ZoneDefinitions.Clear();
            storedData.ZoneDefinitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "All Zones were removed");
        }
        [ChatCommand("zone_remove")]
        private void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            if (args.Length == 0) { SendMessage(player, "/zone_remove XXXXXID"); return; }
            ZoneDefinition zoneDef;
            if (!ZoneDefinitions.TryGetValue(args[0], out zoneDef)) { SendMessage(player, "This zone doesn't exist"); return; }
            storedData.ZoneDefinitions.Remove(zoneDef);
            ZoneDefinitions.Remove(args[0]);
            SaveData();
            RefreshZone(args[0]);
            SendMessage(player, "Zone " + args[0] + " was removed");
        }
        [ChatCommand("zone_edit")]
        private void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            string zoneId;
            if (args.Length == 0)
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count != 1)
                {
                    SendMessage(player, "/zone_edit XXXXXID");
                    return;
                }
                zoneId = zones.First().Info.ID;
            }
            else
                zoneId = args[0];
            if (!ZoneDefinitions.ContainsKey(zoneId)) { SendMessage(player, "This zone doesn't exist"); return; }
            LastZone[player.userID] = zoneId;
            SendMessage(player, "Editing zone ID: " + zoneId);
            ShowZone(player, zoneId);
        }
        [ChatCommand("zone_player")]
        private void cmdChatZonePlayer(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var targetPlayer = player;
            if (args != null && args.Length > 0)
            {
                targetPlayer = FindPlayer(args[0]);
                if (targetPlayer == null)
                {
                    SendMessage(player, "Player not found");
                    return;
                }
            }
            ZoneFlags tags;
            playerTags.TryGetValue(targetPlayer, out tags);
            SendMessage(player, $"=== {targetPlayer.displayName} ===");
            SendMessage(player, $"Flags: {tags}");
            SendMessage(player, "========== Zone list ==========");
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(targetPlayer, out zones) || zones.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (var zone in zones)
                SendMessage(player, $"{zone.Info.ID} => {zone.Info.name} - {zone.Info.Location}");
            UpdateFlags(targetPlayer);
        }
        [ChatCommand("zone_list")]
        private void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            SendMessage(player, "========== Zone list ==========");
            if (ZoneDefinitions.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (var pair in ZoneDefinitions)
                SendMessage(player, $"{pair.Key} => {pair.Value.name} - {pair.Value.Location}");
        }
        [ChatCommand("zone")]
        private void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            string zoneId;
            if (!LastZone.TryGetValue(player.userID, out zoneId)) { SendMessage(player, "You must first say: /zone_edit XXXXXID"); return; }

            var zoneDefinition = ZoneDefinitions[zoneId];
            if (args.Length < 1)
            {
                SendMessage(player, "/zone option value/reset");
                SendMessage(player, $"name => {zoneDefinition.name}");
                SendMessage(player, $"enabled => {zoneDefinition.enabled}");
                SendMessage(player, $"ID => {zoneDefinition.ID}");
                SendMessage(player, $"radiation => {zoneDefinition.radiation}");
                SendMessage(player, $"radius => {zoneDefinition.radius}");
                SendMessage(player, $"Location => {zoneDefinition.Location}");
                SendMessage(player, $"Size => {zoneDefinition.Size}");
                SendMessage(player, $"Rotation => {zoneDefinition.Rotation}");
                SendMessage(player, $"enter_message => {zoneDefinition.enter_message}");
                SendMessage(player, $"leave_message => {zoneDefinition.leave_message}");
                SendMessage(player, $"flags => {zoneDefinition.flags}");

                //var values = Enum.GetValues(typeof(ZoneFlags));
                //foreach (var value in values)
                //    SendMessage(player, $"{Enum.GetName(typeof(ZoneFlags), value)} => {HasZoneFlag(zoneDefinition, (ZoneFlags)value)}");
                ShowZone(player, zoneId);
                return;
            }
            if (args.Length % 2 != 0) { SendMessage(player, "Value missing..."); return; }
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);
            SaveData();
            ShowZone(player, zoneId);
        }

        [ConsoleCommand("zone")]
        private void ccmdZone(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!hasPermission(player, PermZone)) { SendMessage(player, "You don't have access to this command"); return; }
            var zoneId = arg.GetString(0);
            ZoneDefinition zoneDefinition;
            if (!arg.HasArgs(3) || !ZoneDefinitions.TryGetValue(zoneId, out zoneDefinition)) { SendMessage(player, "Zone Id not found or Too few arguments: zone <zoneid> <arg> <value>"); return; }

            var args = new string[arg.Args.Length - 1];
            Array.Copy(arg.Args, 1, args, 0, args.Length);
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);
            //SaveData();
            //ShowZone(player, zoneId);
        }

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0) message = string.Format(message, args);
                SendReply(player, $"{prefix}{message}");
            }
            else
                Puts(message);
        }

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
    }
}
