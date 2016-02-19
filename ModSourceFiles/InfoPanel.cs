using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using UnityEngine;

using Newtonsoft.Json;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("InfoPanel", "Ghosst", "0.9.1", ResourceId = 1356)]
    [Description("A little panel with useful informations.")]
    public class InfoPanel : RustPlugin
    {
        #region DefaultConfigs
        private static string DefaultFontColor = "1 1 1 1";
        #endregion

        private Timer TestTimer;

        private Dictionary<string, Dictionary<string, IPanel>> PlayerPanels = new Dictionary<string, Dictionary<string, IPanel>>();
        private Dictionary<string, Dictionary<string, IPanel>> PlayerDockPanels = new Dictionary<string, Dictionary<string, IPanel>>();

        private Dictionary<string, List<string>> LoadedPluginPanels = new Dictionary<string, List<string>>();

        #region DefaultConfig

        private static PluginConfig Settings;

        private List<string> TimeFormats = new List<string>
        {
            {"H:mm"},
            {"HH:mm"},
            {"h:mm"},
            {"h:mm tt"},
        };

        PluginConfig DefaultConfig()
        {
            var DefaultConfig = new PluginConfig
            {
                ThirdPartyPanels = new Dictionary<string, Dictionary<string, PanelConfig>>(),

                Messages = Messages,
                TimeFormats = TimeFormats,
                CompassDirections = new Dictionary<string, string>
                {
                    {"n","North"},
                    {"ne","Northeast"},
                    {"e","East"},
                    {"se","Southeast"},
                    {"s","South"},
                    {"sw","Southwest"},
                    {"w","West"},
                    {"nw","Northwest"},
                }
                ,
                Docks = new Dictionary<string, DockConfig>
                {
                    { "BottomDock", new DockConfig
                        {
                            Available = true,
                            Width = 0.99f,
                            Height = 0.030f,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0.005 0.005 0.005 0.005",
                            BackgroundColor = "0 0 0 0.4",
                        }
                    },
                    { "TopDock", new DockConfig
                        {
                            Available = true,
                            Width = 0.99f,
                            Height = 0.030f,
                            AnchorX = "Left",
                            AnchorY = "Top",
                            Margin = "0.005 0.005 0.005 0.005",
                            BackgroundColor = "0 0 0 0.4",
                        }
                    },
                },

                Panels = new Dictionary<string, PanelConfig>
                {
                    {"Clock", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 1,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0",
                            Width = 0.045f,
                            Height = 0.95f,
                            BackgroundColor = "0.1 0.1 0.1 0",
                            Text = new PanelTextConfig
                            {
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 14,
                                Margin = "0 0.01 0 0.01",
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "ClockUpdateFrequency (seconds)" ,ClockUpdateFrequency },
                                { "TimeFormat", "HH:mm" }
                            }
                        }
                    },
                    { "MessageBox", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 7,
                            AnchorX = "Right",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.3f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4",
                            Text = new PanelTextConfig
                            {
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 14,
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "MessageUpdateFrequency (seconds)", MessageUpdateFrequency },
                                { "MsgOrder","normal" }
                            }
                        }
                    },
                    { "Coordinates", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 7,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.095f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4" ,
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.13f,
                                Height = 0.8f,
                                Margin = "0 0.01 0.1 0.01",
                                Url = "http://i.imgur.com/Kr1pQ5b.png",
                            },
                            Text = new PanelTextConfig
                            {
                                Order =  2,
                                Width = 0.848f,
                                Height = 1f,
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 12,
                                Margin = "0 0.02 0 0",
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "RefreshRate(s)", "3" },
                            }
                        }
                    },
                    { "Compass", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 8,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.07f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4" ,
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.188f,
                                Height = 0.8f,
                                Margin = "0 0.01 0.1 0.03",
                                Url = "http://i.imgur.com/dG5nOOJ.png",
                            },
                            Text = new PanelTextConfig
                            {
                                Order =  2,
                                Width = 0.76f,
                                Height = 1f,
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 12,
                                Margin = "0 0.02 0 0",
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "RefreshRate(s)", "1" },
                                { "TextOrAngle", "text" }
                            }
                        }
                    },
                    { "OPlayers", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 2,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.07f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4" ,
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.17f,
                                Height = 0.8f,
                                Margin = "0 0.05 0.1 0.05",
                                Url = "http://i.imgur.com/n9EYIWi.png",
                            },
                            Text = new PanelTextConfig
                            {
                                Order =  2,
                                Width = 0.68f,
                                Height = 1f,
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 14,
                            }
                        }
                    },
                    { "Sleepers", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 3,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.055f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4",
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.22f,
                                Height = 0.8f,
                                Margin = "0 0.05 0.1 0.05",
                                Url = "http://i.imgur.com/XIIZkqD.png",
                            },
                            Text = new PanelTextConfig
                            {
                                Order =  2,
                                Width = 0.63f,
                                Height = 1f,
                                Align = TextAnchor.MiddleCenter,
                                FontColor = DefaultFontColor,
                                FontSize = 14,
                            }
                        }
                    },
                    { "AirdropEvent", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order =  4,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.005",
                            Width = 0.018f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4",
                            Image = new PanelImageConfig
                                {
                                    Order =  1,
                                    Width = 0.8f,
                                    Height = 0.8f,
                                    Margin = "0 0.1 0.1 0.1",
                                    Url = "http://i.imgur.com/dble6vf.png",
                                },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "InactiveColor", "1 1 1 0.1" },
                                { "ActiveColor", "0 1 0 1" },
                            }
                        }
                    },
                    { "HelicopterEvent", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 5,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.0005",
                            Width = 0.020f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4",
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.75f,
                                Height = 0.8f,
                                Margin = "0 0.15 0.1 0.1",
                                Url = "http://i.imgur.com/hTTyTTx.png",
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "InactiveColor", "1 1 1 0.1" },
                                { "ActiveColor", "0.7 0.2 0.2 1" },
                            }

                        }
                    },
                    { "Radiation", new PanelConfig
                        {
                            Available = true,
                            Dock = "BottomDock",
                            Order = 6,
                            AnchorX = "Left",
                            AnchorY = "Bottom",
                            Margin = "0 0 0 0.0005",
                            Width = 0.020f,
                            Height = 0.95f,
                            BackgroundColor = "0 0 0 0.4",
                            Image = new PanelImageConfig
                            {
                                Order =  1,
                                Width = 0.75f,
                                Height = 0.8f,
                                Margin = "0 0.15 0.1 0.1",
                                Url = "http://i.imgur.com/owVdFsK.png",
                            },
                            PanelSettings = new Dictionary<string,object>
                            {
                                { "InactiveColor", "1 1 1 0.1" },
                                { "ActiveColor", "1 1 0 1" },
                                { "RefreshRate(s)", "3"}
                            }

                        }
                    }
                }
            };

            return DefaultConfig;
        }

        class PluginConfig
        {
            //public Dictionary<string, string> Settings { get; set; }

            public Dictionary<string, DockConfig> Docks { get; set; }
            public Dictionary<string, PanelConfig> Panels { get; set; }

            public Dictionary<string, Dictionary<string, PanelConfig>> ThirdPartyPanels { get; set; }

            public List<string> Messages { get; set; }
            public List<string> TimeFormats { get; set; }
            public Dictionary<string, string> CompassDirections { get; set; }

            public T GetPanelSettingsValue<T>(string Panel, string Setting, T defaultValue)
            {
                if (!this.Panels.ContainsKey(Panel))
                {
                    return defaultValue;
                }

                PanelConfig PanelCfg = this.Panels[Panel];

                if (PanelCfg.PanelSettings == null)
                {
                    return defaultValue;
                }

                if (!PanelCfg.PanelSettings.ContainsKey(Setting))
                {
                    return defaultValue;
                }

                var value = PanelCfg.PanelSettings[Setting];

                return (T)Convert.ChangeType(value, typeof(T));
            }

            public bool CheckPanelAvailability(string Panel)
            {
                if (!this.Panels.ContainsKey(Panel))
                {
                    return false;
                }

                PanelConfig PanelCfg = this.Panels[Panel];

                if (!PanelCfg.Available)
                {
                    return false;
                }

                if (!this.Docks.ContainsKey(PanelCfg.Dock))
                {
                    return false;
                }

                DockConfig DockCfg = this.Docks[PanelCfg.Dock];

                if (!DockCfg.Available)
                {
                    return false;
                }

                return true;
            }

        }

        class DockConfig
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool Available { get; set; } = true;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string AnchorX { get; set; } = "Left";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string AnchorY { get; set; } = "Bottom";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float Width { get; set; } = 0.05f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float Height { get; set; } = 0.95f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string BackgroundColor { get; set; } = "0 0 0 0.4";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Margin { get; set; } = "0 0 0 0.005";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public PanelImageConfig Image { get; set; }
        }

        class BasePanelConfig
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool Available { get; set; } = true;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string AnchorX { get; set; } = "Left";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string AnchorY { get; set; } = "Bottom";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float Width { get; set; } = 0.05f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float Height { get; set; } = 0.95f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int Order { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string BackgroundColor { get; set; } = "0 0 0 0.4";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Margin { get; set; } = "0 0 0 0.005";
        }

        class PanelConfig : BasePanelConfig
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool Autoload { get; set; } = true;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Dock { get; set; } = "BottomDock";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, object> PanelSettings { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public PanelImageConfig Image { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public PanelTextConfig Text { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float FadeOut { get; set; }
        }

        class PanelTextConfig : BasePanelConfig
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public new float Width { get; set; } = 1f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public TextAnchor Align { get; set; } = TextAnchor.MiddleCenter;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string FontColor { get; set; } = "1 1 1 1";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int FontSize { get; set; } = 14;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Content { get; set; } = "No Content";

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float FadeIn { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public float FadeOut { get; set; }
        }

        class PanelImageConfig : BasePanelConfig
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public new float Width { get; set; } = 1f;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Url { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Color { get; set; } = null;
        }

        protected void LoadConfigValues()
        {
            Settings = Config.ReadObject<PluginConfig>();

            Dictionary<string, PanelConfig> UnOrderPanels = Settings.Panels.Where(p => p.Value.Order == 0).ToDictionary(s => s.Key, s => s.Value);

            if (UnOrderPanels.Count == 0)
            {
                return;
            }

            PrintWarning("Reordering Panels.");

            foreach (KeyValuePair<string, PanelConfig> PanelCfg in UnOrderPanels)
            {
                //int HighestSiblingOrder = Settings.Panels.Where(p => p.Value.Dock == Settings.Panels[PanelName].Dock && p.Value.AnchorX == Settings.Panels[PanelName].AnchorX).Max(m => m.Value.Order);
                Settings.Panels[PanelCfg.Key].Order = PanelReOrder(PanelCfg.Value.Dock, PanelCfg.Value.AnchorX);
            }

            Config.WriteObject(Settings, true);
            PrintWarning("Config Saved.");
        }

        int PanelReOrder(string DockName, string AnchorX)
        {
            var SiblingPanels = Settings.Panels.Where(p => p.Value.Dock == DockName && p.Value.AnchorX == AnchorX);

            int Max = 0;
            if (SiblingPanels.Any())
            {
                Max = SiblingPanels.Max(m => m.Value.Order);
            }

            foreach (KeyValuePair<string, Dictionary<string, PanelConfig>> PPanelCfg in Settings.ThirdPartyPanels)
            {
                if (PPanelCfg.Value.Count == 0) { continue; }

                var SiblingPluginPAnels = PPanelCfg.Value.Where(p => p.Value.Dock == DockName && p.Value.AnchorX == AnchorX);

                if (SiblingPluginPAnels.Any())
                {
                    int PluginMax = PPanelCfg.Value.Where(p => p.Value.Dock == DockName && p.Value.AnchorX == AnchorX).Max(m => m.Value.Order);
                    if (PluginMax > Max)
                    {
                        Max = PluginMax;
                    }
                }
            }
            return Max + 1;
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(DefaultConfig(), true);
            PrintWarning("Default configuration file created.");
        }

        void Init()
        {
            LoadConfigValues();
            LoadData();
        }

        void OnServerInitialized()
        {
            Clock = new Watch
            (
                Settings.GetPanelSettingsValue("Clock", "ClockUpdateFrequency (seconds)", ClockUpdateFrequency),
                Settings.CheckPanelAvailability("Clock")
            );

            MessageBox = new Messenger
            (
                Settings.Messages,
                Settings.GetPanelSettingsValue("MessageBox", "MessageUpdateFrequency (seconds)", MessageUpdateFrequency),
                Settings.GetPanelSettingsValue("MessageBox", "MsgOrder", "normal")
            );

            Airplane = new AirplaneEvent();
            Helicopter = new HelicopterEvent();

            CompassObj = new Compass
            (
                Settings.GetPanelSettingsValue("Compass", "RefreshRate(s)", 1)
            );

            Rad = new Radiation
            (
                Settings.GetPanelSettingsValue("Radiation", "RefreshRate(s)", 3)
            );

            Coord = new Coordinates
            (
                Settings.GetPanelSettingsValue("Coordinates", "RefreshRate(s)", 3)
            );

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                LoadPanels(player);
                InitializeGUI(player);
            }

            if (Settings.CheckPanelAvailability("Radiation"))
            {
                RadiationUpdater = timer.Repeat(Rad.RefreshRate, 0, () => Rad.Refresh(storedData, PlayerPanels));
            }

            if (Settings.CheckPanelAvailability("Coordinates"))
            {
                CoordUpdater = timer.Repeat(Coord.RefreshRate, 0, () => Coord.Refresh(storedData, PlayerPanels));
            }

            if (Settings.CheckPanelAvailability("MessageBox"))
            {
                MsgUpdater = timer.Repeat(MessageBox.RefreshRate, 0, () => MessageBox.Refresh(storedData, PlayerPanels));
            }

            if (Settings.CheckPanelAvailability("Clock"))
            {
                TimeUpdater = timer.Repeat(Clock.RefresRate, 0, () => Clock.Refresh(storedData, PlayerPanels));
            }

            if (Settings.CheckPanelAvailability("Compass"))
            {
                CompassUpdater = timer.Repeat(CompassObj.RefreshRate, 0, () => CompassObj.Refresh(storedData, PlayerPanels));
            }

            //TestTimer = timer.Repeat(5, 0, () => TestSH());

            ActivePlanes = UnityEngine.Object.FindObjectsOfType<CargoPlane>().ToList();

            if (ActivePlanes.Count > 0)
            {
                CheckAirplane();
            }
            else
            {
                Airplane.Refresh(storedData, PlayerPanels);
            }

            ActiveHelicopters = UnityEngine.Object.FindObjectsOfType<BaseHelicopter>().ToList();

            if (ActiveHelicopters.Count > 0)
            {
                CheckHelicopter();
            }
            else
            {
                Helicopter.Refresh(storedData, PlayerPanels);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(2, () => OnPlayerInit(player));
                return;
            }
            //Ha player connection alatt kickelve volt akkor nem hat rá az onDisconnect, ezért itt is ellenőrizni kell....
            if (PlayerPanels.ContainsKey(player.UserIDString))
            {
                PlayerPanels.Remove(player.UserIDString);
            }

            if (PlayerDockPanels.ContainsKey(player.UserIDString))
            {
                PlayerDockPanels.Remove(player.UserIDString);
            }

            timer.In(1, () => GUITimerInit(player));
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            PlayerPanels.Remove(player.UserIDString);
            PlayerDockPanels.Remove(player.UserIDString);

            timer.Once(2, RefreshOnlinePlayers);
            timer.Once(2, RefreshSleepers);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            timer.Once(2, RefreshSleepers);
        }

        private void OnEntitySpawned(BaseEntity Entity)
        {
            if (Entity == null) return;
            if (Entity is BaseHelicopter && Settings.Panels["HelicopterEvent"].Available)
            {
                ActiveHelicopters.Add((BaseHelicopter) Entity);

                if (HelicopterTimer == false)
                {
                    CheckHelicopter();
                }
            }


            if (Entity is CargoPlane && Settings.Panels["AirdropEvent"].Available)
            {
                ActivePlanes.Add((CargoPlane) Entity);

                if (AirplaneTimer == false)
                {
                    CheckAirplane();
                }
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyGUI(player);

            SaveData();

            PlayerPanels.Clear();
            PlayerDockPanels.Clear();

            Err.Clear();
            ErrD.Clear();
            ErrB.Clear();
            ErrA.Clear();

            storedData = null;
            Settings = null;
        }

        void OnPluginUnloaded(Plugin plugin)
        {
            if (!Settings.ThirdPartyPanels.ContainsKey(plugin.Title)) return;
            List<string> PluginPanels = LoadedPluginPanels[plugin.Title];

            foreach(string PanelName in PluginPanels)
            {
                foreach (var PlayerID in PlayerPanels.Keys)
                {
                    if(PlayerPanels[PlayerID].ContainsKey(PanelName))
                    {
                        PlayerPanels[PlayerID][PanelName].DestroyPanel();
                    }

                    PlayerPanels[PlayerID][PanelName].Remover();
                }
            }

            LoadedPluginPanels.Remove(plugin.Title);
        }

        void OnServerSave()
        {
            SaveData();
        }

        void OnServerShutdown()
        {
            SaveData();
        }

        #endregion

        #region PanelLoad
        /// <summary>
        /// Panelek betöltése egy adott játékosnak.
        /// </summary>
        /// <param name="Player"></param>
        private void LoadPanels(BasePlayer Player)
        {
            foreach(KeyValuePair<string, DockConfig> Docks in Settings.Docks)
            {
                if (!Settings.Docks[Docks.Key].Available)
                {
                    continue;
                }

                IPanel DockPanel = LoadDockPanel(Docks.Key, Player);
            }

            foreach (var GrouppedByDock in Settings.Panels.GroupBy(g => g.Value.Dock).ToDictionary(gd => gd.Key, gd => gd.Select(p => p).ToDictionary(gk => gk.Key, gk => gk.Value)))
            {
                if (!Settings.Docks[GrouppedByDock.Key].Available)
                {
                    continue;
                }

                foreach (var PanelCfg in GrouppedByDock.Value)
                {
                    if (!Settings.CheckPanelAvailability(PanelCfg.Key))
                    {
                        continue;
                    }

                    LoadPanel(PlayerDockPanels[Player.UserIDString][GrouppedByDock.Key], PanelCfg.Key, PanelCfg.Value);
                }
            }

            foreach(var LoadedPluginPanel in LoadedPluginPanels)
            {
                foreach(string PanelName in LoadedPluginPanel.Value)
                {
                    if (!Settings.ThirdPartyPanels.ContainsKey(LoadedPluginPanel.Key)
                        || !Settings.ThirdPartyPanels[LoadedPluginPanel.Key].ContainsKey(PanelName)
                        || !Settings.ThirdPartyPanels[LoadedPluginPanel.Key][PanelName].Available)
                    {
                        continue;
                    }

                    LoadPanel(PlayerDockPanels[Player.UserIDString][Settings.ThirdPartyPanels[LoadedPluginPanel.Key][PanelName].Dock], PanelName, Settings.ThirdPartyPanels[LoadedPluginPanel.Key][PanelName]);
                }


            }
        }

        private IPanel LoadDockPanel(string DockName, BasePlayer Player)
        {
            IPanel DockPanel = new IPanel(DockName, Player, PlayerPanels, PlayerDockPanels);
            DockPanel.Width = Settings.Docks[DockName].Width;
            DockPanel.Height = Settings.Docks[DockName].Height;
            DockPanel.AnchorX = Settings.Docks[DockName].AnchorX;
            DockPanel.AnchorY = Settings.Docks[DockName].AnchorY;
            DockPanel.Margin = Vector4Parser(Settings.Docks[DockName].Margin);
            DockPanel.BackgroundColor = ColorEx.Parse(Settings.Docks[DockName].BackgroundColor);
            DockPanel.IsDock = true;

            //LoadedDocks.Add(DockName, DockPanel);

            if(!PlayerDockPanels.ContainsKey(Player.UserIDString))
            {
                PlayerDockPanels.Add(Player.UserIDString, new Dictionary<string, IPanel>());
                PlayerDockPanels[Player.UserIDString].Add(DockName, DockPanel);
            }
            else
            {
                PlayerDockPanels[Player.UserIDString].Add(DockName, DockPanel);
            }

            return DockPanel;
        }

        private void LoadPanel(IPanel Dock, string PanelName, PanelConfig PCfg)
        {
            IPanel Panel = Dock.AddPanel(PanelName);
            Panel.Width = PCfg.Width;
            Panel.Height = PCfg.Height;
            Panel.AnchorX = PCfg.AnchorX;
            Panel.AnchorY = PCfg.AnchorY;
            Panel.Margin = Vector4Parser(PCfg.Margin);
            Panel.BackgroundColor = ColorEx.Parse(PCfg.BackgroundColor);
            Panel.Order = PCfg.Order;
            Panel.Autoload = PCfg.Autoload;
            Panel.IsPanel = true;
            Panel.DockName = Dock.Name;
            Panel.FadeOut = Dock.FadeOut;

            if (PCfg.Text != null)
            {
                IPanelText Text = Panel.AddText(PanelName + "Text");
                Text.Width = PCfg.Text.Width;
                Text.Height = PCfg.Text.Height;
                Text.Margin = Vector4Parser(PCfg.Text.Margin);
                Text.Content = PCfg.Text.Content;
                Text.FontColor = ColorEx.Parse(PCfg.Text.FontColor);
                Text.FontSize = PCfg.Text.FontSize;
                Text.Align = PCfg.Text.Align;
                Text.Order = PCfg.Text.Order;
                Text.FadeOut = PCfg.Text.FadeOut;
                Text.TextComponent.FadeIn = PCfg.Text.FadeIn;
            }

            if (PCfg.Image != null)
            {
                IPanelRawImage Image = Panel.AddImage(PanelName + "Image");
                Image.Width = PCfg.Image.Width;
                Image.Height = PCfg.Image.Height;
                Image.Margin = Vector4Parser(PCfg.Image.Margin);
                Image.Url = PCfg.Image.Url;
                Image.Order = PCfg.Image.Order;
                if(PCfg.Image.Color != null)
                {
                    Image.Color = ColorEx.Parse(PCfg.Image.Color);
                }
            }
        }

        #endregion

        #region Clock

        private Watch Clock;
        private int ClockUpdateFrequency = 4;
        private Timer TimeUpdater;

        public class Watch
        {
            string ClockFormat = "HH:mm";
            public int RefresRate = 4;
            public bool Available = true;

            TOD_Sky Sky = TOD_Sky.Instance;

            public Watch(int RefreshRate, bool Available)
            {
                this.RefresRate = RefreshRate;
                this.Available = Available;
            }

            public string GetServerTime(string PlayerID, StoredData storedData)
            {
                return DateTime.Now.AddHours(storedData.GetPlayerPanelSettings(PlayerID, "Clock", "Offset", 0)).ToString(storedData.GetPlayerPanelSettings(PlayerID, "Clock", "TimeFormat", ClockFormat), CultureInfo.InvariantCulture);
            }

            public string GetSkyTime(string PlayerID, StoredData storedData)
            {
                return Sky.Cycle.DateTime.ToString(storedData.GetPlayerPanelSettings(PlayerID, "Clock", "TimeFormat", ClockFormat), CultureInfo.InvariantCulture);
            }

            public string ShowTime(string PlayerID, StoredData storedData)
            {
                if (storedData.GetPlayerPanelSettings(PlayerID, "Clock", "Type", "Game") == "Server")
                {
                    return GetServerTime(PlayerID, storedData);
                }

                return GetSkyTime(PlayerID, storedData);
            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if(!Settings.CheckPanelAvailability("Clock"))
                {
                    return;
                }

                foreach (KeyValuePair<string, Dictionary<string, IPanel>> panel in panels)
                {
                    if (panel.Value.ContainsKey("ClockText"))
                    {
                        var showTime = ShowTime(panel.Key, storedData);
                        var panelText = (IPanelText)panel.Value["ClockText"];
                        if (!showTime.Equals(panelText.Content))
                        {
                            panelText.Content = showTime;
                            panelText.Refresh();
                        }
                    }
                }
            }
        }

        #endregion

        #region MessageBox

        private Messenger MessageBox;
        private Timer MsgUpdater;
        private int MessageUpdateFrequency = 20;
        private List<string> Messages = new List<string>() { "Welcome!", "Beware! You Are Not Alone!", "Leeeeeeeeeeeroy Jenkins" };
        private bool MessageBoxAvailable = true;


        public class Messenger
        {
            List<string> Messages;
            public int RefreshRate = 20;
            private int Counter = 0;
            private string MsgOrder = "normal";

            public Messenger(List<string> msgs, int RefreshRate,string MsgOrder)
            {
                this.Messages = msgs;
                this.RefreshRate = RefreshRate;
                this.MsgOrder = MsgOrder;

                if (MsgOrder == "random")
                {
                    Counter = Core.Random.Range(0, Messages.Count - 1);
                }

            }

            public string GetMessage()
            {
                return Messages[Counter];
            }

            private void RefreshCounter()
            {
                if (MsgOrder == "random")
                {
                    int OldCounter = Counter;
                    int NewCounter = Core.Random.Range(0, Messages.Count - 1);

                    if(OldCounter == NewCounter)
                    {
                        if(NewCounter+1 <= Messages.Count-1)
                        {
                            Counter = NewCounter + 1;
                            return;
                        }
                        else if(NewCounter - 1 >= 0)
                        {
                            Counter = NewCounter - 1;
                            return;
                        }
                    }

                    Counter = NewCounter;
                    return;
                }

                Counter++;
                if (Counter >= Messages.Count)
                    Counter = 0;
            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if (!Settings.CheckPanelAvailability("MessageBox"))
                {
                    return;
                }

                foreach (KeyValuePair<string, Dictionary<string, IPanel>> panel in panels)
                {
                    if (panel.Value.ContainsKey("MessageBoxText"))
                    {
                        var message = GetMessage();
                        var panelText = (IPanelText)panel.Value["MessageBoxText"];
                        if (!message.Equals(panelText.Content))
                        {
                            panelText.Content = message;
                            panelText.Refresh();
                        }
                    }
                }

                RefreshCounter();
            }

        }
        #endregion

        #region Events
        private Timer HeliAttack;
        private Timer RadiationUpdater;

        private AirplaneEvent Airplane;
        private List<CargoPlane> ActivePlanes;
        private bool AirplaneTimer = false;

        private HelicopterEvent Helicopter;
        private List<BaseHelicopter> ActiveHelicopters;
        private bool HelicopterTimer = false;

        private Radiation Rad;

        private BaseHelicopter ActiveHelicopter;

        public class AirplaneEvent
        {
            public bool isActive = false;
            public Color ImageColor;

            public AirplaneEvent()
            {
                ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("AirdropEvent", "InactiveColor", "1 1 1 0.1"));
            }

            public void SetActivity(bool active)
            {
                this.isActive = active;

                if (isActive)
                {
                    ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("AirdropEvent", "ActiveColor", "0 1 0 1"));
                    return;
                }
                ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("AirdropEvent", "InactiveColor", "1 1 1 0.1"));
                return;
            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if (!Settings.CheckPanelAvailability("AirdropEvent"))
                {
                    return;
                }

                foreach (var panel in panels)
                {
                    if (panel.Value.ContainsKey("AirdropEventImage"))
                    {
                        var panelRawImage = (IPanelRawImage)panel.Value["AirdropEventImage"];
                        if (panelRawImage.Color != ImageColor)
                        {
                            panelRawImage.Color = ImageColor;
                            panelRawImage.Refresh();
                        }
                    }
                }
            }
        }

        public class HelicopterEvent
        {
            public bool isActive = false;
            public Color ImageColor;

            public HelicopterEvent()
            {
                ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("HelicopterEvent", "InactiveColor", "1 1 1 0.1"));
            }

            public void SetActivity(bool active)
            {
                this.isActive = active;

                if (isActive)
                {
                    ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("HelicopterEvent", "ActiveColor", "1 0 0 1"));
                    return;
                }

                ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("HelicopterEvent", "InactiveColor", "1 1 1 0.1"));
                return;

            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if (!Settings.CheckPanelAvailability("HelicopterEvent"))
                {
                    return;
                }

                foreach (var panel in panels)
                {
                    if (panel.Value.ContainsKey("HelicopterEventImage"))
                    {
                        var panelRawImage = (IPanelRawImage)panel.Value["HelicopterEventImage"];
                        if (panelRawImage.Color != ImageColor)
                        {
                            panelRawImage.Color = ImageColor;
                            panelRawImage.Refresh();
                        }
                    }
                }
            }
        }

        public class Radiation
        {
            bool isActive = false;
            public Color ImageColor;
            public int RefreshRate = 3;

            public Radiation(int RefreshRate)
            {
                isActive = ConVar.Server.radiation;
                this.RefreshRate = RefreshRate;
                if (isActive)
                {
                    ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("Radiation", "ActiveColor", "1 1 0 1"));
                }
                else
                {
                    ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("Radiation", "InactiveColor", "1 1 1 0.1"));
                }
            }

            public void SetActivity(bool active)
            {
                this.isActive = active;

                if (isActive)
                {
                    ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("Radiation", "ActiveColor", "1 0 0 1"));
                    return;
                }

                ImageColor = ColorEx.Parse(Settings.GetPanelSettingsValue("Radiation", "InactiveColor", "1 1 1 0.1"));
                return;

            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if(isActive == ConVar.Server.radiation)
                {
                    return;
                }

                SetActivity(ConVar.Server.radiation);

                if (!Settings.CheckPanelAvailability("Radiation"))
                {
                    return;
                }

                foreach (KeyValuePair<string, Dictionary<string, IPanel>> panel in panels)
                {
                    if (panel.Value.ContainsKey("RadiationImage"))
                    {
                        var panelRawImage = (IPanelRawImage)panel.Value["RadiationImage"];
                        if (panelRawImage.Color != ImageColor)
                        {
                            panelRawImage.Color = ImageColor;
                            panelRawImage.Refresh();
                        }
                    }
                }
            }
        }

        public void CheckAirplane()
        {
            ActivePlanes.RemoveAll(p => !p.IsActive());
            if (ActivePlanes.Count > 0)
            {
                if(Airplane.isActive == false)
                {
                    Airplane.SetActivity(true);
                    Airplane.Refresh(storedData, PlayerPanels);
                }

                AirplaneTimer = true;
                timer.In(10, () => CheckAirplane());
                return;
            }

            Airplane.SetActivity(false);
            Airplane.Refresh(storedData, PlayerPanels);
            AirplaneTimer = false;
        }

        public void CheckHelicopter()
        {
            ActiveHelicopters.RemoveAll(p => !p.IsActive());

            if (ActiveHelicopters.Count > 0)
            {

                if (Helicopter.isActive == false)
                {
                    Helicopter.SetActivity(true);
                    Helicopter.Refresh(storedData, PlayerPanels);
                }

                HelicopterTimer = true;
                timer.In(5, CheckHelicopter);
                return;
            }

            Helicopter.SetActivity(false);
            Helicopter.Refresh(storedData, PlayerPanels);
            HelicopterTimer = false;
        }

        #endregion

        #region Coordinates

        private Coordinates Coord;

        private Timer CoordUpdater;

        public class Coordinates
        {
            public int RefreshRate = 3;

            public Coordinates(int RefreshRate)
            {
                this.RefreshRate = RefreshRate;
            }

            public string GetCoord(string PlayerID)
            {
                BasePlayer player = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString == PlayerID);

                Vector3 PCurrent = player.transform.position;

                return "X: " + PCurrent.x.ToString("0") + " | Z: " + PCurrent.z.ToString("0");
            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if (!Settings.CheckPanelAvailability("Coordinates"))
                {
                    return;
                }

                foreach (var panel in panels)
                {
                    if (panel.Value.ContainsKey("CoordinatesText"))
                    {
                        var coord = GetCoord(panel.Key);
                        var panelText = (IPanelText)panel.Value["CoordinatesText"];
                        if (!coord.Equals(panelText.Content))
                        {
                            panelText.Content = coord;
                            panelText.Refresh();
                        }
                    }
                }
            }
        }
        #endregion

        #region Compass

        private Compass CompassObj;

        private Timer CompassUpdater;

        public class Compass
        {
            public int RefreshRate = 3;

            public Compass(int RefreshRate)
            {
                this.RefreshRate = RefreshRate;
            }

            public string GetDirection(string PlayerID)
            {
                BasePlayer player = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString == PlayerID);

                Vector3 PCurrent = player.eyes.rotation.eulerAngles;

                string str = PCurrent.y.ToString("0") + "°";

                if (Settings.GetPanelSettingsValue("Compass", "TextOrAngle", "text") == "text")
                {
                    if (PCurrent.y > 337.5 || PCurrent.y < 22.5)
                        str = Settings.CompassDirections["n"];
                    else if (PCurrent.y > 22.5 && PCurrent.y < 67.5)
                        str = Settings.CompassDirections["ne"];
                    else if (PCurrent.y > 67.5 && PCurrent.y < 112.5)
                        str = Settings.CompassDirections["e"];
                    else if (PCurrent.y > 112.5 && PCurrent.y < 157.5)
                        str = Settings.CompassDirections["se"];
                    else if (PCurrent.y > 157.5 && PCurrent.y < 202.5)
                        str = Settings.CompassDirections["s"];
                    else if (PCurrent.y > 202.5 && PCurrent.y < 247.5)
                        str = Settings.CompassDirections["sw"];
                    else if (PCurrent.y > 247.5 && PCurrent.y < 292.5)
                        str = Settings.CompassDirections["w"];
                    else if (PCurrent.y > 292.5 && PCurrent.y < 337.5)
                        str = Settings.CompassDirections["nw"];
                }

                return str;
            }

            public void Refresh(StoredData storedData, Dictionary<string, Dictionary<string, IPanel>> panels)
            {
                if (!Settings.CheckPanelAvailability("Compass"))
                {
                    return;
                }

                foreach (var panel in panels)
                {
                    if (panel.Value.ContainsKey("CompassText"))
                    {
                        var direction = GetDirection(panel.Key);
                        var panelText = (IPanelText)panel.Value["CompassText"];
                        if (!direction.Equals(panelText.Content))
                        {
                            panelText.Content = direction;
                            panelText.Refresh();
                        }
                    }
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("ipanel")]
        private void IPanelCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                string Str = "InfoPanel Available Commands:\n";
                Str += "<b><color=#ffa500ff>/ipanel</color></b> - Chat Command list \n";
                Str += "<b><color=#ffa500ff>/ipanel <hide|show></color></b>- To hide or show the panel. \n";
                Str += "<b><color=#ffa500ff>/ipanel clock game</color></b> - Change to game time. \n";
                Str += "<b><color=#ffa500ff>/ipanel clock server <offset></color></b> - Change to server time.\n Offset: Add hours to the clock. (-23 - 23) \n";
                Str += "<b><color=#ffa500ff>/ipanel timeformat</color></b> - To change time format. \n";

                PrintToChat(player, Str);

                return;
            }

            switch (args[0])
            {
                case "hide":
                    if (!storedData.GetPlayerSettings(player.UserIDString, "enable", true))
                    {
                        break;
                    }

                    ChangePlayerSettings(player, "enable", "false");
                    DestroyGUI(player);
                    break;
                case "show":
                    if (storedData.GetPlayerSettings(player.UserIDString, "enable", true))
                    {
                        break;
                    }

                    ChangePlayerSettings(player, "enable", "true");
                    RevealGUI(player);
                    break;

                case "clock":
                    if (args[1] == "server")
                    {
                        ChangePlayerPanelSettings(player, "Clock", "Type", "Server");

                        if (args.Length == 3)
                        {
                            int offset = 0;

                            if (Int32.TryParse(args[2], out offset) && offset > -23 && offset < 23)
                            {
                                ChangePlayerPanelSettings(player, "Clock", "Offset", offset.ToString());
                            }
                        }

                    }
                    else if (args[1] == "game")
                    {
                        ChangePlayerPanelSettings(player, "Clock", "Type", "Game");
                    }
                    break;
                case "timeformat":
                    if (args.Length == 1)
                    {
                        string Str = "Available Time Formats:\n";

                        for (int index = 0; index < Settings.TimeFormats.Count; index++)
                        {
                            Str += "[<color=#ffa500ff>" + index + "</color>] - " + DateTime.Now.ToString(Settings.TimeFormats[index])+"\n";
                        }

                        PrintToChat(player, Str+"Usage: /ipanel timeformat <color=#ffa500ff> NUMBER </color>");
                    }
                    else if(args.Length == 2)
                    {
                        int TimeFormat = 0;
                        if (Int32.TryParse(args[1], out TimeFormat) && TimeFormat >= 0 && TimeFormat < Settings.TimeFormats.Count)
                        {
                            ChangePlayerPanelSettings(player, "Clock", "TimeFormat", TimeFormats[TimeFormat]);
                        }
                    }
                    break;
                default:
                    PrintToChat(player, "Wrong Command!");
                    break;
            };

        }

        [ChatCommand("iptest")]
        private void IPaCommand(BasePlayer player, string command, string[] args)
        {

        }

        [ChatCommand("iperr")]
        private void IPCommand(BasePlayer player, string command, string[] args)
        {
            /*
            foreach (string item in Err)
            {
                Puts(item);
            }*/

            /*foreach (KeyValuePair<string,Dictionary<string,IPanel>> item in PlayerDockPanels)
            {
                foreach (KeyValuePair<string, IPanel> itemm in item.Value)
                {
                    Puts(itemm.Key);
                }
            }*/
            /*
            foreach (KeyValuePair<string, int> item in ErrB.OrderBy(k => k.Key))
            {
                Puts(item.Key + " - " + item.Value);
            }*/
           /*
            foreach (KeyValuePair<string, List<string>> item in ErrA)
            {
                Puts(item.Key + " -> ");

                foreach (string itemm in item.Value)
                {
                    Puts(itemm);
                }

                Puts("--------");
            }*/

            Err.Clear();
            ErrA.Clear();
            ErrB.Clear();
        }

        #endregion

        #region StoredData

        public static StoredData storedData;

        public class StoredData
        {
            public Dictionary<string, PlayerSettings> Players;

            public StoredData()
            {
                this.Players = new Dictionary<string, PlayerSettings>();
            }

            public bool CheckPlayerData(BasePlayer Player)
            {
                return (Players.ContainsKey(Player.UserIDString)) ? true : false;
            }

            public T GetPlayerSettings<T>(string PlayerID, string Key, T DefaultValue)
            {
                if (Players.ContainsKey(PlayerID))
                {
                    return Players[PlayerID].GetSetting(Key, DefaultValue);
                }

                return DefaultValue;
            }

            public T GetPlayerPanelSettings<T>(BasePlayer Player, string Panel, string Key, T DefaultValue)
            {

                if (Players.ContainsKey(Player.UserIDString))
                {
                    return Players[Player.UserIDString].GetPanelSetting(Panel, Key, DefaultValue);
                }
                return DefaultValue;
            }

            public T GetPlayerPanelSettings<T>(string PlayerID, string Panel, string Key, T DefaultValue)
            {

                if (Players.ContainsKey(PlayerID))
                {
                    return Players[PlayerID].GetPanelSetting(Panel, Key, DefaultValue);
                }
                return DefaultValue;
            }

        }

        public class PlayerSettings
        {
            public string UserId;
            public Dictionary<string, string> Settings;
            public Dictionary<string, Dictionary<string, string>> PanelSettings;

            public PlayerSettings()
            {
                this.Settings = new Dictionary<string, string>();
                this.PanelSettings = new Dictionary<string, Dictionary<string, string>>();
            }

            public PlayerSettings(BasePlayer player)
            {
                UserId = player.UserIDString;
                this.Settings = new Dictionary<string, string>();
                this.PanelSettings = new Dictionary<string, Dictionary<string, string>>();
            }

            public void SetSetting(string Key, string Value)
            {
                this.Settings[Key] = Value;
            }

            public void SetPanelSetting(string Panel, string Key, string Value)
            {
                if (!this.PanelSettings.ContainsKey(Panel))
                {
                    this.PanelSettings.Add(Panel, new Dictionary<string, string>());
                }

                this.PanelSettings[Panel][Key] = Value;
            }

            public T GetPanelSetting<T>(string Panel, string Key, T DefaultValue)
            {
                if (!this.PanelSettings.ContainsKey(Panel))
                {
                    return DefaultValue;
                }

                var PanelConfig = this.PanelSettings[Panel];

                if (!PanelConfig.ContainsKey(Key))
                {
                    return DefaultValue;
                }

                var value = PanelConfig[Key];

                if (value == null)
                {
                    return DefaultValue;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }


            public T GetSetting<T>(string Key, T DefaultValue)
            {

                if (!this.Settings.ContainsKey(Key))
                {
                    return DefaultValue;
                }

                var value = this.Settings[Key];

                if (value == null)
                {
                    return DefaultValue;
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }

        }

        public void LoadData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("InfoPanel_db");
            if (storedData == null)
            {
                storedData = new StoredData();
                SaveData();
            }
        }

        public void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("InfoPanel_db", storedData);
        }

        public void ChangePlayerSettings(BasePlayer player, string Key, string Value)
        {
            PlayerSettings playerSettings;
            if (!storedData.Players.TryGetValue(player.UserIDString, out playerSettings))
                storedData.Players[player.UserIDString] = playerSettings = new PlayerSettings(player);
            playerSettings.SetSetting(Key, Value);
        }

        public void ChangePlayerPanelSettings(BasePlayer player, string Panel, string Key, string Value)
        {
            PlayerSettings playerSettings;
            if (!storedData.Players.TryGetValue(player.UserIDString, out playerSettings))
                storedData.Players[player.UserIDString] = playerSettings = new PlayerSettings(player);
            playerSettings.SetPanelSetting(Panel, Key, Value);
        }


        #endregion

        public List<string> Err = new List<string>();
        public Dictionary<string, List<string>> ErrA = new Dictionary<string,List<string>>();
        public Dictionary<string, int> ErrB = new Dictionary<string, int>();
        public Dictionary<string,int> ErrD = new Dictionary<string,int>();

        #region IPanelClass
        [JsonObject(MemberSerialization.OptIn)]
        public class IPanel
        {
            #region Class Variables

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("parent")]
            public string ParentName { get; set; } = "HUD/Overlay";

            [JsonProperty("components")]
            public List<ICuiComponent> Components = new List<ICuiComponent>();

            [JsonProperty("fadeOut")]
            public float FadeOut { get; set; }

            //Left-Right
            public Vector2 HorizontalPosition { get; set; } = new Vector2(0f, 1f);

            //Bottom-Top
            public Vector2 VerticalPosition { get; set; } = new Vector2(0f, 1f);

            public string AnchorX { get; set; } = "Left";

            public string AnchorY { get; set; } = "Bottom";

            public Vector4 Padding = Vector4.zero;
            public Vector4 Margin { get; set; } = Vector4.zero;

            public float Width { get; set; } = 1f;

            public float Height { get; set; } = 1f;

            public Color _BGColor = Color.black;
            public Color BackgroundColor
            {
                get
                {
                    return this._BGColor;
                }
                set
                {
                    this._BGColor = value;

                    if (ImageComponent == null)
                    {
                        ImageComponent = new CuiImageComponent();
                        Components.Insert(0, ImageComponent);
                    }

                    ImageComponent.Color = $"{value.r} {value.g} {value.b} {value.a}";
                }
            }

            public int Order = 0;

            public float _VerticalOffset = 0f;
            public float VerticalOffset
            {
                get
                {
                    return this._VerticalOffset;
                }

                set
                {
                    this._VerticalOffset = value;
                    SetVerticalPosition();
                }
            }

            //public Dictionary<string, IPanel> Childs = new Dictionary<string, IPanel>();
            public List<string> Childs = new List<string>();

            //Components
            public CuiRectTransformComponent RecTransform;
            public CuiImageComponent ImageComponent;

            //public bool ChildsChanged = false;

            BasePlayer Owner = null;

            public string DockName = null;

            public bool IsActive = false;
            public bool IsHidden = false;

            public bool IsPanel = false;
            public bool IsDock = false;

            public bool Autoload = true;
            private Dictionary<string, Dictionary<string, IPanel>> playerPanels;
            private Dictionary<string, Dictionary<string, IPanel>> playerDockPanels;
            #endregion

            public IPanel(string name, BasePlayer Player, Dictionary<string, Dictionary<string, IPanel>> playerPanels, Dictionary<string, Dictionary<string, IPanel>> playerDockPanels)
            {

                this.Name = name;
                this.Owner = Player;
                this.playerPanels = playerPanels;
                this.playerDockPanels = playerDockPanels;

                //LoadedPanels.Add(this._Name, this);

                Dictionary<string, IPanel> playerPanel;
                if (!playerPanels.TryGetValue(Player.UserIDString, out playerPanel))
                    playerPanels.Add(Player.UserIDString, playerPanel = new Dictionary<string, IPanel>());
                playerPanel.Add(name, this);

                RecTransform = new CuiRectTransformComponent();
                Components.Add(RecTransform);
            }

            public void SetAnchorXY(string Horizontal, string Vertical)
            {
                this.AnchorX = Horizontal;
                this.AnchorY = Vertical;
            }

            #region Positioning

            //x,y,z,w
            public void SetHorizontalPosition()
            {
                float Left;
                float Right;
                float Offset = GetOffset();

                if (AnchorX == "Right")
                {
                    Right = 1f;
                    Left = Right - (this.Width);

                    this.HorizontalPosition = new Vector2(Left, Right) - new Vector2(Offset - this.Margin.y, Offset - this.Margin.y);
                }
                else
                {
                    Left = 0f;
                    Right = Left + (this.Width);

                    this.HorizontalPosition = new Vector2(Left, Right) + new Vector2(Offset + this.Margin.w, Offset + this.Margin.w);
                }

                RecTransform.AnchorMin = $"{HorizontalPosition.x} {VerticalPosition.x}";
                RecTransform.AnchorMax = $"{HorizontalPosition.y} {VerticalPosition.y}";
            }

            public void SetVerticalPosition()
            {
                float Top;
                float Bottom;

                if (AnchorY == "Top")
                {
                    Top = 1f;
                    Bottom = Top - (Height);
                    this.VerticalPosition = new Vector2(Bottom, Top) + new Vector2(this._VerticalOffset - this.Margin.x, this._VerticalOffset - this.Margin.x);
                }
                else
                {
                    Bottom = 0f;
                    Top = Bottom + (Height);

                    this.VerticalPosition = new Vector2(Bottom, Top) + new Vector2(this._VerticalOffset + this.Margin.z, this._VerticalOffset + this.Margin.z);
                }

                RecTransform.AnchorMin = $"{HorizontalPosition.x} {VerticalPosition.x}";
                RecTransform.AnchorMax = $"{HorizontalPosition.y} {VerticalPosition.y}";
            }

            float FullWidth()
            {
                return this.Width + this.Margin.y + this.Margin.w;
            }

            float GetSiblingsFullWidth()
            {
                return 1f;
            }
            #endregion

            #region Json
            public string ToJson()
            {
                SetHorizontalPosition();
                SetVerticalPosition();

                return JsonConvert.SerializeObject(
                    this,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Ignore
                    }
                );
            }


            public float GetOffset()
            {
                // Előző testvérek ugyan azzal az anchorX-el

                float Offset = 0f;

                IPanel Parent = GetPanel(ParentName);

                if (Parent == null)
                {
                    return Offset;
                }

                List<IPanel> Siblings = Parent.GetChilds().Where(c => c.Value.AnchorX == this.AnchorX && c.Value.Order <= this.Order && c.Value.IsActive && c.Value.Name != this.Name).Select(c => c.Value).OrderBy(s => s.Order).ToList();

                foreach (IPanel Sibling in Siblings)
                {
                    Offset += Sibling.Width + Sibling.Margin.y + Sibling.Margin.w;
                }

                return Offset;
            }

            public string GetJson(bool Brackets = true)
            {
                string Panel = ToJson();

                if (Brackets)
                {
                    return $"[{Panel}]";
                }
                return Panel;
            }

            #endregion

            #region Childs

            public int GetLastChild()
            {
                if (this.Childs.Count == 0)
                {
                    return 0;
                }
                else
                {
                    return this.GetChilds().Max(p => p.Value.Order);
                }
            }

            public IPanelText AddText(string Name)
            {
                IPanelText Text = new IPanelText(Name, this.Owner, playerPanels, playerDockPanels) {ParentName = this.Name};
                Childs.Add(Name);
                return Text;
            }

            public IPanelRawImage AddImage(string Name)
            {
                IPanelRawImage Image = new IPanelRawImage(Name, this.Owner, playerPanels, playerDockPanels) {ParentName = this.Name};
                Childs.Add(Name);
                return Image;
            }

            public IPanel AddPanel(string Name)
            {
                IPanel Panel = new IPanel(Name, this.Owner, playerPanels, playerDockPanels) {ParentName = this.Name};
                Childs.Add(Name);
                return Panel;
            }

            #endregion

            #region Selectors

            List<string> GetActiveAfterThis()
            {
                List<string> Panels = playerPanels[this.Owner.UserIDString]
                    .Where(p => p.Value.IsActive && p.Value.Order > this.Order && p.Value.ParentName == this.ParentName && p.Value.AnchorX == this.AnchorX)
                    .OrderBy(s => s.Value.Order)
                    .Select(k => k.Key)
                    .ToList();

                return Panels;
            }

            public Dictionary<string, IPanel> GetChilds()
            {
                return playerPanels[this.Owner.UserIDString].Where(x => Childs.Contains(x.Key)).ToDictionary(se => se.Key, se => se.Value);
            }

            public IPanel GetParent()
            {
                if (GetPanel(this.ParentName) != null)
                {
                    return GetPanel(this.ParentName);
                }

                return null;
            }

            public List<IPanel> GetSiblings()
            {
                IPanel Parent = GetPanel(ParentName);

                if(Parent != null)
                {
                    return Parent.GetChilds().Where(c => c.Value.AnchorX == this.AnchorX && c.Value.Name != this.Name).Select(c => c.Value).OrderBy(s => s.Order).ToList();
                }

                return new List<IPanel>() { };
            }

            public IPanel GetPanel(string PName)
            {
                if (playerPanels[this.Owner.UserIDString].ContainsKey(PName))
                {
                    return playerPanels[this.Owner.UserIDString][PName];
                }

                return null;
            }

            public IPanel GetDock()
            {
                if (DockName == null) return null;

                if (playerDockPanels[this.Owner.UserIDString].ContainsKey(DockName))
                {
                    return playerPanels[this.Owner.UserIDString][DockName];
                }

                return null;
            }

            #endregion

            #region GUI


            public void Hide()
            {
                foreach (KeyValuePair<string, IPanel> Panel in GetChilds().Where(p => p.Value.IsActive))
                {
                    Panel.Value.Hide();
                }

                Oxide.Game.Rust.Cui.CuiHelper.DestroyUi(this.Owner, this.Name);
                //CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(this.Owner.net.connection), null, "DestroyUI", new Facepunch.ObjectList(this._Name));
            }

            public void Reveal()
            {

                //Interface.Oxide.LogInfo(GetJson()); //TODO
                Oxide.Game.Rust.Cui.CuiHelper.AddUi(this.Owner, GetJson());
                //CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(this.Owner.net.connection), null, "AddUI", new Facepunch.ObjectList(GetJson()));

                IsActive = true;
                IsHidden = false;

                foreach (KeyValuePair<string, IPanel> Child in GetChilds().Where(p => p.Value.Autoload || p.Value.IsActive).OrderBy(s => s.Value.Order))
                {
                    Child.Value.Reveal();
                }

            }

            void ReDrawPanels(List<string> PanelsName)
            {
                foreach (string PanelName in PanelsName)
                {
                    GetPanel(PanelName)?.DestroyPanel(false);
                }

                foreach (string PanelName in PanelsName)
                {
                    GetPanel(PanelName)?.ShowPanel();
                }
            }

            /// <summary>
            /// Panel megjelenítése.
            /// </summary>
            /// <param name="player"></param>
            public void ShowPanel(bool Childs = true)
            {
                if (storedData.GetPlayerSettings(Owner.UserIDString, "enable", true))
                {
                    IPanel Dock = GetDock();
                    if(Dock != null && Dock.IsActive == false)
                    {
                        Dock.ShowPanel(false);
                    }

                    List<string> ActivePanelsAfterThis = GetActiveAfterThis();

                    foreach (string PanelName in ActivePanelsAfterThis)
                    {
                        GetPanel(PanelName)?.DestroyPanel(false);
                    }

                    //ErrB.Add(this.Name + ErrB.Count,ActivePanelsAfterThis.Count);

                    if (storedData.GetPlayerSettings(Owner.UserIDString, "enable", true))
                    {
                        //Interface.Oxide.LogInfo(GetJson()); //TODO
                        Oxide.Game.Rust.Cui.CuiHelper.AddUi(this.Owner, GetJson());
                        //CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(this.Owner.net.connection), null, "AddUI", new Facepunch.ObjectList(GetJson()));
                    }

                    IsActive = true;
                    IsHidden = false;

                    if(Childs)
                    {
                        foreach (KeyValuePair<string, IPanel> Child in GetChilds().Where(p => p.Value.Autoload || p.Value.IsActive).OrderBy(s => s.Value.Order))
                        {
                            Child.Value.ShowPanel();
                        }
                    }

                    foreach (string PanelName in ActivePanelsAfterThis)
                    {
                        GetPanel(PanelName)?.ShowPanel();
                    }

                }
                else
                {
                    ShowPanelIfHidden();
                }
            }

            void ShowPanelIfHidden(bool Childs = true)
            {
                IsActive = true;
                IsHidden = true;
                if (Childs)
                {
                    foreach (KeyValuePair<string, IPanel> Child in GetChilds().Where(p => p.Value.Autoload || p.Value.IsActive).OrderBy(s => s.Value.Order))
                    {
                        Child.Value.ShowPanel();
                    }
                }
            }


            public void DestroyPanel( bool Redraw = true)
            {

                foreach (KeyValuePair<string, IPanel> Panel in GetChilds().Where(p => p.Value.IsActive))
                {
                    Panel.Value.DestroyPanel(false);
                }

                Oxide.Game.Rust.Cui.CuiHelper.DestroyUi(this.Owner, this.Name);
                //CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(this.Owner.net.connection), null, "DestroyUI", new Facepunch.ObjectList(this._Name));

                IsActive = false;

                if (Redraw)
                {
                    ReDrawPanels(GetActiveAfterThis());
                }

                IPanel Dock = GetDock();
                if(Dock?.GetChilds().Count(p => p.Value.IsActive) == 0) Dock.DestroyPanel();
            }


            public virtual void Refresh()
            {
                this.DestroyPanel();
                this.ShowPanel();
            }

            #endregion

            #region Util

            public void Remover()
            {
                foreach (KeyValuePair<string, IPanel> Child in GetChilds())
                {
                    Child.Value.Remover();
                }

                GetPanel(ParentName).Childs.Remove(this.Name);
                playerPanels[this.Owner.UserIDString].Remove(this.Name);
            }

            protected string ColorToString(Color color)
            {
                return $"{color.r} {color.g} {color.b} {color.a}";
            }

            #endregion
        }

        public class IPanelText : IPanel
        {
            public string Content
            {
                get
                {
                    return this.TextComponent.Text;
                }
                set
                {
                    this.TextComponent.Text = value;
                }
            }
            public TextAnchor Align
            {
                get
                {
                    return this.TextComponent.Align;
                }

                set
                {
                    this.TextComponent.Align = value;
                }
            }
            public int FontSize {
                get
                {
                    return this.TextComponent.FontSize;
                }
                set
                {
                    this.TextComponent.FontSize = value;
                }
            }
            public Color _FontColor = Color.white;
            public Color FontColor
            {
                get
                {
                    return this._FontColor;
                }
                set
                {
                    this._FontColor = value;
                    TextComponent.Color = $"{value.r} {value.g} {value.b} {value.a}";
                }
            }

            public CuiTextComponent TextComponent;

            public IPanelText(string Name, BasePlayer Player, Dictionary<string, Dictionary<string, IPanel>> playerPanels, Dictionary<string, Dictionary<string, IPanel>> playerDockPanels) : base(Name, Player, playerPanels, playerDockPanels)
            {
                TextComponent = new CuiTextComponent();
                Components.Insert(0, this.TextComponent);
            }

            public void RefreshText(BasePlayer player, string text)
            {
                this.DestroyPanel();
                this.Content = text;
                this.ShowPanel();
            }
        }

        public class IPanelRawImage : IPanel
        {
            public string Url
            {
                get
                {
                    return RawImageComponent.Url;
                }
                set
                {
                    RawImageComponent.Url = value;
                }
            }

            public Color _Color;
            public Color Color
            {
                get
                {
                    return this._Color;
                }
                set
                {
                    this._Color = value;
                    RawImageComponent.Color = ColorToString(value);
                }
            }

            public CuiRawImageComponent RawImageComponent;

            public IPanelRawImage(string Name, BasePlayer Player, Dictionary<string, Dictionary<string, IPanel>> playerPanels, Dictionary<string, Dictionary<string, IPanel>> playerDockPanels) : base(Name, Player, playerPanels, playerDockPanels)
            {
                RawImageComponent = new CuiRawImageComponent();
                Components.Insert(0, this.RawImageComponent);
            }
        }

        #endregion

        #region GUI

        private void DestroyGUI(BasePlayer player)
        {
            foreach (KeyValuePair<string, IPanel> Dock in PlayerDockPanels[player.UserIDString])
            {
                Dock.Value.DestroyPanel(false);
            }
        }

        void GUITimerInit(BasePlayer player)
        {
            if (player == null) return;

            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2, () => GUITimerInit(player));
            }
            else
            {
                if (!PlayerDockPanels.ContainsKey(player.UserIDString))
                {
                    LoadPanels(player);
                    InitializeGUI(player);

                    RefreshOnlinePlayers();
                }
            }
        }

        private void InitializeGUI(BasePlayer player)
        {
            if (!storedData.GetPlayerSettings(player.UserIDString, "enable", true))
            {
                return;
            }

            foreach (KeyValuePair<string, IPanel> Panel in PlayerPanels[player.UserIDString])
            {
                if (Panel.Key == "ClockText")
                {
                    ((IPanelText) Panel.Value).Content = Clock.ShowTime(player.UserIDString, storedData);
                }
                else if (Panel.Key == "OPlayersText")
                {
                    ((IPanelText) Panel.Value).Content = BasePlayer.activePlayerList.Count + "/" + ConVar.Server.maxplayers;
                }
                else if (Panel.Key == "SleepersText")
                {
                    ((IPanelText) Panel.Value).Content = BasePlayer.sleepingPlayerList.Count.ToString();
                }
                else if (Panel.Key == "MessageBoxText")
                {
                    ((IPanelText) Panel.Value).Content = MessageBox.GetMessage();
                }
                else if (Panel.Key == "CoordinatesText")
                {
                    ((IPanelText) Panel.Value).Content = Coord.GetCoord(player.UserIDString);
                }
                else if (Panel.Key == "RadiationImage")
                {
                    ((IPanelRawImage) Panel.Value).Color = Rad.ImageColor;
                }
                else if (Panel.Key == "AirdropEventImage")
                {
                    ((IPanelRawImage) Panel.Value).Color = Airplane.ImageColor;
                }
                else if (Panel.Key == "HelicopterEventImage")
                {
                    ((IPanelRawImage) Panel.Value).Color = Helicopter.ImageColor;
                }
                else if (Panel.Key == "CompassText")
                {
                    ((IPanelText) Panel.Value).Content = CompassObj.GetDirection(player.UserIDString);
                }
            }

            foreach (KeyValuePair<string, IPanel> Dock in PlayerDockPanels[player.UserIDString])
            {
                if(Dock.Value.Childs.Count != 0)
                {
                    Dock.Value.ShowPanel();
                }
            }

        }

        private void RevealGUI(BasePlayer player)
        {
            foreach (KeyValuePair<string, IPanel> Dock in PlayerDockPanels[player.UserIDString])
            {
                if (Dock.Value.Childs.Count != 0)
                {
                    Dock.Value.ShowPanel();
                }
            }
        }

        private void RefreshOnlinePlayers()
        {
            foreach (KeyValuePair<string, Dictionary<string, IPanel>> panel in PlayerPanels)
            {
                if (Settings.GetPanelSettingsValue("OPlayers", "Available", true) && panel.Value.ContainsKey("OPlayersText"))
                {
                    var panelText = (IPanelText)panel.Value["OPlayersText"];
                    panelText.Content = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}";
                    panelText.Refresh();
                }
            }
        }

        private void RefreshSleepers()
        {
            foreach (KeyValuePair<string, Dictionary<string, IPanel>> panel in PlayerPanels)
            {
                if (Settings.GetPanelSettingsValue("Sleepers", "Available", true) && panel.Value.ContainsKey("SleepersText"))
                {
                    var panelText = (IPanelText)panel.Value["SleepersText"];
                    panelText.Content = BasePlayer.sleepingPlayerList.Count.ToString();
                    panelText.Refresh();
                }
            }
        }

        #endregion

        #region API

        /// <summary>
        /// Panel beírása a configba.
        /// </summary>
        /// <param name="PluginName"></param>
        /// <param name="PanelName"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        private bool PanelRegister(string PluginName,string PanelName, string json)
        {
            List<string> loadedPlugin;
            if (LoadedPluginPanels.TryGetValue(PluginName, out loadedPlugin) && loadedPlugin.Contains(PanelName))
            {
                return true;
            }

            PanelConfig Cfg = JsonConvert.DeserializeObject<PanelConfig>(json);

            Dictionary<string, PanelConfig> thirdPartyPanel;
            if (!Settings.ThirdPartyPanels.TryGetValue(PluginName, out thirdPartyPanel))
                Settings.ThirdPartyPanels.Add(PluginName, thirdPartyPanel = new Dictionary<string, PanelConfig>());

            //Ha a plugin panelja még nem volt bejegyezve
            if (!thirdPartyPanel.ContainsKey(PanelName))
            {
                Cfg.Order = PanelReOrder(Cfg.Dock, Cfg.AnchorX);
                thirdPartyPanel.Add(PanelName, Cfg);

                Config.WriteObject(Settings, true);
                PrintWarning("New panel added to the config file: [" + PluginName + "] " + PanelName);
            }

            foreach (KeyValuePair<string,Dictionary<string,IPanel>> Docks in PlayerDockPanels)
            {
                if(Docks.Value.ContainsKey(Cfg.Dock))
                {
                    LoadPanel(Docks.Value[Cfg.Dock], PanelName, Cfg);
                }
            }

            if (!LoadedPluginPanels.TryGetValue(PluginName, out loadedPlugin))
                LoadedPluginPanels.Add(PluginName, loadedPlugin = new List<string>());
            loadedPlugin.Add(PanelName);

            return true;
        }

        private bool ShowPanel(string PluginName,string PanelName, string PlayerId = null)
        {
            if (!Settings.ThirdPartyPanels[PluginName][PanelName].Available)
            {
                return false;
            }

            if (PlayerId != null && PlayerPanels.ContainsKey(PlayerId))
            {
                PlayerPanels[PlayerId][PanelName].ShowPanel();
                return true;
            }

            foreach(string PlayerID in PlayerPanels.Keys)
            {
                PlayerPanels[PlayerID][PanelName].ShowPanel();
            }

            return true;
        }

        private bool HidePanel(string PluginName,string PanelName, string PlayerId = null)
        {
            if (!Settings.ThirdPartyPanels[PluginName][PanelName].Available)
            {
                return false;
            }

            if (PlayerId != null && PlayerPanels.ContainsKey(PlayerId))
            {
                PlayerPanels[PlayerId][PanelName].DestroyPanel();
                return true;
            }

            foreach (string PlayerID in PlayerPanels.Keys)
            {
                PlayerPanels[PlayerID][PanelName].DestroyPanel();
            }

            return true;
        }

        private bool RefreshPanel(string PluginName,string PanelName, string PlayerId = null)
        {
            if (!Settings.ThirdPartyPanels[PluginName][PanelName].Available)
            {
                return false;
            }

            if (PlayerId != null && PlayerPanels.ContainsKey(PlayerId))
            {
                PlayerPanels[PlayerId][PanelName].DestroyPanel();
                PlayerPanels[PlayerId][PanelName].ShowPanel();
                return true;
            }

            foreach (string PlayerID in PlayerPanels.Keys)
            {
                PlayerPanels[PlayerID][PanelName].DestroyPanel();
                PlayerPanels[PlayerID][PanelName].ShowPanel();
            }

            return true;
        }

        private void SetPanelAttribute(string PluginName,string PanelName, string Attribute, string Value, string PlayerId = null )
        {
            if (PlayerId != null && PlayerPanels.ContainsKey(PlayerId))
            {
                IPanel Panel = PlayerPanels[PlayerId][PanelName];
                PropertyInfo PropInfo = Panel.GetType().GetProperty(Attribute);

                if (PropInfo == null)
                {
                    PrintWarning("Wrong Attribute name: " + Attribute);
                    return;
                }

                if (Attribute == "FontColor" || Attribute == "BackgroundColor")
                {
                    PropInfo.SetValue(Panel, ColorEx.Parse(Value), null);
                }
                else if (Attribute == "Margin")
                {
                    PropInfo.SetValue(Panel, Vector4Parser(Value), null);
                }
                else
                {
                    var ConvertedValue = Convert.ChangeType(Value, PropInfo.PropertyType);

                    PropInfo.SetValue(Panel, ConvertedValue, null);
                }

                return;
            }

            foreach (string playerID in PlayerPanels.Keys)
            {
                IPanel Panel = PlayerPanels[playerID][PanelName];
                PropertyInfo PropInfo = Panel.GetType().GetProperty(Attribute);

                if (PropInfo == null)
                {
                    PrintWarning("Wrong Attribute name: " + Attribute);
                    return;
                }

                if (Attribute == "FontColor" || Attribute == "BackgroundColor")
                {
                    PropInfo.SetValue(Panel, ColorEx.Parse(Value), null);
                }
                else if (Attribute == "Margin")
                {
                    PropInfo.SetValue(Panel, Vector4Parser(Value), null);
                }
                else
                {
                    var ConvertedValue = Convert.ChangeType(Value, PropInfo.PropertyType);

                    PropInfo.SetValue(Panel, ConvertedValue, null);
                }
            }
        }

        private bool SendPanelInfo(string PluginName, List<string> Panels)
        {
           if(!Settings.ThirdPartyPanels.ContainsKey(PluginName))
            {
                return false;
            }

            List<string> Removable = Settings.ThirdPartyPanels[PluginName].Keys.Except(Panels).ToList();

            foreach(string item in Removable)
            {
                Settings.ThirdPartyPanels[PluginName].Remove(item);
            }

            if(Removable.Count > 0)
            {
                Config.WriteObject(Settings, true);
                PrintWarning("Config File refreshed! " + Removable.Count + " panel removed! [" + PluginName + "]");
            }

            return true;
        }

        private bool IsPlayerGUILoaded(string PlayerId)
        {
            return PlayerPanels.ContainsKey(PlayerId);
        }

        #endregion

        #region Utility
        internal static Vector4 Vector4Parser(string p)
        {
            string[] strArrays = p.Split(' ');
            if ((int)strArrays.Length != 4)
            {
                return Vector4.zero;
            }
            return new Vector4(float.Parse(strArrays[0]), float.Parse(strArrays[1]), float.Parse(strArrays[2]), float.Parse(strArrays[3]));
        }

        public bool IsList(object o)
        {
            if (o == null) return false;
            return o is IList &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        public static bool IsDictionary(object o)
        {
            if (o == null) return false;
            return o is IDictionary &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }
        #endregion
    }
}
