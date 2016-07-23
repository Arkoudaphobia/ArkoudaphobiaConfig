using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NightLantern", "d3nnib", "1.2.1", ResourceId = 1182)]
    [Description("Turns ON and OFF lanterns automatically after sunrise and sunset")]
    class NightLantern : RustPlugin
    {
        
        
        /******************************************/
        /*  VARIABLES DECLARATION                 */
        /******************************************/
       
        private bool isLanternFirstEveningCheck = true;
        private bool isLanternFirstMorningCheck = true;
        private bool isLanternFirstNightCheck = true;
        public int sunriseHour;
        public int sunsetHour;
        public bool isAutoTurnLanternsEnabled;
        public bool includeJackOLanterns;
        public bool includeCeilingLight;
        public bool includeCampfires;
        protected string pluginPrefix;
        protected bool configChanged;
        


        /******************************************/
        /*  OXIDE HOOKS (Plugin Life)             */
        /******************************************/
        
        
        //-------------------------------------------------------------------------------
        // void Init():
        //-------------------------------------------------------------------------------
        // On plugin initializing sets up the plugin by checking/registering permissions
        // and initializing variables that are not already initialized.
        //-------------------------------------------------------------------------------
        void Init()
        {
            pluginPrefix = (this.Title + " v" + this.Version).ToString();
            
            loadPermissions();
            HandleConfigs();
        }
        
        
        
        //-------------------------------------------------------------------------------
        // void LoadDefaultConfig():
        //-------------------------------------------------------------------------------
        // Creates config file if it doesn't exist and populates it with deafult values.
        //-------------------------------------------------------------------------------
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            Config.Clear();
            HandleConfigs();
        }
        
        
        //-------------------------------------------------------------------------------
        // void OnTick():
        //-------------------------------------------------------------------------------
        // If automatic control of lanterns is enabled in config file 
        // (AutoTurnLanterns: true by default) rules the Day/Night cycle to control 
        // lanterns every server tick.
        //-------------------------------------------------------------------------------
        void OnTick()
        {
            if (isAutoTurnLanternsEnabled)
            {
                dayNightCycle();
            }
        }
        
        
        //-------------------------------------------------------------------------------
        // void OnItemDeployed():
        //-------------------------------------------------------------------------------
        // When and object it's deployed checks if it is a lantern, if it's night time
        // and if automatic control of lanterns is enabled.
        // If all conditions are true, this lantern is automatically turned on.
        //-------------------------------------------------------------------------------
        void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            bool status = false;
            double currentTime = TOD_Sky.Instance.Cycle.Hour;
            
            if (deployedEntity.ShortPrefabName == "lantern.deployed"
               || ( (deployedEntity.ShortPrefabName == "ceilinglight.deployed") && includeCeilingLight) 
               || ( (deployedEntity.ShortPrefabName == "jackolantern.happy") && includeJackOLanterns) 
               || ( (deployedEntity.ShortPrefabName == "jackolantern.angry") && includeJackOLanterns)
               || ( (deployedEntity.ShortPrefabName == "campfire") && includeCampfires)
               ) 
            {
                
                if ( currentTime < sunriseHour && isAutoTurnLanternsEnabled || currentTime >= sunsetHour && isAutoTurnLanternsEnabled )
                    {
                        status = true;
                    }
                
                deployedEntity.SetFlag(BaseEntity.Flags.On, status);

            }
        }
        
        
        
        /******************************************/
        /*  METHODS DECLARATION                   */
        /******************************************/
        
        //-------------------------------------------------------------------------------
        // void turnLanterns(bool status):
        //-------------------------------------------------------------------------------
        // Create a list of all server Baseovens, cycles through it and each lantern
        // it's turned ON or OFF based on boolean status variable passed to the function.
        //-------------------------------------------------------------------------------
        private void turnLanterns(bool status) {
            
            List<BaseOven> ovens = Component.FindObjectsOfType<BaseOven>().ToList();
            foreach (BaseOven oven in ovens)
            {
                if (oven.ShortPrefabName == "lantern.deployed"
                   || ( (oven.ShortPrefabName == "ceilinglight.deployed") && includeCeilingLight)
                   || ( (oven.ShortPrefabName == "jackolantern.happy") && includeJackOLanterns)
                   || ( (oven.ShortPrefabName == "jackolantern.angry") && includeJackOLanterns)
                   || ( (oven.ShortPrefabName == "campfire") && includeCampfires)
                   )
                {
                    oven.SetFlag(BaseEntity.Flags.On, status);

                }
            }
            
        }
        
        //-------------------------------------------------------------------------------
        // void dayNightCycle():
        //-------------------------------------------------------------------------------
        // Plugin Core Function: Checks in-game hour and calls turnLanterns() function
        // to turn lanterns ON or OFF based on it.
        // Sunset and sunrise hours are taken from config file.
        // turnLanterns() functions is called only once for (first tick of) morning, 
        // evening and night changing the value of variables:
        // isLanternFirstMorningCheck, isLanternFirstEveningCheck and isLanternFirstNightCheck
        //-------------------------------------------------------------------------------
        private void dayNightCycle() 
        {
            
            double currentTime = TOD_Sky.Instance.Cycle.Hour;
            
            if ( currentTime >= sunsetHour && isLanternFirstEveningCheck )
            {
                turnLanterns(true);
                isLanternFirstMorningCheck = true;
                isLanternFirstEveningCheck = false;
                isLanternFirstNightCheck = true;
                
                myPuts("All lanterns turned ON");
            }
            else if ( currentTime >= sunriseHour && currentTime < sunsetHour && isLanternFirstMorningCheck )
            {
                turnLanterns(false);
                isLanternFirstMorningCheck = false;
                isLanternFirstEveningCheck = true;
                isLanternFirstNightCheck = true;
                
                myPuts("All lanterns turned OFF");
            }
            else if (  currentTime < sunriseHour && isLanternFirstNightCheck )
            {
                turnLanterns(true);
                isLanternFirstMorningCheck = true;
                isLanternFirstEveningCheck = true;
                isLanternFirstNightCheck = false;
                
            }
        
        }
        
        
        //-------------------------------------------------------------------------------
        // object GetConfig(string menu, string datavalue, object defaultValue)
        //-------------------------------------------------------------------------------
        // Manages config values by creating sections in config file (string menu)
        // and populates them with key/value values passed to the function.
        // as Dictionary<string, object>
        // Returns the value for the passed key which is the config value if present, or
        // default value otherwise.
        //-------------------------------------------------------------------------------
        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                configChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                configChanged = true;
            }
            return value;
        }
        
        
        //-------------------------------------------------------------------------------
        // void HandleConfigs():
        //-------------------------------------------------------------------------------
        // Calls GetConfig() function for each parameter to insert in config file.
        // Initialize necessary variables with config values and writes them down
        // to the config file if necessary.
        //-------------------------------------------------------------------------------
        private void HandleConfigs()
        {
            sunriseHour = Convert.ToInt32(GetConfig("Settings", "sunriseHour", "7"));
            sunsetHour = Convert.ToInt32(GetConfig("Settings", "sunsetHour", "18"));
            isAutoTurnLanternsEnabled = Convert.ToBoolean(GetConfig("Settings", "AutoTurnLanterns", true));
			includeCeilingLight = Convert.ToBoolean(GetConfig("Settings", "includeCeilingLight", true));
            includeJackOLanterns = Convert.ToBoolean(GetConfig("Settings", "includeJackOLanterns", true));
            includeCampfires = Convert.ToBoolean(GetConfig("Settings", "includeCampfires", false));
            
            if (configChanged)
            {
                SaveConfig();
            }
            
        }
        
        
        
        
        /******************************************/
        /*  CHAT COMMANDS                         */
        /******************************************/
        
        
        //-------------------------------------------------------------------------------
        // void chatCommand_LantON(BasePlayer player, string command, string[] args)
        //-------------------------------------------------------------------------------
        // Chat command lantON: turn ON all lanterns if user has "nightlantern.lanterns" 
        // permission.
        //-------------------------------------------------------------------------------  
        [ChatCommand("lant")]
        void chatCommand_Lant(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "nightlantern.lanterns"))
            {
                return;
            }
            
            if (args.Length == 0) 
            {
                PrintToChat(player, "Invalid command: you have to type /lant <ON|OFF>");
            }
            
            else 
            {
                string parametro = args[0].ToLower();
                
                if (parametro == "on")
                {
                    turnLanterns(true);
                    myPuts("All lanterns turned ON");
                }
                else if (parametro == "off")
                {
                    turnLanterns(false);
                    myPuts("All lanterns turned OFF");
                }
                else 
                {
                    PrintToChat(player, "Invalid command: you have to type /lant <ON|OFF>");
                }
            }
            
            
            
        }
        
            
        
        /******************************************/
        /* PERMISSIONS: Checkings and Registering */
        /******************************************/
        
        
        //-------------------------------------------------------------------------------
        // void loadPermissions()
        //-------------------------------------------------------------------------------
        // Checks if "CanControlLanterns" custom permission is already registered, if not
        // it registers it.
        // The permission is then assigned to owner group by default.
        //------------------------------------------------------------------------------- 
        private void loadPermissions() {
            
            if (!permission.PermissionExists("nightlantern.lanterns"))
            {
                permission.RegisterPermission("nightlantern.lanterns", this);
                
            }
            
            permission.GrantGroupPermission("owner", "nightlantern.lanterns", this);
            
            

        }
        
        
        //-------------------------------------------------------------------------------
        // bool IsAllowed(BasePlayer player, string perm)
        //-------------------------------------------------------------------------------
        // Checks if current user has the passed permission, if true return true, else it
        // returns false and send a chat alert to the player.
        //------------------------------------------------------------------------------- 
        private bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            PrintToChat(player, "You're not allowed to use this command");
            return false;
        }

        
        /******************************************/
        /*  TOOLBOX PART                          */
        /******************************************/
        
        //-------------------------------------------------------------------------------
        // void myPuts(string message)
        //-------------------------------------------------------------------------------
        // A customized version of oxide Puts, it returns in console the passed string
        // with a prefix containing plugin title and version number.
        //------------------------------------------------------------------------------- 
        protected void myPuts(string message)
        {
            Interface.Oxide.LogInfo(String.Format("[{0}] {1}", pluginPrefix, message));
        }
        


    }
}