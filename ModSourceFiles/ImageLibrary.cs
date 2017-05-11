﻿//Reference: Rust.Workshop
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core;
using System.IO;
using System.Collections;
using Oxide.Core.Plugins;
using Rust;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("ImageLibrary", "Absolut", "1.8.1", ResourceId = 2193)]

    class ImageLibrary : RustPlugin
    {
        #region Fields

        ImageData imageData;
        private DynamicConfigFile ImageLibraryData;
        bool WorkshopDone;
        bool WorkshopLoading;
        bool BasicDone;
        public class ImageData
        {
            public uint CommID;
            public Dictionary<string, Dictionary<ulong, uint>> Images = new Dictionary<string, Dictionary<ulong, uint>>();
            public Dictionary<string, Dictionary<ulong, SkinInfo>> ItemSkinInfo = new Dictionary<string, Dictionary<ulong, SkinInfo>>();
        }
        public class SkinInfo
        {
            public string Title;
            public uint VotesUp;
            public uint VotesDown;
            public float Score;
            public int Views;
            public string Description;
        }
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        public List<string> UnableToFindImageList = new List<string>();
        static GameObject webObject;
        static Images images;
        Dictionary<string, string> ItemLists = new Dictionary<string, string>();
        #endregion

        #region Hooks   

        void Loaded()
        {
            ImageLibraryData = Interface.Oxide.DataFileSystem.GetFile("ImageLibrary_Data");
            WorkshopDone = false;
            BasicDone = false;
        }

        void Unload()
        {
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
            UnableToFindImageList.Clear();
            SaveData();
        }

        void OnServerInitialized()
        {
            webObject = new GameObject("WebObject");
            images = webObject.AddComponent<Images>();
            images.SetDataDir(this);
            LoadVariables();
            LoadData();
            InitializeItemList();
            RefreshImages();
            timers.Add("clear", timer.Once(300, () => ClearImageNotFoundList()));
        }

        void OnPlayerInit(BasePlayer player)
        {
            GetPlayerAvatar(player.userID);
        }

        string GetPluginName(ulong resourceId)
        {
            foreach (var entry in plugins.GetAll().Where(k => k.ResourceId == (int)resourceId))
                return entry.Name;
            return "";
        }

        private void ClearImageNotFoundList()
        {
            if (timers.ContainsKey("clear"))
            {
                timers["clear"].Destroy();
                timers.Remove("clear");
            }
            UnableToFindImageList.Clear();
            timers.Add("clear", timer.Once(300, () => ClearImageNotFoundList()));
        }

        static readonly Regex Regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");
        void GetPlayerAvatar(ulong ID)
        {
            webrequest.EnqueueGet($"http://steamcommunity.com/profiles/{ID}?xml=1", (code, response) =>
            {
                string avatar = null;
                if (response != null && code == 200)
                {
                    avatar = Regex.Match(response).Groups[1].ToString();
                    images.Add(avatar, ID.ToString(), 0);
                }
            }, this);
        }


        #endregion

        #region External Calls

        [HookMethod("GetSkinInfo")]
        public object GetSkinInfo(string shortname,ulong id, string request)
        {
            if (!imageData.ItemSkinInfo.ContainsKey(shortname) || !imageData.ItemSkinInfo[shortname].ContainsKey(id)) return false;
            switch (request.ToLower())
            {
                case "title":
                        return imageData.ItemSkinInfo[shortname][id].Title;
                case "votesdown":
                    return imageData.ItemSkinInfo[shortname][id].VotesDown;
                case "votesup":
                    return imageData.ItemSkinInfo[shortname][id].VotesUp;
                case "score":
                    return imageData.ItemSkinInfo[shortname][id].Score;
                case "views":
                    return imageData.ItemSkinInfo[shortname][id].Views;
                case "description":
                    return imageData.ItemSkinInfo[shortname][id].Description;
                default:
                    return false;
            }
        }

        [HookMethod("isReady")]
        public bool isReady()
        {
            if (WorkshopDone && BasicDone) return true;
            else return false;
        }


        //[HookMethod("GetImageURL")]
        //public string GetImageURL(string shortname, ulong skin = 0)
        //{
        //    shortname = shortname.ToLower();
        //    if (!imageData.ImageURLs.ContainsKey(shortname)) return imageData.ImageURLs["NONE"][0].ToString();
        //    if (!imageData.ImageURLs[shortname].ContainsKey(skin))
        //        return imageData.ImageURLs["NONE"][0].ToString();
        //    return imageData.ImageURLs[shortname][skin].ToString();
        //}

        [HookMethod("GetImage")]
        public string GetImage(string shortname, ulong skin = 0)
        {
            shortname = shortname.ToLower();
            if (!imageData.Images.ContainsKey(shortname))
            {
                if (UnableToFindImageList.Contains(shortname)) return imageData.Images["NONE"][0].ToString();
                UnableToFindImageList.Add(shortname);
                if (GetPluginName(skin) != "")
                {
                    try
                    {
                        var pluginfolder = GetPluginName(skin);
                        string path = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}{pluginfolder}{ Path.DirectorySeparatorChar}Images{ Path.DirectorySeparatorChar}";
                        images.Add(path + shortname + ".png", shortname, skin);
                        return imageData.Images[shortname][skin].ToString();
                    }
                    catch
                    {
                    }
                }
                return imageData.Images["NONE"][0].ToString();
            }
            if (!imageData.Images[shortname].ContainsKey(skin))
            {
                if (UnableToFindImageList.Contains(shortname)) return imageData.Images["NONE"][0].ToString();
                UnableToFindImageList.Add(shortname);
                //Puts("NOT FOUND - Skin");
                if (GetPluginName(skin) != "")
                {
                    try
                    {
                        var pluginfolder = GetPluginName(skin);
                        string path = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}{pluginfolder}{ Path.DirectorySeparatorChar}Images{ Path.DirectorySeparatorChar}";
                        images.Add(path + shortname + ".png", shortname, skin);
                        return imageData.Images[shortname][skin].ToString();
                    }
                    catch
                    {
                    }
                }
                return imageData.Images["NONE"][0].ToString();
            }
            //Puts("FOUND");
            return imageData.Images[shortname][skin].ToString();
        }

        [HookMethod("GetImageList")]
        public List<ulong> GetImageList(string shortname)
        {
            shortname = shortname.ToLower();
            if (!imageData.Images.ContainsKey(shortname)) return null;
            List<ulong> images = new List<ulong>();
            foreach (var entry in imageData.Images[shortname])
                images.Add(entry.Key);
            return images;
        }


        [HookMethod("HasImage")]
        public bool HasImage(string shortname, ulong skin = 0)
        {
            shortname = shortname.ToLower();
            if (!imageData.Images.ContainsKey(shortname)) return false;
            if (!imageData.Images[shortname].ContainsKey(skin))
                return false;
            return true;
        }

        [HookMethod("AddImage")]
        public bool AddImage(string url, string name, ulong skin = 0)
        {
            name = name.ToLower();
            try
            {
                if (url == "local")
                {
                    if (GetPluginName(skin) != "")
                    {
                        var pluginfolder = GetPluginName(skin);
                        string path = $"file://{Interface.Oxide.DataDirectory}{Path.DirectorySeparatorChar}{pluginfolder}{ Path.DirectorySeparatorChar}Images{ Path.DirectorySeparatorChar}";
                        images.Add(path + name + ".png", name, skin);
                    }
                }
                else if (!imageData.Images.ContainsKey(name))
                    images.Add(url, name, skin);
                else
                {
                    if (imageData.Images[name].ContainsKey(skin))
                        imageData.Images[name].Remove(skin);
                    images.Add(url, name, skin);
                }
                return true;
            }
            catch
            {
                Puts("An error occured while trying to add an image. Check the Image Data File for corruption");
                return false;
            }
        }
        #endregion

        #region Images
        class QueueImages
        {
            public string url;
            public string name;
            public ulong skin;
            public QueueImages(string ur, string nm, ulong sk)
            {
                url = ur;
                name = nm;
                skin = sk;
            }
        }

        class Images : MonoBehaviour
        {
            ImageLibrary filehandler;
            const ulong MaxActiveLoads = 10;
            static readonly List<QueueImages> QueueList = new List<QueueImages>();
            static byte activeLoads;
            private void Awake() => filehandler = (ImageLibrary)Interface.Oxide.RootPluginManager.GetPlugin(nameof(ImageLibrary));
            private void OnDestroy()
            {
                QueueList.Clear();
                filehandler = null;
            }
            private MemoryStream stream = new MemoryStream();

            public void SetDataDir(ImageLibrary fc) => filehandler = fc;
            public void Add(string url, string name, ulong skin)
            {
                //if (url.Contains("http"))
                //{
                //    if (!filehandler.imageData.ImageURLs.ContainsKey(name))
                //        filehandler.imageData.ImageURLs.Add(name, new Dictionary<ulong, string>());
                //    if (!filehandler.imageData.ImageURLs[name].ContainsKey(skin))
                //        filehandler.imageData.ImageURLs[name].Add(skin, url);
                //    else filehandler.imageData.ImageURLs[name][skin] = url;
                //}
                QueueList.Add(new QueueImages(url, name, skin));
                if (activeLoads < MaxActiveLoads) Next();
            }

            void Next()
            {
                activeLoads++;
                var qi = QueueList[0];
                QueueList.RemoveAt(0);
                var www = new WWW(qi.url);
                StartCoroutine(WaitForRequest(www, qi));
            }

            private void ClearStream()
            {
                stream.Position = 0;
                stream.SetLength(0);
            }

            byte[] GetImageBytes(WWW www)
            {
                var tex = www.texture;
                byte[] img;
                img = www.bytes;
                DestroyImmediate(tex);
                return img;
            }

            IEnumerator WaitForRequest(WWW www, QueueImages info)
            {
                yield return www;
                if (www.error != null)
                {
                    print(string.Format("Image loading fail! Error: {0}", www.error));
                }
                else
                {
                    if (!filehandler.imageData.Images.ContainsKey(info.name))
                        filehandler.imageData.Images.Add(info.name, new Dictionary<ulong, uint>());
                    if (!filehandler.imageData.Images[info.name].ContainsKey(info.skin))
                        filehandler.imageData.Images[info.name].Add(info.skin, 0);
                    {
                        ClearStream();
                        byte[] image = GetImageBytes(www);
                        stream.Write(image, 0, image.Length);
                        uint textureID = FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        ClearStream();
                        filehandler.imageData.Images[info.name][info.skin] = textureID;
                    }
                }
                activeLoads--;
                if (QueueList.Count > 0) Next();
                else if (filehandler.WorkshopDone && filehandler.BasicDone)
                    filehandler.SaveData();
                else if (!filehandler.BasicDone)
                {
                    filehandler.BasicDone = true;
                    if (!filehandler.configData.UseWorkshopImages)
                    {
                        filehandler.WorkshopDone = true;
                        filehandler.Puts("All Images Done Loading");
                        filehandler.SaveData();
                    }
                }
                else if (!filehandler.WorkshopLoading && !filehandler.WorkshopDone && filehandler.BasicDone)
                {
                    filehandler.WorkshopDone = true;
                    filehandler.timer.Once(20, () => { filehandler.Puts("All Images Done Loading"); filehandler.SaveData(); });
                }
            }
        }

        [ConsoleCommand("RefreshAllImages")]
        private void cmdRefreshAllImages(ConsoleSystem.Arg arg)
        {
            RefreshAllImages();
        }

        private void RefreshAllImages()
        {
            BasicDone = false;
            WorkshopDone = false;
            InitializeItemList();
            imageData.Images.Clear();
            imageData.CommID = 0;
            Puts("All Images Wiped!");
            RefreshImages();
        }

        [ConsoleCommand("WorkshopImages")]
        private void cmdWorkshopImages(ConsoleSystem.Arg arg)
        {
            WorkshopDone = false;
            ServerMgr.Instance.StartCoroutine(GetWorkshopSkins());
            Puts("Loading Workshop Images");
        }

        [ConsoleCommand("RefreshImages")]
        private void cmdRefreshImages(ConsoleSystem.Arg arg)
        {
            RefreshImages();
        }

        void InitializeItemList()
        {
            ItemLists.Clear();
            foreach (var item in ItemManager.itemList)
            {
                ItemLists.Add(item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", ""), item.shortname);
            }
            ItemLists.Add("longtshirt", ItemManager.FindItemDefinition("tshirt.long").shortname);
            ItemLists.Add("cap", ItemManager.FindItemDefinition("hat.cap").shortname);
            ItemLists.Add("beenie", ItemManager.FindItemDefinition("hat.beenie").shortname);
            ItemLists.Add("boonie", ItemManager.FindItemDefinition("hat.boonie").shortname);
            ItemLists.Add("balaclava", ItemManager.FindItemDefinition("mask.balaclava").shortname);
            ItemLists.Add("pipeshotgun", ItemManager.FindItemDefinition("shotgun.waterpipe").shortname);
            ItemLists.Add("woodstorage", ItemManager.FindItemDefinition("box.wooden").shortname);
            ItemLists.Add("ak47", ItemManager.FindItemDefinition("rifle.ak").shortname);
            ItemLists.Add("boltrifle", ItemManager.FindItemDefinition("rifle.bolt").shortname);
            ItemLists.Add("bandana", ItemManager.FindItemDefinition("mask.bandana").shortname);
            ItemLists.Add("snowjacket", ItemManager.FindItemDefinition("jacket.snow").shortname);
            ItemLists.Add("buckethat", ItemManager.FindItemDefinition("bucket.helmet").shortname);
            ItemLists.Add("semiautopistol", ItemManager.FindItemDefinition("pistol.semiauto").shortname);
            ItemLists.Add("burlapgloves", ItemManager.FindItemDefinition("burlap.gloves").shortname);
            ItemLists.Add("roadsignvest", ItemManager.FindItemDefinition("roadsign.jacket").shortname);
            ItemLists.Add("roadsignpants", ItemManager.FindItemDefinition("roadsign.kilt").shortname);
            ItemLists.Add("burlappants", ItemManager.FindItemDefinition("burlap.trousers").shortname);
            ItemLists.Add("collaredshirt", ItemManager.FindItemDefinition("shirt.collared").shortname);
            ItemLists.Add("mp5", ItemManager.FindItemDefinition("smg.mp5").shortname);
            ItemLists.Add("sword", ItemManager.FindItemDefinition("longsword").shortname);
            ItemLists.Add("workboots", ItemManager.FindItemDefinition("shoes.boots").shortname);
            ItemLists.Add("vagabondjacket", ItemManager.FindItemDefinition("jacket").shortname);
            ItemLists.Add("hideshoes", ItemManager.FindItemDefinition("shoes.boots").shortname);
            ItemLists.Add("deerskullmask", ItemManager.FindItemDefinition("deer.skull.mask").shortname);
            ItemLists.Add("minerhat", ItemManager.FindItemDefinition("hat.miner").shortname);
            ItemLists.Add("hideshirt", ItemManager.FindItemDefinition("shirt.tanktop").shortname);
        }

        private void RefreshImages()
        {
            if (!string.IsNullOrEmpty(configData.MapURL))
                AddImage(configData.MapURL, "worldmap");
            images.Add("http://i.imgur.com/sZepiWv.png", "NONE", 0);
            //Puts($"Current ID: {imageData.CommID} - New Comm: {CommunityEntity.ServerInstance.net.ID}");
            if (imageData.CommID == CommunityEntity.ServerInstance.net.ID) { BasicDone = true; WorkshopDone = true; return; }
            Puts("Server Instance ID has changed! Loading Basic Images");
            imageData.Images.Clear();
            imageData.CommID = CommunityEntity.ServerInstance.net.ID;
            webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", (code, response) =>
            {
                if (!(response == null && code == 200))
                {
                    var schm = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                    var items = schm.items;
                    var defs = new List<Facepunch.Steamworks.Inventory.Definition>();
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrEmpty(item.itemshortname) && !string.IsNullOrEmpty(item.icon_url))
                            images.Add(item.icon_url_large, item.itemshortname, item.itemdefid);
                    }
                }
            }, this);
            foreach (var entry in ItemImages)
                foreach (var item in entry.Value.Where(k => k.Key == 0))
                    images.Add(item.Value, entry.Key, item.Key);
            if(configData.UseWorkshopImages)
                ServerMgr.Instance.StartCoroutine(GetWorkshopSkins());
        }

        public IEnumerator GetWorkshopSkins()
        {
            while (!BasicDone) yield return null;
            Puts("Basic Images Loaded");
            Puts("Loading Workshop Images");
            WorkshopLoading = true;
            var workshopQuery = Global.SteamServer.Workshop.CreateQuery();
            workshopQuery.Page = 1;
            workshopQuery.PerPage = 50000;
            workshopQuery.RequireTags.Add("Version3");
            workshopQuery.Run();

            yield return new WaitWhile(new System.Func<bool>(() => workshopQuery.IsRunning));
            bool flag = false;
            foreach (var item in workshopQuery.Items)
            {
                string itemshortname = null;
                flag = false;
                foreach (var tag in item.Tags)
                {
                    string removeskin = tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                    if (ItemLists.ContainsKey(removeskin))
                    {
                        itemshortname = ItemLists[removeskin];
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    continue;
                }
                if (!imageData.ItemSkinInfo.ContainsKey(itemshortname))
                    imageData.ItemSkinInfo.Add(itemshortname, new Dictionary<ulong, SkinInfo>());
                if (!imageData.ItemSkinInfo[itemshortname].ContainsKey(item.Id))
                    imageData.ItemSkinInfo[itemshortname].Add(item.Id, new SkinInfo { Title = item.Title, VotesUp = item.VotesUp, VotesDown = item.VotesDown, Description = item.Description, Score = item.Score, Views = item.WebsiteViews });
                images.Add(item.PreviewImageUrl.ToString(),itemshortname, item.Id);
            }
            workshopQuery.Dispose();
            WorkshopLoading = false;
        }
        #endregion

        #region Image Urls
        private Dictionary<string, Dictionary<ulong, string>> ItemImages = new Dictionary<string, Dictionary<ulong, string>>
        {
                { "tshirt", new Dictionary<ulong, string>
                {
                {0, "http://imgur.com/SAD8dWX.png" },
                {10130, "http://imgur.com/tqwRCKw.png"},
                {10033, "http://imgur.com/UjGqhac.png" },
                {10003, "http://imgur.com/Q2w1w74.png"},
                {14177, "http://imgur.com/wuj2TnQ.png" },
                {10056, "http://imgur.com/2lfKuYz.png"},
                {14181, "http://imgur.com/MgRHg0D.png" },
                {10024, "http://imgur.com/C0IH5q0.png"},
                {10035, "http://imgur.com/Vh9yCpv.png" },
                {10046, "http://imgur.com/r4EZ4X5.png"},
                {10038, "http://imgur.com/tSWGLIo.png" },
                {101, "http://imgur.com/iY3zqU3.png" },
                {10025, "http://imgur.com/6s4nmz6.png" },
                {10002, "http://imgur.com/2CwEo5f.png"},
                {10134, "http://imgur.com/bgAgtiN.png" },
                {10131, "http://imgur.com/QBDtZZt.png"},
                {10041, "http://imgur.com/ZWIFX0J.png" },
                {10053, "http://imgur.com/JzPIjvu.png"},
                {10039, "http://imgur.com/2e6RlNV.png" },
                {584379, "http://imgur.com/QGo7psZ.png"},
                {10043, "http://imgur.com/4oz5N6s.png" },
                }
            },
            {"pants", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/iiFJAso.png" },
                {10001, "http://imgur.com/ntwPM8B.png"},
                {10049, "http://imgur.com/UroE7FB.png" },
                {10019, "http://imgur.com/e4lMi7b.png"},
                {10078, "http://imgur.com/GtYg84o.png" },
                {10048, "http://imgur.com/NFpjEVG.png"},
                {10021, "http://imgur.com/zVQSCOM.png" },
                {10020, "http://imgur.com/jrILSlp.png" },
            }
            },
            {"shoes.boots", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/b8HJ3TJ.png" },
                {10080, "http://imgur.com/7LSy7LN.png"},
                {10023, "http://imgur.com/JWk9YKb.png" },
                {10088, "http://imgur.com/RRFrv7d.png"},
                {10034, "http://imgur.com/wkYqkDd.png" },
                {10044, "http://imgur.com/2b01wU2.png"},
                {10022, "http://imgur.com/CCqzvRr.png" },
            }
            },
             {"tshirt.long", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/KPxtIQI.png" },
                {10047, "http://imgur.com/S8H3tcI.png"},
                {10004, "http://imgur.com/e28NFqe.png" },
                {10089, "http://imgur.com/fg1o3bI.png"},
                {10106, "http://imgur.com/QCTgWL8.png" },
                {10050, "http://imgur.com/d7VWfRi.png"},
                {10032, "http://imgur.com/mWO2yFG.png" },
                {10005, "http://imgur.com/9x9r5nv.png"},
                {10125, "http://imgur.com/JF2G3Bo.png" },
                {10118, "http://imgur.com/MxfnH0L.png"},
                {10051, "http://imgur.com/PNQbN6q.png" },
                {10006, "http://imgur.com/mq1o74X.png"},
                {10036, "http://imgur.com/kHA82wu.png" },
                {10042, "http://imgur.com/gVSKubo.png" },
                {10007, "http://imgur.com/Nddd4yq.png"},
            }
            },
             {"mask.bandana", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/PImuCst.png" },
                {10061, "http://imgur.com/6z7XFqf.png"},
                {10060, "http://imgur.com/RvaahST.png" },
                {10067, "http://imgur.com/AXu92sd.png"},
                {10104, "http://imgur.com/eb2WQMJ.png" },
                {10066, "http://imgur.com/e2UwJ5L.png"},
                {10063, "http://imgur.com/u7cupKO.png" },
                {10059, "http://imgur.com/wJF8J0l.png"},
                {10065, "http://imgur.com/FVDQu9Y.png" },
                {10064, "http://imgur.com/k71r2Zq.png"},
                {10062, "http://imgur.com/TtSFbRI.png" },
                {10079, "http://imgur.com/hBW2DeR.png"},
            }
            },
             {"mask.balaclava", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BYFgE5c.png" },
                {10105, "http://imgur.com/aZ24Prz.png"},
                {10069, "http://imgur.com/lZRsgUO.png"},
                {10071, "http://imgur.com/yqrwGPA.png" },
                {10068, "http://imgur.com/NSXHTJ8.png"},
                {10057, "http://imgur.com/qq0Kkf8.png" },
                {10075, "http://imgur.com/c52VsFb.png"},
                {10070, "http://imgur.com/qnu8n2a.png" },
                {10054, "http://imgur.com/QZCZVSP.png"},
                {10090, "http://imgur.com/1ngWJs4.png" },
                {10110, "http://imgur.com/4e4Jups.png"},
                {10084, "http://imgur.com/TXqTQBd.png" },
                {10139, "http://imgur.com/3hJfzEV.png"},
                {10111, "http://imgur.com/0Kl5Dcu.png" },
            }
            },
             {"jacket.snow", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/32ZO3jO.png" },
                {10082, "http://imgur.com/8jqmVOg.png"},
                {10113, "http://imgur.com/t2WQFcw.png" },
                {10083, "http://imgur.com/1lEjT1g.png"},
                {10112, "http://imgur.com/fdTvghu.png" },
            }
            },
             {"jacket", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/zU7TQPR.png" },
                {10011, "http://imgur.com/1qLvjuy.png"},
                {10012, "http://imgur.com/GA1QAnS.png" },
                {10009, "http://imgur.com/spufx0f.png"},
                {10015, "http://imgur.com/ua9esyK.png" },
                {10013, "http://imgur.com/7rkcCZ4.png"},
                {10072, "http://imgur.com/8jX3QSR.png" },
                {10010, "http://imgur.com/8snfg2N.png" },
                {10008, "http://imgur.com/Tk0KIFU.png"},
                {10014, "http://imgur.com/o0ZdjsQ.png" },
            }
            },
            {"hoodie", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/EvGigZB.png" },
                {10142, "http://imgur.com/WwwArof.png"},
                {14179, "http://imgur.com/wEyu9Ew.png" },
                {10052, "http://imgur.com/ghnihF2.png"},
                {14178, "http://imgur.com/EOh10jX.png" },
                {10133, "http://imgur.com/hmZGoIY.png"},
                {14072, "http://imgur.com/A0o5Tm5.png" },
                {10132, "http://imgur.com/i0tdeK7.png"},
                {10129, "http://imgur.com/UqqydUz.png" },
                {10086, "http://imgur.com/A7gjMm0.png"},
            }
            },
            {"hat.cap", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/TfycJC9.png" },
                {10029, "http://imgur.com/QFNHOZz.png"},
                {10027, "http://imgur.com/Zf14dTy.png" },
                {10055, "http://imgur.com/1zfiClI.png"},
                {10030, "http://imgur.com/acgOSe6.png" },
                {10026, "http://imgur.com/Augez3h.png"},
                {10028, "http://imgur.com/VZqY3iA.png" },
                {10045, "http://imgur.com/F34fPio.png" },
            }
            },
            {"hat.beenie", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/yDkGk47.png" },
                {14180, "http://imgur.com/ProarPm.png"},
                {10018, "http://imgur.com/gEPcMj7.png" },
                {10017, "http://imgur.com/QKmuZg9.png"},
                {10040, "http://imgur.com/2EEZQdG.png" },
                {10016, "http://imgur.com/PMU76bY.png"},
                {10085, "http://imgur.com/FDKeEhw.png" },
            }
            },
            {"burlap.gloves", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/8aFVMgl.png" },
                {10128, "http://imgur.com/HqZut8a.png"},
            }
            },
            {"burlap.shirt", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/MUs4xL6.png" },
                {10136, "http://imgur.com/E4wXccC.png"},
            }
            },
            {"hat.boonie", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2b4OjxB.png" },
                {10058, "http://imgur.com/lkfKdyj.png"},
            }
            },
            {"santahat", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/bmOV0aX.png" },
            }
            },
            {"shirt.tanktop", new Dictionary<ulong, string>
            {
                {0, "http://vignette4.wikia.nocookie.net/play-rust/images/1/1e/Tank_Top_icon.png/revision/latest/scale-to-width-down/100?cb=20161102190317" },
            }
            },
            {"shirt.collared", new Dictionary<ulong, string>
            {
                {0, "http://vignette1.wikia.nocookie.net/play-rust/images/8/8c/Shirt_icon.png/revision/latest/scale-to-width-down/100?cb=20161102193325" },
            }
            },
            {"pants.shorts", new Dictionary<ulong, string>
            {
                {0, "http://vignette4.wikia.nocookie.net/play-rust/images/4/46/Shorts_icon.png/revision/latest/scale-to-width-down/100?cb=20161102194514" },
            }
            },
            {"hazmat.pants", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ZsaLNUK.png" },
            }
            },
            {"hazmat.jacket", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/uKk9ghN.png" },
            }
            },
            {"hazmat.helmet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BHSrFsh.png" },
            }
            },
            {"hazmat.gloves", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/JYTXvnx.png" },
            }
            },
            {"hazmat.boots", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/sfU4PdX.png" },
            }
            },
            {"hat.miner", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/RtRy2ne.png" },
            }
            },
            {"hat.candle", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/F7nP0PC.png" },
            }
            },
            {"hat.wolf", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/D2Z8QjL.png" },
            }
            },
            {"burlap.trousers", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/tDqEh7T.png" },
            }
            },
            {"burlap.shoes", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/wXrkSxd.png" },
            }
            },
            {"burlap.headwrap", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/u6YLWda.png" },
            }
            },
            {"bucket.helmet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Sb5cnpz.png" },
                {10127, "http://imgur.com/ZD3jtRS.png"},
                {10126, "http://imgur.com/qULrqXO.png" },
            }
            },
            {"wood.armor.helmet", new Dictionary<ulong, string>
            {
                {0, "https://vignette1.wikia.nocookie.net/play-rust/images/0/0f/Ef4af380406f0c3385ed80fc87971b60.png/revision/latest/scale-to-width-down/480?cb=20170420184039" },
            }
            },
            {"wood.armor.pants", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/k2O9xEX.png" },
            }
            },
            {"wood.armor.jacket", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/9PUyVIv.png" },
            }
            },
            {"roadsign.kilt", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/WLh1Nv4.png" },
            }
            },
            {"roadsign.jacket", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/tqpDp2V.png" },
            }
            },
            {"riot.helmet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/NlxGOum.png" },
            }
            },
            {"metal.plate.torso", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/lMw6ez2.png" },
            }
            },
            {"metal.facemask", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BPd5q6h.png" },
            }
            },

            {"coffeecan.helmet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/RrY8aMM.png" },
            }
            },
            {"bone.armor.suit", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/FkFR1kX.png" },
            }
            },
            {"attire.hide.vest", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/RQ8LJ5q.png" },
            }
            },
            {"attire.hide.skirt", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/nRlYLJW.png" },
            }
            },
            {"attire.hide.poncho", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/cqHND3g.png" },
            }
            },
            {"attire.hide.pants", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/rJy27KQ.png" },
            }
            },
            {"attire.hide.helterneck", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2RXe7cg.png" },
            }
            },
            {"attire.hide.boots", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/6S98FbC.png" },
            }
            },
            {"deer.skull.mask", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/sqLjUSE.png" },
            }
            },
            {"pistol.revolver", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/C6BHyBB.png" },
                {10114, "http://imgur.com/DAj7lQo.png"},
            }
            },
            {"pistol.semiauto", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Zwqg3ic.png" },
                {10087, "http://imgur.com/hQwcNSG.png"},
                {10108, "http://imgur.com/21uutmr.png" },
                {10081, "http://imgur.com/vllF4FS.png"},
                {10073, "http://imgur.com/MSBvxA7.png" },
            }
            },
            {"rifle.ak", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/qlgloXW.png" },
                {10135, "http://imgur.com/0xgio10.png"},
                {10137, "http://imgur.com/UPDtgyK.png" },
                {10138, "http://imgur.com/XXKKLC4.png"},
            }
            },
            {"rifle.bolt", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/8oVVXJS.png" },
                {10117, "http://imgur.com/lFOPXfE.png"},
                {10115, "http://imgur.com/qbTQ06y.png" },
                {10116, "http://imgur.com/VhRwq7N.png"},
            }
            },
            {"shotgun.pump", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/OHRph6g.png" },
                {10074, "http://imgur.com/h91b64t.png"},
                {10140, "http://imgur.com/ktINZdj.png" },
            }
            },
            {"shotgun.waterpipe", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/3BliJtR.png" },
                {10143, "http://imgur.com/rmftGXr.png"},
            }
            },
            {"rifle.lr300", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/NYffUwv.png"},
            }
            },
            {"crossbow", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/nDBFhTA.png" },
            }
            },
            {"smg.thompson", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/rSQ5nHj.png" },
                {10120, "http://imgur.com/H3nPvJh.png"},
            }
            },
            {"weapon.mod.small.scope", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/jMvDHLz.png" },
            }
            },
            {"weapon.mod.silencer", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oighpzk.png" },
            }
            },
            {"weapon.mod.muzzlebrake", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/sjxJIjT.png" },
            }
            },
            {"weapon.mod.muzzleboost", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/U9aMaPN.png" },
            }
            },
            {"weapon.mod.lasersight", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/rxIzDwY.png" },
            }
            },
            {"weapon.mod.holosight", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/R76B83t.png" },
            }
            },
            {"weapon.mod.flashlight", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4gFapPt.png" },
            }
            },
            {"spear.wooden", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/7QpIs8B.png" },
            }
            },
            {"spear.stone", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Y3HstyV.png" },
            }
            },
            {"smg.2", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ElXI2uv.png" },
            }
            },
            {"smg.mp5", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ohazNYk.png" },
            }
            },
            {"shotgun.double", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Pm2Q4Dj.png" },
            }
            },
            {"salvaged.sword", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/M6gWbNv.png" },
            }
            },
            {"salvaged.cleaver", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DrelWEg.png" },
            }
            },
            {"rocket.launcher", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2yDyb9p.png" },
            }
            },
            {"rifle.semiauto", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/UfGP5kq.png" },
            }
            },
            {"pistol.eoka", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/SSb9czm.png" },
            }
            },
            {"machete", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/KfwkwV8.png" },
            }
            },
            {"mace", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/OtsvCkC.png" },
            }
            },
            {"longsword", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/1StsKVe.png" },
            }
            },
            {"lmg.m249", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/f7Rzrn2.png" },
            }
            },
            {"knife.bone", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/9TaVbYX.png" },
            }
            },
            {"flamethrower", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/CwhZ8i7.png" },
            }
            },
            {"bow.hunting", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Myv79jT.png" },
            }
            },
            {"bone.club", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ib11D8V.png" },
            }
            },
            {"grenade.f1", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ZwrVuXh.png" },
            }
            },
            {"grenade.beancan", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/FQZOd7m.png" },
            }
            },
            {"ammo.handmade.shell", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/V0CyZ7j.png" },
            }
            },
            {"ammo.pistol", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/gDNR7oj.png" },
            }
            },
             {"ammo.pistol.fire", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/VyX0pAu.png" },
            }
            },
            {"ammo.pistol.hv", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/E1dB4Nb.png" },
            }
            },
            {"ammo.rifle", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/rqVkjX3.png" },
            }
            },
            {"ammo.rifle.explosive", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/hpAxKQc.png" },
            }
            },
            {"ammo.rifle.hv", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BkG4hLM.png" },
            }
            },
            {"ammo.rifle.incendiary", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/SN4XV2S.png" },
            }
            },
            {"ammo.rocket.basic", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Weg1M6y.png" },
            }
            },
            {"ammo.rocket.fire", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/j4XMSmO.png" },
            }
            },
            {"ammo.rocket.hv", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/5mdVIIV.png" },
            }
            },
            {"ammo.rocket.smoke", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/kMTgSEI.png" },
            }
            },
            {"ammo.shotgun", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/caFY5Bp.png" },
            }
            },
            {"ammo.shotgun.slug", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ti5fCBp.png" },
            }
            },
            {"arrow.hv", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/r6VLTt2.png" },
            }
            },
            {"arrow.wooden", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/yMCfjKh.png" },
            }
            },
            {"bandage", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/TuMpnnu.png" },
            }
            },
            {"syringe.medical", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DPDicE6.png" },
            }
            },
            { "largemedkit", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/iPsWViD.png" },
            }
            },
            { "antiradpills", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/SIhXEtB.png" },
            }
            },
            { "blood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Mdtvg2m.png" },
            }
            },
            {"bed", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/K0zQtwh.png" },
            }
            },
            {"box.wooden", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/dFqTUTQ.png" },
            }
            },
            {"box.wooden.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/qImBEtL.png" },
                {10124, "http://imgur.com/oXO4riD.png" },
                {10122, "http://imgur.com/Ue06zjq.png" },
                {10123, "http://imgur.com/QAizFb6.png" },
                {10141, "http://imgur.com/gSzIfNj.png" },
            }
            },
            {"campfire", new Dictionary<ulong, string>
            {
                {0, "http://i.imgur.com/TiAlJpv.png" },
            }
            },
            {"ceilinglight", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/3sikyL6.png" },
            }
            },
            {"door.double.hinged.metal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/awNuhRv.png" },
            }
            },
            {"door.double.hinged.toptier", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oJCqHd6.png" },
            }
            },
            {"door.double.hinged.wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/tcHmZXZ.png" },
            }
            },
            {"door.hinged.metal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/UGZftiQ.png" },
            }
            },
            {"door.hinged.toptier", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/bc2TrfQ.png" },
            }
            },
            {"door.hinged.wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/PrrWSN2.png" },
            }
            },
            {"floor.grill", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/bp7ZOkE.png" },
            }
            },
            {"floor.ladder.hatch", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/suML6jj.png" },
            }
            },
            {"gates.external.high.stone", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/o4NWWXp.png" },
            }
            },
            {"gates.external.high.wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DRa9a8G.png" },
            }
            },
            {"cupboard.tool", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/OzUewI1.png" },
            }
            },
            {"shelves", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/vjtdyk5.png" },
            }
            },
            {"shutter.metal.embrasure.a", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/1ke0LVO.png" },
            }
            },
            {"shutter.metal.embrasure.b", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/uRtgNRH.png" },
            }
            },
            {"shutter.wood.a", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/VngPUi2.png" },
            }
            },
            {"sign.hanging", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/VIeRGh9.png" },
            }
            },
            {"sign.hanging.banner.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Owr3668.png" },
            }
            },
            {"sign.hanging.ornate", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/nQ1xHYb.png" },
            }
            },
            {"sign.pictureframe.landscape", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/nNh1uro.png" },
            }
            },
            {"sign.pictureframe.portrait", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/CQr8UYq.png" },
            }
            },
            {"sign.pictureframe.tall", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/3b51GfA.png" },
            }
            },
            {"sign.pictureframe.xl", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/3zdBDqa.png" },
            }
            },
            {"sign.pictureframe.xxl", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/9xSgewe.png" },
            }
            },
            {"sign.pole.banner.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/nGRDZrO.png" },
            }
            },
            {"sign.post.double", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/CXUsPSn.png" },
            }
            },
            {"sign.post.single", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/0qXuSMs.png" },
            }
            },
            {"sign.post.town", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/KgN4T1C.png" },
            }
            },
            {"sign.post.town.roof", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/hCLJXg4.png" },
            }
            },
            {"sign.wooden.huge", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DehcZTb.png" },
            }
            },
            {"sign.wooden.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BItcvBB.png" },
            }
            },
            {"sign.wooden.medium", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/zXJcB26.png" },
            }
            },
            {"sign.wooden.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/wfDYYYW.png" },
            }
            },
            {"jackolantern.angry", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/NRdMCfb.png" },
            }
            },
            {"jackolantern.happy", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2gIfuAO.png" },
            }
            },
            {"ladder.wooden.wall", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/E3haHSe.png" },
            }
            },
            {"lantern", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/UHQdu3Q.png" },
            }
            },
            {"lock.code", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/pAXI8ZY.png" },
            }
            },
            {"mining.quarry", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4Mgh1nK.png" },
            }
            },
            {"mining.pumpjack", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/FWbMASw.png" },
            }
            },
            {"wall.external.high", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/mB8oila.png" },
            }
            },
            {"wall.external.high.stone", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/7t3BdwH.png" },
            }
            },
            {"wall.frame.cell", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oLj65GS.png" },
            }
            },
            {"wall.frame.cell.gate", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/iAcwJmG.png" },
            }
            },
            {"wall.frame.fence", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4HVSY9Y.png" },
            }
            },
            {"wall.frame.fence.gate", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/mpmO78C.png" },
            }
            },
            {"wall.frame.shopfront", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/G7fB7kk.png" },
            }
            },
            {"wall.window.bars.metal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/QmkIpkZ.png" },
            }
            },
            {"wall.window.bars.toptier", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/AsMdaCc.png" },
            }
            },
            {"wall.window.bars.wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/VS3SVVB.png" },
            }
            },
            {"lock.key", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/HuelWn0.png" },
            }
            },
            { "barricade.concrete", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/91Ob9XP.png" },
            }
            },
            {"barricade.metal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/7rseBMC.png" },
            }
            },
            { "barricade.sandbags", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/gBQLSgQ.png" },
            }
            },
            { "barricade.wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ycYTO3W.png" },
            }
            },
            { "barricade.woodwire", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/PMEFBla.png" },
            }
            },
            { "barricade.stone", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/W8qTCEX.png" },
            }
            },
            {"bone.fragments", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/iOJbBGT.png" },
            }
            },
            {"charcoal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/G2hyxqi.png" },
            }
            },
            {"cloth", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/0olknLW.png" },
            }
            },
            {"coal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/SIWOdbj.png" },
            }
            },
            {"crude.oil", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/VmQvwPS.png" },
            }
            },
            {"fat.animal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/7NdUBKm.png" },
            }
            },
            {"hq.metal.ore", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/kdBrQ2P.png" },
            }
            },
            {"lowgradefuel", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/CSNPLYX.png" },
            }
            },
            {"metal.fragments", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/1bzDvUs.png" },
            }
            },
            {"metal.ore", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/yrTGHvv.png" },
            }
            },
            {"leather", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/9rqWrIy.png" },
            }
            },
            {"metal.refined", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/j2947YU.png" },
            }
            },
            {"wood", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/AChzDls.png" },
            }
            },
            {"seed.corn", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/u9ZPaeG.png" },
            }
            },
            {"seed.hemp", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/wO6aojb.png" },
            }
            },
            {"seed.pumpkin", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/mHaV8ei.png" },
            }
            },
            {"skull.human", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ZFnWubS.png" },
            }
            },
            {"skull.wolf", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/f4MRE72.png" },
            }
            },
            {"stones", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/cluFzuZ.png" },
            }
            },
            {"sulfur", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/1RTTB7k.png" },
            }
            },
            {"sulfur.ore", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/AdxkKGb.png" },
            }
            },
            {"gunpowder", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/qV7b4WD.png" },
            }
            },
            {"researchpaper", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Pv8jxrl.png" },
            }
            },
            {"explosives", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/S43G64k.png" },
            }
            },
            {"botabag", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/MkIOiUs.png" },
            }
            },
            {"box.repair.bench", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/HpwYNjI.png" },
            }
            },
            {"bucket.water", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/svlCdlv.png" },
            }
            },
            {"explosive.satchel", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/dlUW54q.png" },
            }
            },
            {"explosive.timed", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/CtxUCgC.png" },
            }
            },
            {"flare", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/MS0JcRT.png" },
            }
            },
            {"fun.guitar", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/l96owHe.png" },
            }
            },
            {"furnace", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/77i4nqb.png" },
            }
            },
            {"furnace.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/NmsmUzo.png" },
            }
            },
            {"hatchet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/5juFLRZ.png" },
            }
            },
            {"icepick.salvaged", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ZTJLWdI.png" },
            }
            },
            {"axe.salvaged", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/muTaCg2.png" },
            }
            },
            {"pickaxe", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/QNirWhG.png" },
            }
            },
            {"research.table", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/C9wL7Kk.png" },
            }
            },
            {"small.oil.refinery", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Qqz6RgS.png" },
            }
            },
            {"stone.pickaxe", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/54azzFs.png" },
            }
            },
            {"stonehatchet", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/toLaFZd.png" },
            }
            },
            {"supply.signal", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/wj6yzow.png" },
            }
            },
            {"surveycharge", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/UPNvuY0.png" },
            }
            },
            {"target.reactive", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BNcKZnU.png" },
            }
            },
            {"tool.camera", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4AaLCfW.png" },
            }
            },
            {"water.barrel", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/JsmzCeU.png" },
            }
            },
            {"water.catcher.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/YWrJQoa.png" },
            }
            },
            {"water.catcher.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/PTXcYXs.png" },
            }
            },
            {"water.purifier", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/L7R4Ral.png" },
            }
            },
            {"rock", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2GMBs5M.png" },
            }
            },
            {"torch", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/qKYxg5E.png" },
            }
            },
            {"stash.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/fH4RWZe.png" },
            }
            },
            {"sleepingbag", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oJes3Lo.png" },
                {10121, "http://imgur.com/GvwtwGH.png" },
                {10037, "http://imgur.com/gDYUE6H.png" },
                {10119, "http://imgur.com/3lxtYiD.png" },
                {10109, "http://imgur.com/wQeDRzA.png" },
                {10107, "http://imgur.com/AHUGw7a.png" },
                {10077, "http://imgur.com/j7YFRrI.png" },
                {10076, "http://imgur.com/UCtDwNT.png" },
            }
            },
            {"hammer.salvaged", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/5oh3Wke.png" },
            }
            },
            {"hammer", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/KNG2Gvs.png" },
            }
            },
            {"blueprulongbase", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/gMdRr6G.png" },
            }
            },
            {"fishtrap.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/spuGlOj.png" },
            }
            },
            {"building.planner", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oXu5F27.png" },
            }
            },
            {"battery.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/214z05n.png" },
            }
            },
            {"can.tuna.empty", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/GB02zHx.png" },
            }
            },
            {"can.beans.empty", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/9K5In35.png" },
            }
            },
            { "cctv.camera", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4j4LD01.png" },
            }
            },
            {"pookie.bear", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/KJSccj0.png" },
            }
            },
            {"targeting.computer", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/oPMPl3B.png" },
            }
            },
            {"trap.bear", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/GZD4bVy.png" },
            }
            },
            {"trap.landmine", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/YR0lVCs.png" },
            }
            },
            {"autoturret", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4R0ByHj.png" },
            }
            },
            {"spikes.floor", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Nj0yJs0.png" },
            }
            },
            {"note", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/AM3Uech.png" },
            }
            },
            {"paper", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/pK49c6M.png" },
            }
            },
            {"map", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/u8HBelr.png" },
            }
            },
            {"stocking.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/di39MBT.png" },
            }
            },
            {"stocking.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/6eAg1zi.png" },
            }
            },
            {"generator.wind.scrap", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/fuQaE1H.png" },
            }
            },
            {"xmas.present.large", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/dU3nhYo.png" },
            }
            },
            {"xmas.present.medium", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Ov5YUty.png" },
            }
            },
            {"xmas.present.small", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/hWCd67B.png" },
            }
            },
            {"door.key", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/kw8UAN2.png" },
            }
            },
            { "wolfmeat.burned", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/zAJhDNd.png" },
            }
            },
            { "wolfmeat.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/LKlgpMe.png" },
            }
            },
            { "wolfmeat.raw", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/qvMvis8.png" },
            }
            },
            { "wolfmeat.spoiled", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/8kXOVyJ.png" },
            }
            },
            {"waterjug", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/BJzeMkc.png" },
            }
            },
            {"water.salt", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/d4ihUtv.png" },
            }
            },
            {"water", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/xdz5L7M.png" },
            }
            },
            {"smallwaterbottle", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/YTLCucH.png" },
            }
            },
            {"pumpkin", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Gb9NvdQ.png" },
            }
            },
            {"mushroom", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/FeWuvuh.png" },
            }
            },
            {"meat.boar", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/4ijrHrn.png" },
            }
            },
            {"meat.pork.burned", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/5Dam9qQ.png" },
            }
            },
            {"meat.pork.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/yhgxCPG.png" },
            }
            },
            {"humanmeat.burned", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DloSZvl.png" },
            }
            },
            {"humanmeat.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/ba2j2rG.png" },
            }
            },
            {"humanmeat.raw", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/28SpF8Y.png" },
            }
            },
            {"humanmeat.spoiled", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/mSWVRUi.png" },
            }
            },
            {"granolabar", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/3rvzSwj.png" },
            }
            },
            {"fish.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Idtzv1t.png" },
            }
            },
            {"fish.minnows", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/7LXZH2S.png" },
            }
            },
            {"fish.troutsmall", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/aJ2PquF.png" },
            }
            },
            {"fish.raw", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/GdErxqf.png" },
            }
            },
            {"corn", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/6V5SJZ0.png" },
            }
            },
            {"chocholate", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Ymq7PsV.png" },
            }
            },
            {"chicken.burned", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/34sYfir.png" },
            }
            },
            {"chicken.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/UvHbBhH.png" },
            }
            },
            {"chicken.raw", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/gMldKSz.png" },
            }
            },
            {"chicken.spoiled", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/hiOEwGn.png" },
            }
            },
            {"cactusflesh", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/8R16YDP.png" },
            }
            },
            {"candycane", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/DSxrXOI.png" },
            }
            },
            {"can.tuna", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/c8rDUP3.png" },
            }
            },
            {"can.beans", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/Ysn6ThW.png" },
            }
            },
            {"blueberries", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/tFZ66fB.png" },
            }
            },
            {"black.raspberries", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/HZjKpX9.png" },
            }
            },
            {"bearmeat", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/hpL2I64.png" },
            }
            },
            {"bearmeat.burned", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/f1eVA0W.png" },
            }
            },
            {"bearmeat.cooked", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/e5Z6w1y.png" },
            }
            },
            {"apple", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/goMCM2w.png" },
            }
            },
            {"apple.spoiled", new Dictionary<ulong, string>
            {
                {0, "http://imgur.com/2pi2sUH.png" },
            }
            },
            {"bleach", new Dictionary<ulong, string>
            {
                {0, "http://vignette3.wikia.nocookie.net/play-rust/images/a/ac/Bleach_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045849" },
            }
            },
            {"ducttape", new Dictionary<ulong, string>
            {
                {0, "http://vignette1.wikia.nocookie.net/play-rust/images/f/f8/Duct_Tape_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045924" },
            }
            },
            {"propanetank", new Dictionary<ulong, string>
            {
                {0, "http://vignette4.wikia.nocookie.net/play-rust/images/a/a8/Empty_Propane_Tank_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045920" },
            }
            },
            {"gears", new Dictionary<ulong, string>
            {
                {0, "http://vignette2.wikia.nocookie.net/play-rust/images/7/72/Gears_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045908" },
            }
            },
            {"glue", new Dictionary<ulong, string>
            {
                {0, "http://vignette3.wikia.nocookie.net/play-rust/images/6/66/Glue_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045933" },
            }
            },
            {"metalblade", new Dictionary<ulong, string>
            {
                {0, "http://vignette4.wikia.nocookie.net/play-rust/images/9/9b/Metal_Blade_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045912" },
            }
            },
            {"metalpipe", new Dictionary<ulong, string>
            {
                {0, "http://vignette2.wikia.nocookie.net/play-rust/images/4/4a/Metal_Pipe_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045929" },
            }
            },
            {"metalspring", new Dictionary<ulong, string>
            {
                {0, "http://vignette2.wikia.nocookie.net/play-rust/images/3/3d/Metal_Spring_icon.png/revision/latest/scale-to-width-down/100?cb=20161101151803" },
            }
            },
            {"riflebody", new Dictionary<ulong, string>
            {
                {0, "http://vignette2.wikia.nocookie.net/play-rust/images/0/08/Rifle_Body_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045853" },
            }
            },
            {"roadsigns", new Dictionary<ulong, string>
            {
                {0, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a5/Road_Signs_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045904" },
            }
            },
            {"rope", new Dictionary<ulong, string>
            {
                {0, "http://vignette1.wikia.nocookie.net/play-rust/images/1/15/Rope_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045939" },
            }
            },
            {"sewingkit", new Dictionary<ulong, string>
            {
                {0, "http://vignette1.wikia.nocookie.net/play-rust/images/2/29/Sewing_Kit_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045952" },
            }
            },
            {"sheetmetal", new Dictionary<ulong, string>
            {
                {0, "http://vignette3.wikia.nocookie.net/play-rust/images/3/39/Sheet_Metal_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045901" },
            }
            },
            {"smgbody", new Dictionary<ulong, string>
            {
                {0, "http://vignette3.wikia.nocookie.net/play-rust/images/d/d8/SMG_Body_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045947" },
            }
            },
            {"sticks", new Dictionary<ulong, string>
            {
                {0, "http://vignette1.wikia.nocookie.net/play-rust/images/d/d5/Sticks_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045943" },
            }
            },
            {"tarp", new Dictionary<ulong, string>
            {
                {0, "http://vignette4.wikia.nocookie.net/play-rust/images/1/12/Tarp_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045916" },
            }
            },
            {"techparts", new Dictionary<ulong, string>
            {
                {0, "http://vignette2.wikia.nocookie.net/play-rust/images/e/eb/Tech_Trash_icon.png/revision/latest/scale-to-width-down/100?cb=20161109045841" },
            }
            },
            {"hazmatsuit", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/3/36/Hazmat_Suit_icon.png/revision/latest/scale-to-width-down/100?cb=20161110200726" }
            }
            },
            {"pistol.m92", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/4/43/M92_Pistol_icon.png/revision/latest/scale-to-width-down/100?cb=20161202144022" }
            }
            },
            {"semibody", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/a/ac/Semi_Automatic_Body_icon.png/revision/latest/scale-to-width-down/100?cb=20161113040709" }
            }
            },
            {"blueprintbase", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/8/83/Blueprint_icon.png/revision/latest/scale-to-width-down/100?cb=20160819063752" }
            }
            },
            {"pistol.python", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/d/d4/Python_Revolver_icon.png/revision/latest/scale-to-width-down/100?cb=20170118190136" }
            }
            },
            {"clone.corn", new Dictionary<ulong, string>
            {
                { 0, "http://vignette4.wikia.nocookie.net/play-rust/images/6/65/Corn_Clone_icon.png/revision/latest/scale-to-width-down/100?cb=20170122060431" }
            }
            },
            {"clone.hemp", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/c/c9/Hemp_Clone_icon.png/revision/latest/scale-to-width-down/100?cb=20170122060314" }
            }
            },
            {"clone.pumpkin", new Dictionary<ulong, string>
            {
                { 0, "http://vignette4.wikia.nocookie.net/play-rust/images/8/82/Pumpkin_Plant_Clone_icon.png/revision/latest/scale-to-width-down/100?cb=20170122060357" }
            }
            },
            {"vending.machine", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/5/5c/Vending_Machine_icon.png/revision/latest/scale-to-width-down/100?cb=20170215180154" }
            }
            },
            {"flameturret", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/f/f9/Flame_Turret_icon.png/revision/latest/scale-to-width-down/100?cb=20170215180224" }
            }
            },
            {"fridge", new Dictionary<ulong, string>
            {
                { 0, "http://vignette4.wikia.nocookie.net/play-rust/images/8/88/Fridge_icon.png/revision/latest/scale-to-width-down/100?cb=20170216160606" }
            }
            },
            {"tunalight", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/b/b2/Tuna_Can_Lamp_icon.png/revision/latest/scale-to-width-down/100?cb=20170215182014" }
            }
            },
            {"door.closer", new Dictionary<ulong, string>
            {
                { 0, "http://i.imgur.com/QIKkGqT.png" }
            }
            },
            {"heavy.plate.pants", new Dictionary<ulong, string>
            {
                { 0, "http://vignette4.wikia.nocookie.net/play-rust/images/1/1e/Heavy_Plate_Pants_icon.png/revision/latest/scale-to-width-down/100?cb=20170301070510" }
            }
            },
            {"heavy.plate.jacket", new Dictionary<ulong, string>
            {
                { 0, "http://vignette1.wikia.nocookie.net/play-rust/images/c/c7/Heavy_Plate_Jacket_icon.png/revision/latest/scale-to-width-down/100?cb=20170301070457" }
            }
            },
            {"heavy.plate.helmet", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/c/cb/Heavy_Plate_Helmet_icon.png/revision/latest/scale-to-width-down/100?cb=20170301070431" }
            }
            },
            {"chair", new Dictionary<ulong, string>
            {
                { 0, "http://vignette1.wikia.nocookie.net/play-rust/images/3/3c/Chair_icon.png/revision/latest/scale-to-width-down/100?cb=20170301072948" }
            }
            },
            {"wall.frame.shopfront.metal", new Dictionary<ulong, string>
            {
                { 0, "http://vignette2.wikia.nocookie.net/play-rust/images/4/46/Metal_Shop_Front_icon.png/revision/latest/scale-to-width-down/100?cb=20170216160552" }
            }
            },
            {"table", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/5/5d/Table_icon.png/revision/latest/scale-to-width-down/100?cb=20170301073010" }
            }
            },
            {"planter.small", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/a/a7/Small_Planter_Box_icon.png/revision/latest/scale-to-width-down/100?cb=20161215213113" }
            }
            },
            {"rug", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/c/c5/Rug_icon.png/revision/latest/scale-to-width-down/100?cb=20170301074259" }
            }
            },
            {"planter.large", new Dictionary<ulong, string>
            {
                { 0, "http://vignette1.wikia.nocookie.net/play-rust/images/3/35/Large_Planter_Box_icon.png/revision/latest/scale-to-width-down/100?cb=20161214151746" }
            }
            },
            {"locker", new Dictionary<ulong, string>
            {
                { 0, "http://vignette3.wikia.nocookie.net/play-rust/images/3/39/Locker_icon.png/revision/latest/scale-to-width-down/100?cb=20170317173730" }
            }
            },
            {"wall.frame.netting", new Dictionary<ulong, string>
            {
                { 0, "http://vignette1.wikia.nocookie.net/play-rust/images/b/bc/Netting_icon.png/revision/latest/scale-to-width-down/100?cb=20170318100728" }
            }
            },
            {"spinner.wheel", new Dictionary<ulong, string>
            {
                { 0, "http://vignette1.wikia.nocookie.net/play-rust/images/5/51/Spinning_wheel_icon.png/revision/latest/scale-to-width-down/100?cb=20170317173832" }
            }
            },
            {"rug.bear", new Dictionary<ulong, string>
            {
                { 0, "https://vignette4.wikia.nocookie.net/play-rust/images/d/d2/Rug_Bear_Skin_icon.png/revision/latest/scale-to-width-down/100?cb=20170301074227" }
            }
            },
            {"scrap", new Dictionary<ulong, string>
            {
                { 0, "http://i.imgur.com/vEObM0G.png" }
            }
            },
            {"searchlight", new Dictionary<ulong, string>
            {
                { 0, "https://vignette2.wikia.nocookie.net/play-rust/images/c/c6/Search_Light_icon.png/revision/latest/scale-to-width-down/100?cb=20170405133839" }
            }
            },
            {"weapon.mod.simplesight", new Dictionary<ulong, string>
            {
                { 0, "https://vignette1.wikia.nocookie.net/play-rust/images/9/93/Simple_Handmade_Sight_icon.png/revision/latest/scale-to-width-down/100?cb=20170405132430" }
            }
            },
};
        #endregion

        #region Data Management

        void SaveData()
        {
            if (isReady())
                ImageLibraryData.WriteObject(imageData);
        }

        void LoadData()
        {
            try
            {
                imageData = ImageLibraryData.ReadObject<ImageData>();
                if (imageData == null)
                {
                    Puts("Image Data File appears corrupt. Creating a new file, loading images...");
                    imageData = new ImageData();
                    RefreshImages();
                }
            }
            catch
            {
                Puts("Couldn't load Image Data, creating new datafile and refreshing Images");
                imageData = new ImageData();
                RefreshImages();
            }
            if (imageData.Images == null)
            {
                imageData.CommID = 0;
                imageData.Images = new Dictionary<string, Dictionary<ulong, uint>>();
            }
            if (imageData.ItemSkinInfo == null)
            {
                imageData.CommID = 0;
                imageData.ItemSkinInfo = new Dictionary<string, Dictionary<ulong, SkinInfo>>();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public bool UseWorkshopImages { get; set; }
            public string MapURL { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                UseWorkshopImages = true,
                MapURL = string.Empty,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}