using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using System.Collections.Generic;

namespace FindBestWay
{
    class ModConfig
    {
        public bool ForceShafts { get; set; } = false;
        public KeybindList FindWay { get; set; } = KeybindList.Parse("Add");
        public bool FindWayOrNot { get; set; } = false;
        public HashSet<PathType> PathTypes { get; set; } = new HashSet<PathType>() { PathType.Fast };
        public KeybindList ToggleHighlightKey { get; set; } = KeybindList.Parse("Subtract");
        public bool HighlightOrNot { get; set; } = false;
        public Color HighlightRectangleRGBA { get; set; } = Color.Lime;
        public decimal HighlightAlpha { get; set; } = 0.45M;
        public int WalkCost { get; set; } = 1;
        public int MineCost { get; set; } = 4;
        public bool IncludeOres { get; set; } = true;
        public bool IncludeGems { get; set; } = true;
        public bool IncludeGeodes { get; set; } = true;
        public bool IncludeCinderShard { get; set; } = true;
        public KeybindList CycleAlpha { get; set; } = KeybindList.Parse("LeftAlt + OemTilde");
        public HashSet<Node> NodeTypes { get; set; } = new HashSet<Node>((Node[])System.Enum.GetValues(typeof(Node)));

        public int DiamondValue { get; set; } = 25;
        public int RubyValue { get; set; } = 12;
        public int JadeValue { get; set; } = 9;
        public int AmethystValue { get; set; } = 7;
        public int TopazValue { get; set; } = 6;
        public int EmeraldValue { get; set; } = 12;
        public int AquamarineValue { get; set; } = 9;
        public int GemValue { get; set; } = 50;
        public int MysticValue { get; set; } = 45;
        
        public int GeodeValue { get; set; } = 6;
        public int FrozenGeodeValue { get; set; } = 8;
        public int MagmaGeodeValue { get; set; } = 12;
        public int OmniGeodeValue { get; set; } = 16;
        
        public int CinderShardValue { get; set; } = 14;
        
        public int CopperValue { get; set; } = 10;
        public int IronValue { get; set; } = 14;
        public int GoldValue { get; set; } = 18;
        public int IridiumValue { get; set; } = 30;
        public int RadioactiveValue { get; set; } = 35;
    }
    enum PathType
    {
        Fast,
        Valuable
    }
    enum Node
    {
        Topaz,
        Amethyst,
        Aquamarine,
        Jade,
        Emerald,
        Ruby,
        Diamond,
        Gem,
        Mystic,
        Geode,
        FrozenGeode,
        MagmaGeode,
        OmniGeode,
        CinderShard,
        Copper,
        Iron,
        Gold,
        Iridium,
        Radioactive
    }

    class RadarNode
    {
        private static ModConfig _config;
        private static List<RadarNode> _cachedAll;
        public static void Initialize(ModConfig config)
        {
            _config = config;
            _cachedAll = null;
        }

        public static List<RadarNode> All { get
            {
                if (_cachedAll == null || _config == null)
                {
                    if (_config == null)
                    {
                        _cachedAll = new List<RadarNode> {
                            new RadarNode(Node.Diamond, 2, 750),
                            new RadarNode(Node.Ruby, 4, 250),
                            new RadarNode(Node.Jade, 6, 200),
                            new RadarNode(Node.Amethyst, 8, 100),
                            new RadarNode(Node.Topaz, 10, 80),
                            new RadarNode(Node.Emerald, 12, 250),
                            new RadarNode(Node.Aquamarine, 14, 180),
                            new RadarNode(Node.Gem, 44, 1000),
                            new RadarNode(Node.Mystic, 46, 1500),
                            // Give these an offset value to maintain grouping, hence -1000
                            new RadarNode(Node.Geode, 75, 50),
                            new RadarNode(Node.FrozenGeode, 76, 100),
                            new RadarNode(Node.MagmaGeode, 77, 150),
                            new RadarNode(Node.OmniGeode, 819, 200),
                            // These are somewhere between rare gem and rare ore, hence +1500
                            new RadarNode(Node.CinderShard, 843, 50),
                            new RadarNode(Node.CinderShard, 844, 50),
                            // Give these an offset value to maintain grouping, hence +2000
                            new RadarNode(Node.Copper, 751, 5),
                            new RadarNode(Node.Copper, 849, 5),
                            new RadarNode(Node.Iron, 290, 10),
                            new RadarNode(Node.Iron, 850, 10),
                            new RadarNode(Node.Gold, 764, 25),
                            new RadarNode(Node.Iridium, 765, 100),
                            new RadarNode(Node.Radioactive, 95, 300)
                        };
                    }
                    else
                    {
                        _cachedAll = new List<RadarNode> {
                            new RadarNode(Node.Diamond, 2, _config.DiamondValue),
                            new RadarNode(Node.Ruby, 4, _config.RubyValue),
                            new RadarNode(Node.Jade, 6, _config.JadeValue),
                            new RadarNode(Node.Amethyst, 8, _config.AmethystValue),
                            new RadarNode(Node.Topaz, 10, _config.TopazValue),
                            new RadarNode(Node.Emerald, 12, _config.EmeraldValue),
                            new RadarNode(Node.Aquamarine, 14, _config.AquamarineValue),
                            new RadarNode(Node.Gem, 44, _config.GemValue),
                            new RadarNode(Node.Mystic, 46, _config.MysticValue),
                            new RadarNode(Node.Geode, 75, _config.GeodeValue),
                            new RadarNode(Node.FrozenGeode, 76, _config.FrozenGeodeValue),
                            new RadarNode(Node.MagmaGeode, 77, _config.MagmaGeodeValue),
                            new RadarNode(Node.OmniGeode, 819, _config.OmniGeodeValue),
                            new RadarNode(Node.CinderShard, 843, _config.CinderShardValue),
                            new RadarNode(Node.CinderShard, 844, _config.CinderShardValue),
                            new RadarNode(Node.Copper, 751, _config.CopperValue),
                            new RadarNode(Node.Copper, 849, _config.CopperValue),
                            new RadarNode(Node.Iron, 290, _config.IronValue),
                            new RadarNode(Node.Iron, 850, _config.IronValue),
                            new RadarNode(Node.Gold, 764, _config.GoldValue),
                            new RadarNode(Node.Iridium, 765, _config.IridiumValue),
                            new RadarNode(Node.Radioactive, 95, _config.RadioactiveValue)
                        };
                    }
                }
                return _cachedAll;
            }
        }

        public RadarNode(Node type, int index, int value)
        {
            this.type = type;
            spriteIndex = index;
            this.value = value;
        }

        public Node type { get; }
        public int spriteIndex { get; }
        public int value { get; }
    }
}