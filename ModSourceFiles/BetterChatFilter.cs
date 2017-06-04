// Requires: BetterChat
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("BetterChatFilter", "LaserHydra & jemes.", "1.0.1", ResourceId = 2403)]
    [Description("Filter for Better Chat")]
    public class BetterChatFilter : CovalencePlugin
    {   
		
        [PluginReference("BetterChat")]
        private Plugin bcLib;
		
		#region Cached Variables

        bool WordFilter_Enabled;
        string WordFilter_Replacement;
        bool WordFilter_UseCustomReplacement;
        string WordFilter_CustomReplacement;
        List<object> WordFilter_Phrases;

        #endregion
		
		#region Plugin General
		
		void Loaded()
		{
			LoadConfig();
		}
		
        new void LoadConfig()
        {
            SetConfig("Word Filter", "Enabled", true);
            SetConfig("Word Filter", "Replacement", "*");
            SetConfig("Word Filter", "Custom Replacement", "Unicorn");
            SetConfig("Word Filter", "Use Custom Replacement", false);
            SetConfig("Word Filter", "Phrases", new List<object> {
                "bitch",
                "cunt",
                "fag",
                "nigger",
                "faggot",
                "fuck"
            });

            SaveConfig();

            //////////////////////////////////////////////////////////////////////////////////

            WordFilter_Enabled = GetConfig(true, "Word Filter", "Enabled");
            WordFilter_Replacement = GetConfig("*", "Word Filter", "Replacement");
            WordFilter_UseCustomReplacement = GetConfig(false, "Word Filter", "Use Custom Replacement");
            WordFilter_CustomReplacement = GetConfig("Unicorn", "Word Filter", "Custom Replacement");
            WordFilter_Phrases = GetConfig(new List<object> {
                "bitch",
                "cunt",
                "fag",
                "nigger",
                "faggot",
                "fuck"
            }, "Word Filter", "Phrases");
        }
			
		protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");
		
	  	#endregion 	
		
        //////////////////////////////////////////////////////////////////////////////////
		
        #region BetterChatHook
		
        object OnBetterChat(Dictionary<string, object> messageData)
        {
            if (GetConfig(false, "Word Filter", "Enabled"))
			{
                string message = (string)messageData["Text"];
				messageData["Text"] = FilterText(message);
				return messageData;
			} 
			else 
			{
				return null;	
			}
        }

        #endregion
		
        //////////////////////////////////////////////////////////////////////////////////
        
		#region Word Filter

        string FilterText(string original)
        {
            string filtered = original;

            foreach (string word in original.Split(' '))
                foreach (string bannedword in WordFilter_Phrases)
                    if (TranslateLeet(word).ToLower().Contains(bannedword.ToLower()))
                        filtered = filtered.Replace(word, Replace(word));

            return filtered;
        }

        string Replace(string original)
        {
            string filtered = string.Empty;

            if (!WordFilter_UseCustomReplacement)
                for (; filtered.Count() < original.Count();)
                    filtered += WordFilter_Replacement;
            else
                filtered = WordFilter_CustomReplacement;

            return filtered;
        }

        string TranslateLeet(string original)
        {
            string translated = original;

            Dictionary<string, string> leetTable = new Dictionary<string, string>
            {
                { "}{", "h" },
                { "|-|", "h" },
                { "]-[", "h" },
                { "/-/", "h" },
                { "|{", "k" },
                { "/\\/\\", "m" },
                { "|\\|", "n" },
                { "/\\/", "n" },
                { "()", "o" },
                { "[]", "o" },
                { "vv", "w" },
                { "\\/\\/", "w" },
                { "><", "x" },
                { "2", "z" },
                { "4", "a" },
                { "@", "a" },
                { "8", "b" },
                { "ß", "b" },
                { "(", "c" },
                { "<", "c" },
                { "{", "c" },
                { "3", "e" },
                { "€", "e" },
                { "6", "g" },
                { "9", "g" },
                { "&", "g" },
                { "#", "h" },
                { "$", "s" },
                { "7", "t" },
                { "|", "l" },
                { "1", "i" },
                { "!", "i" },
                { "0", "o" },
            };

            foreach (var leet in leetTable)
                translated = translated.Replace(leet.Key, leet.Value);

            return translated;
        }
        #endregion
		
        //////////////////////////////////////////////////////////////////////////////////
		
        #region Convert Helper

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());

        #endregion
		
        #region Data & Config Helper

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config. Please delete your config and reload the Plugin. If this does not work, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////////

	}
}

