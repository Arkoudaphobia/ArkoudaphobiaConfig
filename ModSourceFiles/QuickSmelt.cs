using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QuickSmelt", "Wulf/Fujikura", "2.5.0", ResourceId = 1067)]
    [Description("Increases the speed of the furnace smelting")]

    class QuickSmelt : RustPlugin
    {
        #region Initialization

        const string permAllow = "quicksmelt.allow";
        bool overcookMeat;
        bool usePermissions;
		int charcoalPercentLoss;
		int charcoalMultiplier;
		float woodFuelAmount;
		bool cookInFurnaces;
		
		Dictionary<string, object> ovenMultipliers = new Dictionary<string, object>();
		Dictionary<string, object> cookTimes = new Dictionary<string, object>();
		Dictionary<string, object> amountsOfBecome = new Dictionary<string, object>();
		
		Dictionary<string, object> ovenDefaults()
        {
            var dp = new Dictionary<string, object>();
            var baseOvens = Resources.FindObjectsOfTypeAll<BaseOven>().Where(c => !c.isActiveAndEnabled).Cast<BaseEntity>().ToList();
			foreach (var oven in baseOvens)
			{
				if (!dp.ContainsKey(oven.ShortPrefabName))
					dp.Add(oven.ShortPrefabName, 1.0f);
			}
           return dp;
        }
		
		Dictionary<string, object> cookTimeDefaults()
        {
            var dp = new Dictionary<string, object>();
			foreach (var itemDef in ItemManager.GetItemDefinitions())
			{
				ItemModCookable component = itemDef.GetComponent<ItemModCookable>();
				if (component)
				{
					if (!overcookMeat && (component.name.Contains("cooked") || component.name.Contains("burned")))
						continue;
					if (!dp.ContainsKey(component.name.Replace(".item","")))
						dp.Add(component.name.Replace(".item",""), component.cookTime);
				}				
			}
			return dp;
		}
		
		Dictionary<string, object> amountOfBecomeDefaults()
        {
            var dp = new Dictionary<string, object>();
			foreach (var itemDef in ItemManager.GetItemDefinitions())
			{
				ItemModCookable component = itemDef.GetComponent<ItemModCookable>();
				if (component)
				{
					if (!overcookMeat && (component.name.Contains("cooked") || component.name.Contains("burned")))
						continue;
					if (!dp.ContainsKey(component.name.Replace(".item","")))
						dp.Add(component.name.Replace(".item",""), component.amountOfBecome);
				}				
			}
			return dp;
		}

        protected override void LoadDefaultConfig()
        {
            Config["OvercookMeat"] = overcookMeat = GetConfig("OvercookMeat", false);
			Config["CookInFurnaces"] = cookInFurnaces = GetConfig("CookInFurnaces", false);
            Config["UsePermissions"] = usePermissions = GetConfig("UsePermissions", false);
			Config["OvenMultipliers"] = ovenMultipliers = GetConfig("OvenMultipliers", ovenDefaults());
			Config["CookTimes"] = cookTimes = GetConfig("CookTimes", cookTimeDefaults());
			Config["AmountsOfBecome"] = amountsOfBecome = GetConfig("AmountsOfBecome", amountOfBecomeDefaults());
			Config["CharcoalPercentLoss"] = charcoalPercentLoss = GetConfig("CharcoalPercentLoss", 25);
			Config["CharcoalMultiplier"] = charcoalMultiplier = GetConfig("CharcoalMultiplier", 1);
			Config["WoodFuelAmount"] = woodFuelAmount = GetConfig("WoodFuelAmount", 10.0f);
			
			if (!overcookMeat)
			{
				foreach (var amount in amountsOfBecome.ToList())
					if (amount.Key.Contains("cooked") || amount.Key.Contains("burned"))
						amountsOfBecome.Remove(amount.Key);
				foreach (var amount in cookTimes.ToList())
					if (amount.Key.Contains("cooked") || amount.Key.Contains("burned"))
						cookTimes.Remove(amount.Key);
			}
			else
			{
				foreach (var amount in cookTimeDefaults().ToList())
					if (!cookTimes.ContainsKey(amount.Key))
						cookTimes.Add(amount.Key, amount.Value);
				foreach (var amount in amountOfBecomeDefaults().ToList())
					if (!amountsOfBecome.ContainsKey(amount.Key))
						amountsOfBecome.Add(amount.Key, amount.Value);
			}
			SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAllow, this);
        }
		
		void Unload()
		{
            foreach (var item in ItemManager.GetItemDefinitions())
            {
				ItemModBurnable component = item.GetComponent<ItemModBurnable>();
				if (component && component.name == "wood.item")
				{
					component.byproductChance = 0.25f;
					component.fuelAmount = 10f;
					component.byproductAmount = 1;
				}
				if (item.shortname.Contains("cooked") || item.shortname.Contains("raw") || item.shortname.Contains("meat.boar") || item.shortname == ("bearmeat"))
				{
					var cookable = item.GetComponent<ItemModCookable>();
					if (cookable != null)
					{
						cookable.lowTemp = 150;
						cookable.highTemp = 250;
					}
				}
            }
		}

		void OnServerInitialized()
        {
			if (ovenMultipliers == null || ovenMultipliers.Count == 0)
			{
				Config["OvenMultipliers"] = ovenMultipliers = ovenDefaults();
				SaveConfig();
			}
			
			if (!overcookMeat)
				foreach (var item in ItemManager.GetItemDefinitions())
				{
					if (item.shortname.Contains(".cooked"))
					{
						var cookable = item.GetComponent<ItemModCookable>();
						if (cookable != null)
						{
							if (cookInFurnaces)
								cookable.highTemp = 800;
							else
								cookable.highTemp = 150;
						}
					}
				}
			
			if (cookInFurnaces)
				foreach (var item in ItemManager.GetItemDefinitions())
				{
					if (item.shortname.Contains("raw") || item.shortname.Contains("meat.boar") || item.shortname == ("bearmeat"))
					{
						var cookable = item.GetComponent<ItemModCookable>();
						if (cookable != null)
						{
							cookable.lowTemp = 800;
							cookable.highTemp = 1200;
						}
					}
				}				
			
			foreach (var item in ItemManager.GetItemDefinitions())
			{
				ItemModBurnable component = item.GetComponent<ItemModBurnable>();
				if (component && component.name == "wood.item")
				{
					if (charcoalPercentLoss > 100) charcoalPercentLoss = 100;
					if (charcoalPercentLoss < 0) charcoalPercentLoss = 0;
					component.byproductChance = Convert.ToSingle(charcoalPercentLoss) / 100;
					if (woodFuelAmount < 0.1f) woodFuelAmount = 0.1f;
					component.fuelAmount = Convert.ToSingle(woodFuelAmount);
					if (charcoalMultiplier < 1) charcoalMultiplier = 1;
					component.byproductAmount = Convert.ToInt32(charcoalMultiplier);
				}
			}			

			foreach (var itemDef in ItemManager.GetItemDefinitions())
			{
				ItemModCookable component = itemDef.GetComponent<ItemModCookable>();
				if (component)
				{
					if (cookTimes.ContainsKey(component.name.Replace(".item","")))
					{
						float time = Convert.ToSingle(cookTimes[component.name.Replace(".item","")]);
						if (time < 0.1f) time = 0.1f;
						component.cookTime = time;
					}
					if (amountsOfBecome.ContainsKey(component.name.Replace(".item","")))
					{
						int amount = Convert.ToInt32(amountsOfBecome[component.name.Replace(".item","")]);
						if (amount < 1) amount = 1;
						component.amountOfBecome = amount;
					}
				}				
			}

			var baseOvens = Resources.FindObjectsOfTypeAll<BaseOven>().Where(c => c.isActiveAndEnabled).Cast<BaseEntity>().ToList();
			foreach (var oven in baseOvens)
			{
				if (usePermissions && !permission.UserHasPermission(oven.OwnerID.ToString(), permAllow))
					continue;
				if (oven.HasFlag(BaseEntity.Flags.On))
				{
					object checkMultiplier;
					if (!ovenMultipliers.TryGetValue(oven.ShortPrefabName, out checkMultiplier))
						continue;
					float ovenMultiplier = Convert.ToSingle(checkMultiplier);
					if (ovenMultiplier > 10f) ovenMultiplier = 10f;
					if (ovenMultiplier < 0.1f) ovenMultiplier = 0.1f;
					oven.CancelInvoke("Cook");
					(oven as BaseOven).inventory.temperature = CookingTemperature((oven as BaseOven).temperature);
					(oven as BaseOven).UpdateAttachmentTemperature();
					oven.InvokeRepeating("Cook", 0.5f / ovenMultiplier, 0.5f / ovenMultiplier);
				}
			}
        }

        #endregion

        #region Smelting Magic
		
		object OnOvenToggle(BaseOven oven, BasePlayer player)
		{
			if (oven.needsBuildingPrivilegeToUse && !player.CanBuild())
				return null;
			 if (usePermissions && !permission.UserHasPermission(oven.OwnerID.ToString(), permAllow))
				 return null;
			if (!oven.HasFlag(BaseEntity.Flags.On))
			{
				object checkMultiplier = null;
				if (!ovenMultipliers.TryGetValue(oven.GetComponent<BaseEntity>().ShortPrefabName, out checkMultiplier))
					return null;
				var ovenMultiplier = Convert.ToSingle(checkMultiplier);
				if (ovenMultiplier > 10f) ovenMultiplier = 10f;
				if (ovenMultiplier < 0.1f) ovenMultiplier = 0.1f;
				StartCooking(oven, oven.GetComponent<BaseEntity>(), ovenMultiplier);
				return false;
			}
			return null;
		}
		
		void StartCooking(BaseOven oven, BaseEntity entity, float ovenMultiplier)
		{
			if (FindBurnable(oven) == null)
				return;
			oven.inventory.temperature = CookingTemperature(oven.temperature);
			oven.UpdateAttachmentTemperature();
			entity.CancelInvoke("Cook");
			entity.InvokeRepeating("Cook", 0.5f / ovenMultiplier, 0.5f / ovenMultiplier);
			entity.SetFlag(BaseEntity.Flags.On, true, false);
		}
		
		float CookingTemperature(BaseOven.TemperatureType temperature)
		{
			switch (temperature)
			{
			case BaseOven.TemperatureType.Warming:
				return 50f;
			case BaseOven.TemperatureType.Cooking:
				if (cookInFurnaces)
					return 1000f;
				else
					return 200f;
			case BaseOven.TemperatureType.Smelting:
				return 1000f;
			case BaseOven.TemperatureType.Fractioning:
				return 1500f;
			default:
				return 15f;
			}
		}
		
		Item FindBurnable(BaseOven oven)
		{
			if (oven.inventory == null)
				return null;
			foreach (Item current in oven.inventory.itemList)
			{
				ItemModBurnable component = current.info.GetComponent<ItemModBurnable>();
				if (component && (oven.fuelType == null || current.info == oven.fuelType))
					return current;
			}
			return null;
		}

        #endregion

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));
    }
}
