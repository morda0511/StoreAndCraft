namespace StoreAndCraft
{
    internal struct DisplayFilter
    {
        public int Id;
        public string En;
        public string De;
        public ItemDrop.ItemData.ItemType[] Types;
        public string[] Names;
        public bool ExcludeNamed;

        public string Label()
        {
            return Loc.T(En, De);
        }
    }

    internal static class DisplayFilters
    {
        public const string ZdoKey = "sac_filter";

        // Wood
        private static readonly string[] WoodNames =
        {
            "wood", "finewood", "roundlog", "elderbark", "yggdrasilwood", "ashwood", "blackwood"
        };

        // Ore & scrap (not finished bars)
        private static readonly string[] OreNames =
        {
            "copperore", "tinore", "ironore", "ironscrap", "silverore",
            "flametalore", "flametalorenew", "blackmetalscrap", "copperscrap"
        };

        // Smelted / worked metals
        private static readonly string[] MetalNames =
        {
            "copper", "tin", "bronze", "iron", "silver", "blackmetal", "flametal", "flametalnew",
            "bronzenails", "ironnails", "chain"
        };

        private static readonly string[] StoneNames =
        {
            "stone", "flint", "obsidian", "crystal", "sharpeningstone", "thunderstone", "grausten"
        };

        private static readonly string[] FuelNames =
        {
            "coal", "resin", "tar"
        };

        private static readonly string[] HideNames =
        {
            "deerhide", "leatherscraps", "trollhide", "wolfpelt", "loxpelt",
            "serpentscale", "chitin", "scalehide", "asksvinhide", "hare_pelt", "harepelt"
        };

        private static readonly string[] PartsNames =
        {
            "bonefragments", "witheredbone", "entrails", "bloodbag", "feathers", "guck", "ooze",
            "needle", "greydwarfeye", "freezegland", "hardantler", "wolffang", "root",
            "softtissue", "mandible", "bukeperries"
        };

        private static readonly string[] CropNames =
        {
            "barley", "barleyflour", "flax", "carrot", "onion", "turnip",
            "carrotseeds", "onionseeds", "turnipseeds", "beechseeds", "birchseeds",
            "fircone", "pinecone", "acorn", "oakseeds", "dandelion", "thistle",
            "magecap", "jotunpuffs", "smokepuffs", "sap", "royaljelly"
        };

        private static readonly string[] RawFoodNames =
        {
            "rawmeat", "boar_meat", "deer_meat", "deermeat", "wolf_meat", "wolfmeat",
            "loxmeat", "fish_raw", "fishraw", "serpentmeat", "necktail",
            "chicken_meat", "chickenmeat", "hare_meat", "haremeat", "asksvin_meat", "asksvinmeat",
            "breaddough", "loxpie_uncooked", "loxpieuncooked",
            "meadbasefrostresist", "meadbasehealth_medium", "meadbasehealth",
            "meadbasepoisonresist", "meadbasestamina_medium", "meadbasestamina", "meadbasetasty",
            "meadbaseeitr_minor", "meadbaseeitr_medium", "barleywinebase",
            "honey", "mushroom", "mushroomblue", "mushroomyellow", "mushroomendon"
        };

        private static readonly string[] GemNames =
        {
            "coins", "amber", "amberpearl", "ruby", "silvernecklace"
        };

        private static readonly string[] BossNames =
        {
            "surtlingcore", "dragontear", "ancientseed", "ymirremains", "queenbee",
            "yagluthdrop", "vegvisirshard_bonemass", "dvergrkeyfragment", "dvergrextractorkey",
            "mechanicalspring", "blackcore", "seekerchitin", "carapace", "refinedeitr",
            "gemstone_red", "gemstone_blue", "gemstone_green"
        };

        public static readonly DisplayFilter[] Choices =
        {
            Named(11, "Wood", "Holz", WoodNames, ItemDrop.ItemData.ItemType.Material),
            Named(12, "Ore", "Erz", OreNames, ItemDrop.ItemData.ItemType.Material),
            Named(13, "Metals", "Metalle", MetalNames, ItemDrop.ItemData.ItemType.Material),
            Named(14, "Stone", "Stein", StoneNames, ItemDrop.ItemData.ItemType.Material),
            Named(15, "Fuel", "Brennstoff", FuelNames, ItemDrop.ItemData.ItemType.Material),
            Named(16, "Hides", "Häute", HideNames, ItemDrop.ItemData.ItemType.Material),
            Named(17, "Parts", "Teile", PartsNames, ItemDrop.ItemData.ItemType.Material),
            Named(18, "Crops & Seeds", "Pflanzen & Samen", CropNames,
                ItemDrop.ItemData.ItemType.Material, ItemDrop.ItemData.ItemType.Consumable),
            Named(19, "Raw Food", "Rohes Essen", RawFoodNames, ItemDrop.ItemData.ItemType.Material),
            Named(20, "Gems & Coins", "Edelsteine & Münzen", GemNames, ItemDrop.ItemData.ItemType.Material),
            Named(21, "Boss / Rare", "Boss / Selten", BossNames, ItemDrop.ItemData.ItemType.Material),
            Typed(2, "Food", "Essen", true,
                ItemDrop.ItemData.ItemType.Consumable),
            Typed(3, "Fish", "Fisch", false,
                ItemDrop.ItemData.ItemType.Fish),
            Typed(4, "Trophy", "Trophäe", false,
                ItemDrop.ItemData.ItemType.Trophy),
            Typed(5, "Misc", "Sonstiges", false,
                ItemDrop.ItemData.ItemType.Misc),
            Typed(6, "Ammo", "Munition", false,
                ItemDrop.ItemData.ItemType.Ammo,
                ItemDrop.ItemData.ItemType.AmmoNonEquipable),
            Typed(7, "Tools", "Werkzeuge", false,
                ItemDrop.ItemData.ItemType.Tool),
            Typed(8, "Weapons", "Waffen", false,
                ItemDrop.ItemData.ItemType.OneHandedWeapon,
                ItemDrop.ItemData.ItemType.TwoHandedWeapon,
                ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
                ItemDrop.ItemData.ItemType.Bow,
                ItemDrop.ItemData.ItemType.Shield,
                ItemDrop.ItemData.ItemType.Torch,
                ItemDrop.ItemData.ItemType.Attach_Atgeir),
            Typed(9, "Armor", "Rüstung", false,
                ItemDrop.ItemData.ItemType.Helmet,
                ItemDrop.ItemData.ItemType.Chest,
                ItemDrop.ItemData.ItemType.Legs,
                ItemDrop.ItemData.ItemType.Hands,
                ItemDrop.ItemData.ItemType.Shoulder,
                ItemDrop.ItemData.ItemType.Trinket),
            Typed(10, "Utility", "Nutzen", false,
                ItemDrop.ItemData.ItemType.Utility)
        };

        private static DisplayFilter Named(int id, string en, string de, string[] names, params ItemDrop.ItemData.ItemType[] types)
        {
            return new DisplayFilter
            {
                Id = id,
                En = en,
                De = de,
                Types = types,
                Names = names,
                ExcludeNamed = false
            };
        }

        private static DisplayFilter Typed(int id, string en, string de, bool excludeNamed, params ItemDrop.ItemData.ItemType[] types)
        {
            return new DisplayFilter
            {
                Id = id,
                En = en,
                De = de,
                Types = types,
                Names = null,
                ExcludeNamed = excludeNamed
            };
        }

        public static bool TryGet(int id, out DisplayFilter filter)
        {
            for (int i = 0; i < Choices.Length; i++)
            {
                if (Choices[i].Id == id)
                {
                    filter = Choices[i];
                    return true;
                }
            }
            filter = default(DisplayFilter);
            return false;
        }

        public static string Label(int id)
        {
            DisplayFilter found;
            if (TryGet(id, out found))
                return found.Label();
            return Loc.T("Select type", "Typ wählen");
        }

        public static bool Matches(ItemDrop.ItemData item, int filterId)
        {
            DisplayFilter found;
            if (item?.m_shared == null || filterId <= 0 || !TryGet(filterId, out found))
                return false;

            if (!TypeAllowed(item.m_shared.m_itemType, found.Types))
                return false;

            string key = Key(item);
            if (found.Names != null && found.Names.Length > 0)
                return NameInList(key, found.Names) || ExtraOreWoodMatch(key, found.Id);

            if (found.ExcludeNamed)
                return !IsClaimedByNamedFilter(key, item.m_shared.m_itemType);

            return true;
        }

        private static bool TypeAllowed(ItemDrop.ItemData.ItemType type, ItemDrop.ItemData.ItemType[] types)
        {
            if (types == null || types.Length == 0)
                return true;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == type)
                    return true;
            }
            return false;
        }

        private static bool IsClaimedByNamedFilter(string key, ItemDrop.ItemData.ItemType type)
        {
            if (NameInList(key, CropNames) || NameInList(key, RawFoodNames))
                return true;
            if (type != ItemDrop.ItemData.ItemType.Material)
                return false;
            return NameInList(key, WoodNames)
                || NameInList(key, OreNames)
                || NameInList(key, MetalNames)
                || NameInList(key, StoneNames)
                || NameInList(key, FuelNames)
                || NameInList(key, HideNames)
                || NameInList(key, PartsNames)
                || NameInList(key, GemNames)
                || NameInList(key, BossNames)
                || ExtraOreWoodMatch(key, 11)
                || ExtraOreWoodMatch(key, 12);
        }

        // Catch future/mod items: *wood, *ore (not leather scraps etc.)
        private static bool ExtraOreWoodMatch(string key, int filterId)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (filterId == 11)
            {
                return key.EndsWith("wood") && !NameInList(key, OreNames) && !NameInList(key, MetalNames);
            }
            if (filterId == 12)
            {
                if (key.EndsWith("ore"))
                    return true;
                if (key.EndsWith("scrap") && !key.Contains("leather"))
                    return NameInList(key, OreNames) || key.Contains("metal") || key.Contains("iron") || key.Contains("copper") || key.Contains("black");
            }
            return false;
        }

        private static bool NameInList(string key, string[] names)
        {
            if (string.IsNullOrEmpty(key) || names == null)
                return false;
            for (int i = 0; i < names.Length; i++)
            {
                if (key == names[i])
                    return true;
            }
            return false;
        }

        private static string Key(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null)
                return "";

            if (item.m_dropPrefab != null)
            {
                string prefab = Normalize(item.m_dropPrefab.name);
                if (!string.IsNullOrEmpty(prefab))
                    return prefab;
            }

            return Normalize(item.m_shared.m_name);
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            string s = raw.Trim().ToLowerInvariant();
            if (s.StartsWith("$item_"))
                s = s.Substring(6);
            s = s.Replace(" ", "").Replace("-", "");
            return s;
        }
    }
}
