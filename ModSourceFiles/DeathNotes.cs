﻿using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core;
using System;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Death Notes", "LaserHydra", "5.1.0", ResourceId = 819)]
    [Description("Broadcast deaths with many details")]
    class DeathNotes : RustPlugin
    {
        bool debug = false;
        bool killReproducing = false;

        Dictionary<ulong, bool> canRead = new Dictionary<ulong, bool>();
        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();

        Dictionary<string, string> reproduceableKills = new Dictionary<string, string>();

        Plugin PopupNotifications;

        #region Classes

        class Attacker
        {
            public string name = string.Empty;
            [JsonIgnore]
            public BaseCombatEntity entity = new BaseCombatEntity();
            public AttackerType type = AttackerType.Invalid;

            public string TryGetName()
            {
                if (type == AttackerType.Player)
                    return entity?.ToPlayer().displayName;
                else if (type == AttackerType.Helicopter)
                    return "Patrol Helicopter";
                else if (type == AttackerType.Turret)
                    return "Auto Turret";
                else if (type == AttackerType.Self)
                    return "himself";
                else if (type == AttackerType.Animal)
                {
                    if ((bool)entity?.name?.Contains("boar"))
                        return "Boar";
                    else if ((bool)entity?.name?.Contains("horse"))
                        return "Horse";
                    else if ((bool)entity?.name?.Contains("wolf"))
                        return "Wolf";
                    else if ((bool)entity?.name?.Contains("stag"))
                        return "Stag";
                    else if ((bool)entity?.name?.Contains("chicken"))
                        return "Chicken";
                    else if ((bool)entity?.name?.Contains("bear"))
                        return "Bear";
                }
                else if (type == AttackerType.Structure)
                {
                    if ((bool)entity?.name?.Contains("barricade.wood.prefab"))
                        return "Wooden Barricade";
                    else if ((bool)entity?.name?.Contains("barricade.woodwire.prefab"))
                        return "Barbed Wooden Barricade";
                    else if ((bool)entity?.name?.Contains("barricade.metal.prefab"))
                        return "Metal Barricade";
                    else if ((bool)entity?.name?.Contains("wall.external.high.wood.prefab"))
                        return "High External Wooden Wall";
                    else if ((bool)entity?.name?.Contains("wall.external.high.stone.prefab"))
                        return "High External Stone Wall";
                    else if ((bool)entity?.name?.Contains("gate.external.high.wood.prefab"))
                        return "High External Wooden Gate";
                    else if ((bool)entity?.name?.Contains("gate.external.high.wood.prefab"))
                        return "High External Stone Gate";
                }
                else if (type == AttackerType.Trap)
                {
                    if ((bool)entity?.name?.Contains("beartrap.prefab"))
                        return "Snap Trap";
                    else if ((bool)entity?.name?.Contains("landmine.prefab"))
                        return "Land Mine";
                    else if ((bool)entity?.name?.Contains("spikes.floor.prefab"))
                        return "Wooden Floor Spikes";
                }

                return "No Attacker";
            }

            public AttackerType TryGetType()
            {
                if (entity == null)
                    return AttackerType.Invalid;
                else if (entity?.ToPlayer() != null)
                    return AttackerType.Player;
                else if ((bool)entity?.name?.Contains("patrolhelicopter.prefab"))
                    return AttackerType.Helicopter;
                else if ((bool)entity?.name?.Contains("animals/"))
                    return AttackerType.Animal;
                else if ((bool)entity?.name?.Contains("barricades/") || (bool)entity?.name?.Contains("wall.external.high"))
                    return AttackerType.Structure;
                else if ((bool)entity?.name?.Contains("beartrap.prefab") || (bool)entity?.name?.Contains("landmine.prefab") || (bool)entity?.name?.Contains("spikes.floor.prefab"))
                    return AttackerType.Trap;
                else if ((bool)entity?.name?.Contains("autoturret_deployed.prefab"))
                    return AttackerType.Turret;

                return AttackerType.Invalid;
            }
        }

        class Victim
        {
            public string name = string.Empty;
            [JsonIgnore]
            public BaseCombatEntity entity = new BaseCombatEntity();
            public VictimType type = VictimType.Invalid;

            public string TryGetName()
            {
                if (type == VictimType.Player)
                    return entity?.ToPlayer().displayName;
                else if (type == VictimType.Helicopter)
                    return "Patrol Helicopter";
                else if (type == VictimType.Animal)
                {
                    if ((bool)entity?.name?.Contains("boar"))
                        return "Boar";
                    else if ((bool)entity?.name?.Contains("horse"))
                        return "Horse";
                    else if ((bool)entity?.name?.Contains("wolf"))
                        return "Wolf";
                    else if ((bool)entity?.name?.Contains("stag"))
                        return "Stag";
                    else if ((bool)entity?.name?.Contains("chicken"))
                        return "Chicken";
                    else if ((bool)entity?.name?.Contains("bear"))
                        return "Bear";

                }

                return "No Victim";
            }

            public VictimType TryGetType()
            {
                if (entity?.ToPlayer() != null)
                    return VictimType.Player;
                else if ((bool)entity?.name?.Contains("patrolhelicopter.prefab"))
                    return VictimType.Helicopter;
                else if ((bool)entity?.name?.Contains("animals/"))
                    return VictimType.Animal;

                return VictimType.Invalid;
            }
        }

        class DeathData
        {
            public Victim victim = new Victim();
            public Attacker attacker = new Attacker();
            public DeathReason reason = DeathReason.Unknown;
            public string damageType = string.Empty;
            public string weapon = string.Empty;
            public List<string> attachments = new List<string>();
            public string bodypart = string.Empty;
            internal float _distance = -1f;

            public float distance
            {
                get
                {
                    try
                    {
                        if (_distance != -1)
                            return _distance;

                        foreach (string death in new List<string> { "Cold", "Drowned", "Heat", "Suicide", "Generic", "Posion", "Radiation", "Thirst", "Hunger", "Fall" })
                        {
                            if (reason == GetDeathReason(death))
                                attacker.entity = victim.entity;
                        }

                        return victim.entity.Distance(attacker.entity.transform.position);
                    }
                    catch(Exception)
                    {
                        return 0f;
                    }
                }
            }

            public DeathReason TryGetReason()
            {
                if (victim.type == VictimType.Helicopter)
                    return DeathReason.HelicopterDeath;
                else if (attacker.type == AttackerType.Helicopter)
                    return DeathReason.Helicopter;
                else if (attacker.type == AttackerType.Turret)
                    return DeathReason.Turret;
                else if (attacker.type == AttackerType.Trap)
                    return DeathReason.Trap;
                else if (attacker.type == AttackerType.Structure)
                    return DeathReason.Structure;
                else if (attacker.type == AttackerType.Animal)
                    return DeathReason.Animal;
                else if (victim.type == VictimType.Animal)
                    return DeathReason.AnimalDeath;
                else if (weapon == "F1 Grenade")
                    return DeathReason.Explosion;
                else if (victim.type == VictimType.Player)
                    return GetDeathReason(damageType);

                return DeathReason.Unknown;
            }

            public DeathReason GetDeathReason(string damage)
            {
                List<DeathReason> Reason = (from DeathReason current in Enum.GetValues(typeof(DeathReason)) where current.ToString() == damage select current).ToList();

                if (Reason.Count == 0)
                    return DeathReason.Unknown;

                return Reason[0];
            }

            [JsonIgnore]
            internal string JSON
            {
                get
                {
                    return JsonConvert.SerializeObject(this, Formatting.Indented);
                }
            }
            
            internal static DeathData Get(object obj)
            {
                JObject jobj = (JObject) obj;
                DeathData data = new DeathData();

                data.bodypart = jobj["bodypart"].ToString();
                data.weapon = jobj["weapon"].ToString();
                data.attachments = (from attachment in jobj["attachments"] select attachment.ToString()).ToList();
                data._distance = Convert.ToSingle(jobj["distance"]);

                /// Victim
                data.victim.name = jobj["victim"]["name"].ToString();

                List<VictimType> victypes = (from VictimType current in Enum.GetValues(typeof(VictimType)) where current.GetHashCode().ToString() == jobj["victim"]["type"].ToString() select current).ToList();

                if (victypes.Count != 0)
                    data.victim.type = victypes[0];

                /// Attacker
                data.attacker.name = jobj["attacker"]["name"].ToString();

                List<AttackerType> attackertypes = (from AttackerType current in Enum.GetValues(typeof(AttackerType)) where current.GetHashCode().ToString() == jobj["attacker"]["type"].ToString() select current).ToList();

                if (attackertypes.Count != 0)
                    data.attacker.type = attackertypes[0];
                
                /// Reason
                List<DeathReason> reasons = (from DeathReason current in Enum.GetValues(typeof(DeathReason)) where current.GetHashCode().ToString() == jobj["reason"].ToString() select current).ToList();
                if (reasons.Count != 0)
                    data.reason = reasons[0];

                return data;
            }
        }

        #endregion

        #region Enums / Types

        enum VictimType
        {
            Player,
            Helicopter,
            Animal,
            Invalid
        }

        enum AttackerType
        {
            Player,
            Helicopter,
            Animal,
            Turret,
            Structure,
            Trap,
            Self,
            Invalid
        }

        enum DeathReason
        {
            Turret,
            Helicopter,
            HelicopterDeath,
            Structure,
            Trap,
            Animal,
            AnimalDeath,
            Generic,
            Hunger,
            Thirst,
            Cold,
            Drowned,
            Heat,
            Bleeding,
            Poison,
            Suicide,
            Bullet,
            Slash,
            Blunt,
            Fall,
            Radiation,
            Stab,
            Explosion,
            Unknown
        }

        #endregion

        #region General Plugin Hooks

        void Loaded()
        {
#if !RUST
            throw new NotSupportedException("This plugin or the version of this plugin does not support this game!");
#endif
            if (killReproducing)
                RegisterPerm("reproduce");

            RegisterPerm("see");

            LoadConfig();
            LoadData();
            LoadMessages();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (!canRead.ContainsKey(player.userID))
                {
                    canRead.Add(player.userID, true);

                    SaveData();
                }

            PopupNotifications = (Plugin)plugins.Find("PopupNotifications");

            if (PopupNotifications == null && GetConfig(false, "Settings", "Use Popup Notifications"))
                PrintWarning("You have set 'Use Popup Notifications' to true, but the Popup Notifications plugin is not installed. Popups will not work without it. Get it here: http://oxidemod.org/plugins/1252/");

        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new config file...");
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!canRead.ContainsKey(player.userID))
            {
                canRead.Add(player.userID, true);
                SaveData();
            }
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "Popup Notifications")
                PopupNotifications = (Plugin) plugin;
        }

        #endregion

        #region Loading

        void LoadData()
        {
            canRead = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>("DeathNotes");

            if (killReproducing)
                reproduceableKills = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>("DeathNotes_KillReproducing");
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("DeathNotes", canRead);

            if (killReproducing)
                Interface.Oxide.DataFileSystem.WriteObject("DeathNotes_KillReproducing", reproduceableKills);
        }

        void LoadConfig()
        {
            SetConfig("Settings", "Chat Icon (SteamID)", "76561198077847390");

            SetConfig("Settings", "Message Radius Enabled", false);
            SetConfig("Settings", "Message Radius", 300f);

            SetConfig("Settings", "Log to File", false);
            SetConfig("Settings", "Write to Console", true);
            SetConfig("Settings", "Use Popup Notifications", false);

            SetConfig("Settings", "Enable Showdeaths Command", true);

            SetConfig("Settings", "Needs Permission", false);

            SetConfig("Settings", "Title", "Death Notes");
            SetConfig("Settings", "Formatting", "[{Title}]: {Message}");
            SetConfig("Settings", "Console Formatting", "{Message}");

            SetConfig("Settings", "Attachments Split", " | ");
            SetConfig("Settings", "Attachments Formatting", " ({attachments})");

            SetConfig("Settings", "Title Color", "#80D000");
            SetConfig("Settings", "Victim Color", "#C4FF00");
            SetConfig("Settings", "Attacker Color", "#C4FF00");
            SetConfig("Settings", "Weapon Color", "#C4FF00");
            SetConfig("Settings", "Attachments Color", "#C4FF00");
            SetConfig("Settings", "Distance Color", "#C4FF00");
            SetConfig("Settings", "Bodypart Color", "#C4FF00");
            SetConfig("Settings", "Message Color", "#696969");
            
            SetConfig("Names", new Dictionary<string, object> { });
            SetConfig("Bodyparts", new Dictionary<string, object> { });
            SetConfig("Weapons", new Dictionary<string, object> { });
            SetConfig("Attachments", new Dictionary<string, object> { });

            SetConfig("Messages", "Bleeding", new List<string> { "{victim} bled out." });
            SetConfig("Messages", "Blunt", new List<string> { "{attacker} used a {weapon} to knock {victim} out." });
            SetConfig("Messages", "Bullet", new List<string> { "{victim} was shot in the {bodypart} by {attacker} with a {weapon}{attachments} from {distance}m." });
            SetConfig("Messages", "Cold", new List<string> { "{victim} became an iceblock." });
            SetConfig("Messages", "Drowned", new List<string> { "{victim} tried to swim." });
            SetConfig("Messages", "Explosion", new List<string> { "{victim} was shredded by {attacker}'s {weapon}" });
            SetConfig("Messages", "Fall", new List<string> { "{victim} did a header into the ground." });
            SetConfig("Messages", "Generic", new List<string> { "The death took {victim} with him." });
            SetConfig("Messages", "Heat", new List<string> { "{victim} burned to ashes." });
            SetConfig("Messages", "Helicopter", new List<string> { "{victim} was shot to pieces by a {attacker}." });
            SetConfig("Messages", "HelicopterDeath", new List<string> { "The {victim} was taken down." });
            SetConfig("Messages", "Animal", new List<string> { "A {attacker} followed {victim} until it finally caught him." });
            SetConfig("Messages", "AnimalDeath", new List<string> { "{attacker} killed a {victim} with a {weapon}{attachments} from {distance}m." });
            SetConfig("Messages", "Hunger", new List<string> { "{victim} forgot to eat." });
            SetConfig("Messages", "Poison", new List<string> { "{victim} died after being poisoned." });
            SetConfig("Messages", "Radiation", new List<string> { "{victim} became a bit too radioactive." });
            SetConfig("Messages", "Slash", new List<string> { "{attacker} slashed {victim} in half." });
            SetConfig("Messages", "Stab", new List<string> { "{victim} was stabbed to death by {attacker} using a {weapon}." });
            SetConfig("Messages", "Structure", new List<string> { "A {attacker} impaled {victim}." });
            SetConfig("Messages", "Suicide", new List<string> { "{victim} had enough of life." });
            SetConfig("Messages", "Thirst", new List<string> { "{victim} dried internally." });
            SetConfig("Messages", "Trap", new List<string> { "{victim} ran into a {attacker}" });
            SetConfig("Messages", "Turret", new List<string> { "A {attacker} defended its home against {victim}." });
            SetConfig("Messages", "Unknown", new List<string> { "{victim} died. Nobody knows why, it just happened." });
            
            SetConfig("Messages", "Blunt Sleeping", new List<string> { "{attacker} used a {weapon} to turn {victim}'s dream into a nightmare." });
            SetConfig("Messages", "Bullet Sleeping", new List<string> { "Sleeping {victim} was shot in the {bodypart} by {attacker} with a {weapon}{attachments} from {distance}m." });
            SetConfig("Messages", "Explosion Sleeping", new List<string> { "{victim} was shredded by {attacker}'s {weapon} while sleeping." });
            SetConfig("Messages", "Generic Sleeping", new List<string> { "The death took sleeping {victim} with him." });
            SetConfig("Messages", "Helicopter Sleeping", new List<string> { "{victim} was sleeping when he was shot to pieces by a {attacker}." });
            SetConfig("Messages", "Animal Sleeping", new List<string> { "{victim} was killed by a {attacker} while having a sleep." });
            SetConfig("Messages", "Slash Sleeping", new List<string> { "{attacker} slashed sleeping {victim} in half." });
            SetConfig("Messages", "Stab Sleeping", new List<string> { "{victim} was stabbed to death by {attacker} using a {weapon} before he could even awake." });
            SetConfig("Messages", "Unknown Sleeping", new List<string> { "{victim} was sleeping when he died. Nobody knows why, it just happened." });

            SaveConfig();
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have permission to use this command."},
                {"Hidden", "You do no longer see death messages."},
                {"Unhidden", "You will now see death messages."}
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("showdeaths")]
        void cmdShowDeaths(BasePlayer player, string cmd, string[] args)
        {
            if(!HasPerm(player.userID, "see"))
            {
                SendChatMessage(player, GetMsg("No Permission", player.userID));
                return;
            }

            if (canRead.ContainsKey(player.userID))
            {
                if (canRead[player.userID])
                {
                    canRead[player.userID] = false;
                    SendChatMessage(player, GetMsg("Hidden", player.userID));
                }
                else
                {
                    canRead[player.userID] = true;
                    SendChatMessage(player, GetMsg("Unhidden", player.userID));
                }
            }
            else
            {
                canRead.Add(player.userID, true);
                SendChatMessage(player, GetMsg("Unhidden", player.userID));
            }

            SaveData();
        }

        [ChatCommand("deathnotes")]
        void cmdGetInfo(BasePlayer player)
        {
            GetInfo(player);
        }

        [ConsoleCommand("reproducekill")]
        void ccmdReproduceKill(ConsoleSystem.Arg arg)
        {
            bool hasPerm = false;

            if (arg?.connection == null)
                hasPerm = true;
            else
            {
                if((BasePlayer)arg.connection.player != null)
                {
                    if (HasPerm(arg.connection.userid, "reproduce"))
                        hasPerm = true;
                }
            }
            
            if (hasPerm)
            {
                if (arg.Args == null || arg.Args.Length != 1)
                {
                    arg.ReplyWith("Syntax: reproducekill <datetime>");
                    return;
                }
                
                if(reproduceableKills.ContainsKey(arg.Args[0]))
                {
                    DeathData data = DeathData.Get(JsonConvert.DeserializeObject(reproduceableKills[arg.Args[0]]));
                    PrintWarning("Reproduced Kill: " + Environment.NewLine + data.JSON);

                    if (data == null)
                        return;

                    NoticeDeath(data, true);
                    arg.ReplyWith("Death reproduced!");
                }
                else
                    arg.ReplyWith("No saved kill at that time found!");
            }
        }

        #endregion

        #region DeathNotes Information

        void GetInfo(BasePlayer player)
        {
            webrequest.EnqueueGet("http://oxidemod.org/plugins/819/", (code, response) => {
                if(code != 200)
                {
                    PrintWarning("Failed to get information!");
                    return;
                }

                string version_published = "0.0.0";
                string version_installed = this.Version.ToString();

                Match version = new Regex(@"<h3>Version (\d{1,2}\.\d{1,2}(\.\d{1,2})?)<\/h3>").Match(response);
                if(version.Success)
                {
                    version_published = version.Groups[1].ToString();
                }

                SendChatMessage(player, $"<size=25><color=#C4FF00>DeathNotes</color></size> <size=20><color=#696969>by LaserHydra</color>{Environment.NewLine}<color=#696969>Latest <color=#C4FF00>{version_published}</color>{Environment.NewLine}Installed <color=#C4FF00>{version_installed}</color></color></size>");
            }, this);
        }

        #endregion

        #region Death Related

        HitInfo TryGetLastWounded(ulong uid, HitInfo info)
        {
            if (LastWounded.ContainsKey(uid))
            {
                HitInfo output = LastWounded[uid];
                LastWounded.Remove(uid);
                return output;
            }

            return info;
        }

        void OnEntityTakeDamage(BaseCombatEntity victim, HitInfo info)
        {
            if(victim != null && victim.ToPlayer() != null && info != null && info?.Initiator != null && info?.Initiator?.ToPlayer() != null)
            {
                NextTick(() => 
                {
                    if (victim.ToPlayer().IsWounded())
                        LastWounded[victim.ToPlayer().userID] = info;
                });
            }
        }

        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim == null)
                return;

            if(victim.ToPlayer() != null)
            {
                if (victim.ToPlayer().IsWounded())
                    info = TryGetLastWounded(victim.ToPlayer().userID, info);
            }

            if (info?.Initiator?.ToPlayer() == null && (bool)victim?.name?.Contains("autospawn"))
                return;

            DeathData data = new DeathData();
            data.victim.entity = victim;
            data.victim.type = data.victim.TryGetType();

            if (data.victim.type == VictimType.Invalid)
                return;

            data.victim.name = data.victim.TryGetName();

            if (info?.Initiator != null)
                data.attacker.entity = info?.Initiator as BaseCombatEntity;
            else
                data.attacker.entity = victim.lastAttacker as BaseCombatEntity;

            data.attacker.type = data.attacker.TryGetType();
            data.attacker.name = data.attacker.TryGetName();
            data.weapon = info?.Weapon?.GetItem()?.info?.displayName?.english ?? FormatThrownWeapon(info?.WeaponPrefab?.name ?? "No Weapon");
            data.attachments = GetAttachments(info);
            data.damageType = FirstUpper(victim.lastDamage.ToString());

            if(data.weapon == "Heli Rocket")
            {
                data.attacker.name = "Patrol Helicopter";
                data.reason = DeathReason.Helicopter;
            }

            if (info?.HitBone != null)
                data.bodypart = FirstUpper(GetBoneName(victim, ((uint)info?.HitBone)) ?? string.Empty);
            else
                data.bodypart = FirstUpper("Body") ?? string.Empty;

            data.reason = data.TryGetReason();

            NoticeDeath(data);
        }

        void NoticeDeath(DeathData data, bool reproduced = false)
        {
            DeathData newData = UpdateData(data);

            if (string.IsNullOrEmpty(GetDeathMessage(newData, false)))
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (InRadius(player, data.attacker.entity) && CanSee(player))
                    SendChatMessage(player, GetDeathMessage(newData, false), null, GetConfig("76561198077847390", "Settings", "Chat Icon (SteamID)"));
            }

            if (GetConfig(true, "Settings", "Write to Console"))
                Puts(StripTags(GetDeathMessage(newData, true)));

            if (GetConfig(false, "Settings", "Log to File"))
                ConVar.Server.Log("oxide/logs/Kills.txt", StripTags(GetDeathMessage(newData, true)));

            if (GetConfig(false, "Settings", "Use Popup Notifications") && PopupNotifications != null)
                PopupMessage(GetDeathMessage(newData, false));

            if (debug)
            {
                PrintWarning("DATA: " + Environment.NewLine + data.JSON);
                PrintWarning("UPDATED DATA: " + Environment.NewLine + newData.JSON);
            }

            if (killReproducing && !reproduced)
            {
                reproduceableKills.Add(DateTime.Now.ToString(), data.JSON.Replace(Environment.NewLine, ""));
                SaveData();
            }
        }

        #endregion

        #region Formatting

        string FormatThrownWeapon(string unformatted)
        {
            if (unformatted == string.Empty)
                return string.Empty;

            string formatted = FirstUpper(unformatted.Split('/').Last().Replace(".prefab", "").Replace(".entity", "").Replace(".weapon", "").Replace("_", " "));

            if (formatted == "Stonehatchet")
                formatted = "Stone Hatchet";
            else if (formatted == "Knife Bone")
                formatted = "Bone Knife";
            else if (formatted == "Spear Wooden")
                formatted = "Wooden Spear";
            else if (formatted == "Spear Stone")
                formatted = "Stone Spear";
            else if (formatted == "Icepick Salvaged")
                formatted = "Salvaged Icepick";
            else if (formatted == "Axe Salvaged")
                formatted = "Salvaged Axe";
            else if (formatted == "Hammer Salvaged")
                formatted = "Salvaged Hammer";

            return formatted;
        }

        string StripTags(string original)
        {
            List<string> regexTags = new List<string>
            {
                @"<color=.+?>",
                @"<size=.+?>"
            };

            List<string> tags = new List<string>
            {
                "</color>",
                "</size>",
                "<i>",
                "</i>",
                "<b>",
                "</b>"
            };

            foreach (string tag in tags)
                original = original.Replace(tag, "");

            foreach (string regexTag in regexTags)
                original = new Regex(regexTag).Replace(original, "");

            return original;
        }

        string FirstUpper(string original)
        {
            if (original == string.Empty)
                return string.Empty;

            List<string> output = new List<string>();
            foreach (string word in original.Split(' '))
                output.Add(word.Substring(0, 1).ToUpper() + word.Substring(1, word.Length - 1));

            return ListToString(output, 0, " ");
        }

        #endregion

        #region Death Variables Methods

        List<string> GetAttachments(HitInfo info)
        {
            List<string> attachments = new List<string>();

            if (info?.Weapon?.GetItem()?.contents?.itemList != null)
            {
                foreach (var content in info?.Weapon?.GetItem().contents?.itemList as List<Item>)
                {
                    attachments.Add(content?.info?.displayName?.english);
                }
            }

            return attachments;
        }

        string GetBoneName(BaseCombatEntity entity, uint boneId) => entity?.skeletonProperties?.FindBone(boneId)?.name?.english ?? "Body";

        bool InRadius(BasePlayer player, BaseCombatEntity attacker)
        {
            if (GetConfig(false, "Settings", "Message Radius Enabled"))
            {
                try
                {
                    if (player.Distance(attacker) <= GetConfig(300f, "Settings", "Message Radius"))
                        return true;
                    else
                        return false;
                }
                catch(Exception)
                {
                    return false;
                }
            }

            return true;
        }

        string GetDeathMessage(DeathData data, bool console)
        {
            List<DeathReason> SleepingDeaths = new List<DeathReason>
            {
                DeathReason.Animal,
                DeathReason.Blunt,
                DeathReason.Bullet,
                DeathReason.Explosion,
                DeathReason.Generic,
                DeathReason.Helicopter,
                DeathReason.Slash,
                DeathReason.Stab,
                DeathReason.Unknown
            };

            string message = string.Empty;
            string reason = string.Empty;
            List<string> messages = new List<string>();

            if (data.victim.type == VictimType.Player && data.victim.entity != null && data.victim.entity.ToPlayer() != null && data.victim.entity.ToPlayer().IsSleeping())
            {
                if(SleepingDeaths.Contains(data.reason))
                {
                    reason = data.reason.ToString() + " Sleeping";
                }
                else
                    reason = data.reason.ToString();
            }
            else
                reason = data.reason.ToString();

            try
            {
                messages = GetConfig(new List<string>(), "Messages", reason);
            }
            catch (InvalidCastException)
            {
                messages = (from msg in GetConfig(new List<object>(), "Messages", reason) select msg.ToString()).ToList();
            }

            if (messages.Count == 0)
                return message;

            string attachmentsString = data.attachments.Count == 0 ? string.Empty : GetConfig(" ({attachments})", "Settings", "Attachments Formatting").Replace("{attachments}", ListToString(data.attachments, 0, GetConfig(" | ", "Settings", "Attachments Split")));

            if (console)
                message = GetConfig("{Message}", "Settings", "Console Formatting").Replace("{Title}", $"<color={GetConfig("#80D000", "Settings", "Title Color")}>{GetConfig("Death Notes", "Settings", "Title")}</color>").Replace("{Message}", $"<color={GetConfig("#696969", "Settings", "Message Color")}>{messages[UnityEngine.Random.Range(0, messages.Count - 1)].ToString()}</color>");
            else
                message = GetConfig("[{Title}]: {Message}", "Settings", "Formatting").Replace("{Title}", $"<color={GetConfig("#80D000", "Settings", "Title Color")}>{GetConfig("Death Notes", "Settings", "Title")}</color>").Replace("{Message}", $"<color={GetConfig("#696969", "Settings", "Message Color")}>{messages[UnityEngine.Random.Range(0, messages.Count - 1)].ToString()}</color>");
            
            message = message.Replace("{attacker}", $"<color={GetConfig("#C4FF00", "Settings", "Attacker Color")}>{data.attacker.name}</color>");
            message = message.Replace("{victim}", $"<color={GetConfig("#C4FF00", "Settings", "Victim Color")}>{data.victim.name}</color>");
            message = message.Replace("{distance}", $"<color={GetConfig("#C4FF00", "Settings", "Distance Color")}>{Math.Round(data.distance, 2).ToString()}</color>");
            message = message.Replace("{weapon}", $"<color={GetConfig("#C4FF00", "Settings", "Weapon Color")}>{data.weapon}</color>");
            message = message.Replace("{bodypart}", $"<color={GetConfig("#C4FF00", "Settings", "Bodypart Color")}>{data.bodypart}</color>");
            message = message.Replace("{attachments}", $"<color={GetConfig("#C4FF00", "Settings", "Attachments Color")}>{attachmentsString}</color>");

            return message;
        }

        DeathData UpdateData(DeathData data)
        {
            bool configUpdated = false;

            if (data.victim.type != VictimType.Player)
            {
                if (Config.Get("Names", data.victim.name) == null)
                {
                    SetConfig("Names", data.victim.name, data.victim.name);
                    configUpdated = true;
                }
                else
                    data.victim.name = GetConfig(data.victim.name, "Names", data.victim.name);
            }

            if (data.attacker.type != AttackerType.Player)
            {
                if (Config.Get("Names", data.attacker.name) == null)
                {
                    SetConfig("Names", data.attacker.name, data.attacker.name);
                    configUpdated = true;
                }
                else
                    data.attacker.name = GetConfig(data.attacker.name, "Names", data.attacker.name);
            }

            if (Config.Get("Bodyparts", data.bodypart) == null)
            {
                SetConfig("Bodyparts", data.bodypart, data.bodypart);
                configUpdated = true;
            }
            else
                data.bodypart = GetConfig(data.bodypart, "Bodyparts", data.bodypart);

            if (Config.Get("Weapons", data.weapon) == null)
            {
                SetConfig("Weapons", data.weapon, data.weapon);
                configUpdated = true;
            }
            else
                data.weapon = GetConfig(data.weapon, "Weapons", data.weapon);

            string[] attachmentsCopy = new string[data.attachments.Count];
            data.attachments.CopyTo(attachmentsCopy);

            foreach (string attachment in attachmentsCopy)
            {
                if (Config.Get("Attachments", attachment) == null)
                {
                    SetConfig("Attachments", attachment, attachment);
                    configUpdated = true;
                }
                else
                {
                    data.attachments.Remove(attachment);
                    data.attachments.Add(GetConfig(attachment, "Attachments", attachment));
                }
            }

            if (configUpdated)
                SaveConfig();

            return data;
        }

        bool CanSee(BasePlayer player)
        {
            if (!GetConfig(false, "Settings", "Needs Permission"))
            {
                if (!GetConfig(true, "Settings", "Enable Showdeaths Command"))
                    return true;
                else
                    return canRead.ContainsKey(player.userID) ? canRead[player.userID] : true;
            }
            else
            {
                if(HasPerm(player.userID, "see"))
                {
                    if (!GetConfig(true, "Settings", "Enable Showdeaths Command"))
                        return true;
                    else
                        return canRead.ContainsKey(player.userID) ? canRead[player.userID] : true;
                }
            }

            return false;
        }

        #endregion

        #region Converting

        string ListToString(List<string> list, int first, string seperator) => string.Join(seperator, list.Skip(first).ToArray());

        #endregion

        #region Config and Message Handling

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        string GetMsg(string key, object userID = null)
        {
            return lang.GetMessage(key, this, userID.ToString());
        }

        #endregion

        #region Permission Handling

        void RegisterPerm(params string[] permArray)
        {
            string perm = ListToString(permArray.ToList(), 0, ".");

            permission.RegisterPermission($"{PermissionPrefix}.{perm}", this);
        }

        bool HasPerm(object uid, params string[] permArray)
        {
            uid = uid.ToString();
            string perm = ListToString(permArray.ToList(), 0, ".");

            return permission.UserHasPermission(uid.ToString(), $"{PermissionPrefix}.{perm}");
        }

        string PermissionPrefix
        {
            get
            {
                return this.Title.Replace(" ", "").ToLower();
            }
        }

        #endregion

        #region Messages

        void BroadcastChat(string prefix, string msg = null) => rust.BroadcastChat(msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg);

        void SendChatMessage(BasePlayer player, string prefix, string msg = null, object uid = null) => rust.SendChatMessage(player, msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg, null, uid?.ToString() ?? "0");

        void PopupMessage(string message) => PopupNotifications?.Call("CreatePopupNotification", message);

        #endregion
    }
}
