using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using Object = StardewValley.Object;
using Vector2 = Microsoft.Xna.Framework.Vector2;


namespace FindBestWay
{
    internal class ModEntry : Mod
    {
        private static List<int> _selectedNodeTypeIndices;
        private static Dictionary<Vector2, Stone> _nodeStones;
        private static ModConfig _config;
        private static Texture2D _pixelTexture;
        private static List<LadderStone> _ladderStones;
        private static List<Vector2> _trueLadder;
        private static List<Vector2> _highlightedPath;
        private static bool _nextIsShaft;

        private static readonly (string Name, Color Color)[] _colorChoices =
        {
            ("Lime", Color.Lime),
            ("Green", Color.Green),
            ("Red", Color.Red),
            ("Blue", Color.Blue),
            ("Cyan", Color.Cyan),
            ("Yellow", Color.Yellow),
            ("Orange", Color.Orange),
            ("Magenta", Color.Magenta),
            ("Purple", Color.Purple),
            ("Pink", Color.Pink),
            ("White", Color.White),
            ("Black", Color.Black),
        };

        private static string _pendingColorName;

        // ===================================================================
        #region Color helpers (name <-> Color for the GMCM dropdown)
        // ===================================================================

        private string ColorToName(Color c)
        {
            foreach (var choice in _colorChoices)
            {
                if (choice.Color.R == c.R && choice.Color.G == c.G && choice.Color.B == c.B)
                    return choice.Name;
            }
            return _colorChoices[0].Name;
        }

        private Color NameToColor(string name)
        {
            foreach (var choice in _colorChoices)
            {
                if (choice.Name == name)
                    return choice.Color;
            }
            return _colorChoices[0].Color;
        }

        private string GetColorName()
        {
            if (_pendingColorName == null)
                _pendingColorName = ColorToName(_config.HighlightRectangleRGBA);
            return _pendingColorName;
        }

        private void SetColorByName(string name)
        {
            _pendingColorName = name;
            Color rgb = NameToColor(name);
            byte a = _config.HighlightRectangleRGBA.A;
            _config.HighlightRectangleRGBA = new Color(rgb.R, rgb.G, rgb.B, a);
        }

        #endregion

        // ===================================================================
        #region Lifecycle & events
        // ===================================================================

        public override void Entry(IModHelper helper)
        {

            _config = Helper.ReadConfig<ModConfig>();
            RadarNode.Initialize(_config);
            _pixelTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            var colorArray = Enumerable.Range(0, 1).Select(i => Color.White).ToArray();
            _pixelTexture.SetData(colorArray);

            _ladderStones = new List<LadderStone>();
            _trueLadder = new List<Vector2>();
            _highlightedPath = new List<Vector2>();

            _selectedNodeTypeIndices = RadarNode.All.Where(radarNode => _config.NodeTypes.Contains(radarNode.type))
                .OrderBy(radarNode => radarNode.value).Select(radarNode => radarNode.spriteIndex).ToList();
            _nodeStones = new Dictionary<Vector2, Stone>();

            Helper.Events.World.ObjectListChanged += OnObjectListChanged;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Player.Warped += OnWarped;
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (!e.IsCurrentLocation) return;
            if (e.Location is StardewValley.Locations.MineShaft mine)
            {
                UpdateTrueLadders();
                var ladderHasSpawned = _trueLadder.Count > 0;
                if (ladderHasSpawned) _ladderStones.Clear();
                else if (_ladderStones.Count <= 0) FindLadders(mine);
            }
            if (LocationHasNodes(e.Location))
                foreach (var pair in e.Removed)
                    _nodeStones.Remove(pair.Key);
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer) return;
            _ladderStones.Clear();
            _nodeStones.Clear();
            _trueLadder.Clear();
            _nextIsShaft = false;
            _highlightedPath.Clear();

            if (LocationHasNodes(e.NewLocation)) FindNodes(e.NewLocation);
            if (e.NewLocation is StardewValley.Locations.MineShaft mine)
            {
                FindLadders(mine);
                UpdateTrueLadders();

                // Resume pathfinding automatically if it was left enabled in config.
                if (_config.FindWayOrNot)
                    SetPathfinding(true, announce: false);
            }
            else
            {
                // Left the mines: detach the per-tick recompute, keep the config flag.
                Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                _highlightedPath.Clear();
            }
        }

        // The single timer used while pathfinding is on: refresh ladder data
        // and recompute the path twice per second (every 30 ticks ~= 0.5s).
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (Game1.mine == null)
            {
                Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                return;
            }

            // Run every 30 ticks (~0.5s).
            if (e.Ticks % 30 != 0)
                return;

            UpdateTrueLadders();
            FindLadders(Game1.mine);

            if (_config.FindWayOrNot)
            {
                UpdatePathToLadder();
            }
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (_ladderStones.Count > 0)
            {
                foreach (var obj in _ladderStones)
                {
                    var rect = obj.BoundingBox;
                    rect.Offset(-Game1.viewport.X, -Game1.viewport.Y);
                    if (_config.HighlightOrNot) DrawRectangle(e.SpriteBatch, rect, GetHighlightDrawColor());
                }
            }

            if (_trueLadder.Count > 0)
            {
                foreach (var ladderPos in _trueLadder)
                {
                    Vector2 worldPos = ladderPos * Game1.tileSize;
                    Vector2 screenPos = worldPos - new Vector2(Game1.viewport.X, Game1.viewport.Y);
                    Rectangle rect = new Rectangle(
                        (int)screenPos.X, (int)screenPos.Y, Game1.tileSize, Game1.tileSize
                    );

                    if (_config.HighlightOrNot) DrawRectangle(e.SpriteBatch, rect, GetHighlightDrawColor());
                }
            }

            if (_highlightedPath == null || _highlightedPath.Count == 0)
                return;

            foreach (var tile in _highlightedPath)
            {
                Vector2 worldPos = new Vector2(tile.X * Game1.tileSize, tile.Y * Game1.tileSize);
                Vector2 screenPos = worldPos - new Vector2(Game1.viewport.X, Game1.viewport.Y);
                int smallSize = Game1.tileSize / 4;
                int offset = (Game1.tileSize - smallSize) / 2;

                Rectangle rect = new Rectangle((int)screenPos.X + offset, (int)screenPos.Y + offset, smallSize, smallSize);
                e.SpriteBatch.Draw(Game1.staminaRect, rect, GetHighlightDrawColor());
            }
        }

        private static bool IsNodeTypeIncluded(Node type)
        {
            switch (type)
            {
                case Node.Copper:
                case Node.Iron:
                case Node.Gold:
                case Node.Iridium:
                case Node.Radioactive:
                    return _config.IncludeOres;

                case Node.Topaz:
                case Node.Amethyst:
                case Node.Aquamarine:
                case Node.Jade:
                case Node.Emerald:
                case Node.Ruby:
                case Node.Diamond:
                case Node.Gem:
                case Node.Mystic:
                    return _config.IncludeGems;

                case Node.Geode:
                case Node.FrozenGeode:
                case Node.MagmaGeode:
                case Node.OmniGeode:
                    return _config.IncludeGeodes;

                case Node.CinderShard:
                    return _config.IncludeCinderShard;

                default:
                    return true;
            }
        }

        private static Color GetHighlightDrawColor()
        {
            float alpha = (float)_config.HighlightAlpha;
            if (alpha < 0f) alpha = 0f;
            if (alpha > 1f) alpha = 1f;
            Color baseColor = _config.HighlightRectangleRGBA;
            return new Color(baseColor.R, baseColor.G, baseColor.B) * alpha;
        }

        private static void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Left + 3, rect.Top, rect.Width - 6, 3), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Left + 3, rect.Bottom - 3, rect.Width - 6, 3), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Left, rect.Top + 3, 3, rect.Height - 6), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Right - 3, rect.Top + 3, 3, rect.Height - 6), color);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {

            if (_config.CycleAlpha.JustPressed())
            {
                _config.HighlightAlpha = Math.Round((_config.HighlightAlpha + 0.15M) % 1.0M, 2);
                Game1.addHUDMessage(new HUDMessage($"Highlight alpha now {_config.HighlightAlpha}", 2));
                Helper.WriteConfig(_config);
            }

            else if (_config.ToggleHighlightKey.JustPressed())
            {
                _config.HighlightOrNot = !_config.HighlightOrNot;
                Game1.addHUDMessage(_config.HighlightOrNot
                    ? new HUDMessage("Ladder highlight enabled", 2)
                    : new HUDMessage("Ladder highlight disabled", 2));
                Helper.WriteConfig(_config);
            }

            else if (_config.FindWay.JustPressed())
            {
                SetPathfinding(!_config.FindWayOrNot, announce: true);
            }
        }

        #endregion

        // ===================================================================
        #region Ladder / node detection & pathfinding
        // ===================================================================

        // Single place that keeps the pathfinding state and the UpdateTicked
        // subscription in sync. Call this instead of setting _config.FindWayOrNot
        // directly, so the timer is always (un)subscribed to match the flag.
        private void SetPathfinding(bool enabled, bool announce)
        {
            _config.FindWayOrNot = enabled;

            // Always detach first to avoid double-subscribing.
            Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

            if (enabled)
            {
                Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
                if (announce) Game1.addHUDMessage(new HUDMessage("Path tracking enabled", 2));
                UpdatePathToLadder();
            }
            else
            {
                if (announce) Game1.addHUDMessage(new HUDMessage("Path tracking disabled", 2));
                _highlightedPath.Clear();
            }
        }

        private void UpdatePathToLadder()
        {
            if (!Context.IsWorldReady || GetAllLadders().Count == 0)
            {
                _highlightedPath.Clear();
                return;
            }
            Vector2 playerPos = Game1.player.Tile;

            var bestPath = FindBestPathToLadder(playerPos);
            _highlightedPath = bestPath ?? new List<Vector2>();
        }

        private void UpdateTrueLadders()
        {
            _trueLadder.Clear();
            
            if (Game1.currentLocation is not MineShaft shaft)
                return;

            IReflectedField<NetPointDictionary<bool, NetBool>> generatedLadders =
                Helper.Reflection.GetField<NetPointDictionary<bool, NetBool>>(
                    shaft,
                    "createLadderDownEvent",
                    false
                );

            if (generatedLadders != null)
            {
                NetPointDictionary<bool, NetBool> target = generatedLadders.GetValue();
                if (target != null)
                {
                    foreach (KeyValuePair<Point, bool> ladder in target.Pairs)
                    {
                        _trueLadder.Add(new Vector2(ladder.Key.X, ladder.Key.Y));
                    }
                }
            }

            IReflectedField<NetVector2Dictionary<bool, NetBool>> placedLadders =
                Helper.Reflection.GetField<NetVector2Dictionary<bool, NetBool>>(
                    shaft,
                    "createLadderAtEvent",
                    false
                );

            if (placedLadders != null)
            {
                NetVector2Dictionary<bool, NetBool> target = placedLadders.GetValue();
                if (target != null)
                {
                    foreach (KeyValuePair<Vector2, bool> ladder in target.Pairs)
                    {
                        _trueLadder.Add(ladder.Key);
                    }
                }
            }
        }


        private bool FindLadders(StardewValley.Locations.MineShaft mine)
        {
            var ladderHasSpawned = Helper.Reflection.GetField<bool>(mine, "ladderHasSpawned").GetValue();
            if (ladderHasSpawned || mine.mustKillAllMonstersToAdvance() || !mine.shouldCreateLadderOnThisLevel()) return true;
            var netStonesLeftOnThisLevel = Helper.Reflection
                .GetField<NetIntDelta>(mine, "netStonesLeftOnThisLevel").GetValue().Value;
            var chance = 0.02 + 1.0 / Math.Max(1, netStonesLeftOnThisLevel) + Game1.player.LuckLevel / 100.0 +
                         Game1.player.DailyLuck / 5.0;
            if (mine.EnemyCount == 0) chance += 0.04;
            _ladderStones.Clear();
            foreach (var pair in mine.Objects.Pairs.Where(pair => pair.Value.Name.Equals("Stone")))
            {
                var obj = pair.Value;
                var r = Utility.CreateDaySaveRandom(pair.Key.X * 1000, pair.Key.Y, mine.mineLevel);
                r.NextDouble();
                var next = r.NextDouble();
                if ((netStonesLeftOnThisLevel == 0 || next < chance) && obj.ParentSheetIndex / 24 < Game1.objectSpriteSheet.Height)
                    _ladderStones.Add(new LadderStone(obj));
            }
            if (_config.ForceShafts && mine.getMineArea() == StardewValley.Locations.MineShaft.desertArea
                && !_nextIsShaft && _ladderStones.Count > 0)
                ForceShaft(mine);
            return _ladderStones.Count > 0;
        }

        private void ForceShaft(StardewValley.Locations.MineShaft mine)
        {
            var mineRandom = Helper.Reflection.GetField<Random>(mine, "mineRandom").GetValue();
            var r = Cloner.Clone(mineRandom);
            var next = r.NextDouble();
            while (next >= 0.2)
            {
                next = r.NextDouble();
                mineRandom.NextDouble();
            }
            _nextIsShaft = true;
        }

        private bool LocationHasNodes(GameLocation loc)
        {
            return loc is StardewValley.Locations.MineShaft || loc is StardewValley.Locations.VolcanoDungeon || loc is StardewValley.Locations.Mountain;
        }

        private void FindNodes(GameLocation loc)
        {
            foreach (var pair in loc.objects.Pairs.Where(pair => pair.Value.Name.Equals("Stone") && _selectedNodeTypeIndices.Contains(pair.Value.ParentSheetIndex)))
                _nodeStones.Add(pair.Key, new Stone(pair.Value));
        }


        private List<Vector2> GetLadderStones()
        {
            var result = new List<Vector2>();
            foreach (var ladder in _ladderStones)
            {
                Vector2 ladderPos = new Vector2(
                    ladder.BoundingBox.X / (float)Game1.tileSize,
                    ladder.BoundingBox.Y / (float)Game1.tileSize
                );
                result.Add(ladderPos);
            }
            return result;
        }

        private List<Vector2> GetAllLadders()
        {
            return _trueLadder.Concat(GetLadderStones()).ToList();
        }

        private bool IsStone(Vector2 tile)
        {
            if (!Game1.currentLocation.objects.TryGetValue(tile, out Object obj))
                return false;

            return obj.Name.Equals("Stone");
        }
        
        private List<Vector2> FindBestPathToLadder(Vector2 startPos)
        {
            var goals = GetAllLadders();
            List<Vector2> path = null;
            float value = float.MinValue;

            if (_config.PathTypes.Contains(PathType.Fast))
            {
                // Fast mode: shortest route to the nearest ladder via multi-goal A*.
                (path, value) = AStarPathfinding(startPos, goals);
            }
            else
            {
                // Valuable mode: among the nearest few ladders, pick the route
                // with the highest score along the way.
                var sortedGoals = goals.OrderBy(g => ManhattanDistance(startPos, g)).ToList();
                int maxGoalsToCheck = Math.Min(4, sortedGoals.Count);

                for (int i = 0; i < maxGoalsToCheck; i++)
                {
                    var (candidatePath, candidateValue) =
                        GreedyPathfinding(startPos, new List<Vector2> { sortedGoals[i] });
                    if (candidateValue > value)
                    {
                        value = candidateValue;
                        path = candidatePath;
                    }
                }
            }

            if (path != null && path.Count > 0)
            {
                Monitor.Log($"Ladder ({path[path.Count - 1].X}, {path[path.Count - 1].Y}): Distance={path.Count}, Grade={value}", LogLevel.Debug);
            }
            return path;
        }

        // Multi-goal A*: finds the shortest-cost route from start to whichever
        // ladder is nearest. Cost = walkCost per step, plus mineCost when a step
        // enters a stone tile (it must be mined). The heuristic is the Manhattan
        // distance to the closest goal, scaled by walkCost, which never overestimates
        // the true cost on a 4-directional grid, so the path returned is optimal.
        // Returned "value" is the net node value collected along that path, so the
        // caller's logging/comparison stays consistent with GreedyPathfinding.
        private (List<Vector2> path, float value) AStarPathfinding(Vector2 start, List<Vector2> goals)
        {
            if (goals == null || goals.Count == 0)
                return (null, 0);

            var walkCost = _config.WalkCost;
            var mineCost = _config.MineCost;

            // gScore: cheapest known cost from start to a tile.
            var gScore = new Dictionary<Vector2, float>();
            var cameFrom = new Dictionary<Vector2, Vector2>();
            var closedSet = new HashSet<Vector2>();

            // Min-heap ordered by fScore = gScore + heuristic.
            var openSet = new PriorityQueue<Vector2, float>();

            float Heuristic(Vector2 n)
            {
                float best = float.MaxValue;
                foreach (var g in goals)
                {
                    float h = ManhattanDistance(n, g);
                    if (h < best) best = h;
                }
                return best * walkCost;
            }

            gScore[start] = 0f;
            openSet.Enqueue(start, Heuristic(start));

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                // Stale entry (we already found a cheaper route and re-queued).
                if (closedSet.Contains(current))
                    continue;

                foreach (var g in goals)
                {
                    if (Vector2.Distance(current, g) < 1.25f)
                    {
                        var path = ReconstructPath(cameFrom, current);
                        return (path, ComputePathValue(path));
                    }
                }

                closedSet.Add(current);

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (closedSet.Contains(neighbor)) continue;
                    if (!IsWalkable(neighbor)) continue;

                    float stepCost = walkCost;
                    if (IsStone(neighbor))
                        stepCost += mineCost;

                    float tentativeG = gScore[current] + stepCost;

                    if (!gScore.TryGetValue(neighbor, out float knownG) || tentativeG < knownG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor));
                    }
                }
            }

            return (null, 0);
        }

        // Net node value collected along a path, counting each node tile once.
        private float ComputePathValue(List<Vector2> path)
        {
            if (path == null || path.Count == 0) return 0;

            var collected = new HashSet<Vector2>();
            float total = 0;
            foreach (var tile in path)
                total += GetTileValue(tile, collected);
            return total;
        }

        // Value-driven greedy search: always expand the highest-scoring node and
        // return as soon as a ladder is reached. Score = collected node value
        // along the way minus walk/mine cost. (Note: not guaranteed optimal.)
        private (List<Vector2> path, float value) GreedyPathfinding(Vector2 start, List<Vector2> goals)
        {
            var openSet = new List<PathNode>();
            var closedSet = new HashSet<Vector2>();
            var cameFrom = new Dictionary<Vector2, Vector2>();
            var score = new Dictionary<Vector2, float>();
            var walkCost = _config.WalkCost;
            var mineCost = _config.MineCost;
            var collectedAtPos = new Dictionary<Vector2, HashSet<Vector2>>();

            collectedAtPos[start] = new HashSet<Vector2>();
            score[start] = GetTileValue(start, collectedAtPos[start]);
            openSet.Add(new PathNode(start, score[start]));

            while (openSet.Count > 0)
            {
                var current = openSet.OrderByDescending(n => n.Score).First();
                foreach (var g in goals)
                {
                    if (Vector2.Distance(current.Position, g) < 1.25f)
                    {
                        return (ReconstructPath(cameFrom, current.Position), score[current.Position]);
                    }
                }

                openSet.Remove(current);
                closedSet.Add(current.Position);

                foreach (var neighbor in GetNeighbors(current.Position))
                {
                    if (closedSet.Contains(neighbor)) continue;
                    if (!IsWalkable(neighbor)) continue;

                    float tentativeScore = score[current.Position] - walkCost;
                    var neighborCollected = new HashSet<Vector2>(collectedAtPos[current.Position]);

                    if (IsStone(neighbor) && !neighborCollected.Contains(neighbor))
                    {
                        tentativeScore -= mineCost;
                    }

                    tentativeScore += GetTileValue(neighbor, neighborCollected);

                    if (!score.ContainsKey(neighbor) || tentativeScore > score[neighbor])
                    {
                        cameFrom[neighbor] = current.Position;
                        score[neighbor] = tentativeScore;
                        collectedAtPos[neighbor] = neighborCollected;

                        var existingNode = openSet.FirstOrDefault(n => n.Position == neighbor);
                        if (existingNode != null)
                            openSet.Remove(existingNode);

                        openSet.Add(new PathNode(neighbor, score[neighbor]));
                    }
                }
            }
            return (null, 0);
        }

        private int GetTileValue(Vector2 tile, HashSet<Vector2> collectedNodes)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return 0;

            int totalValue = 0;

            var tilesToCheck = GetNeighbors(tile);

            foreach (var checkTile in tilesToCheck)
            {
                if (collectedNodes.Contains(checkTile))
                    continue;

                if (_nodeStones.TryGetValue(checkTile, out Stone stone))
                {
                    var radarNode = RadarNode.All.FirstOrDefault(rn => rn.spriteIndex == stone.SpriteIndex);
                    if (radarNode != null && IsNodeTypeIncluded(radarNode.type))
                    {
                        totalValue += radarNode.value;
                        collectedNodes.Add(checkTile);
                    }
                }
            }

            return totalValue;
        }

        private bool IsWalkable(Vector2 tile)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null) return false;

            if (tile.X < 0 || tile.Y < 0 ||
                tile.X >= Game1.currentLocation.Map.Layers[0].LayerWidth ||
                tile.Y >= Game1.currentLocation.Map.Layers[0].LayerHeight)
                return false;

            var buildingsLayer = Game1.currentLocation.Map.GetLayer("Buildings");
            if (buildingsLayer != null)
            {
                var tile2 = buildingsLayer.Tiles[(int)tile.X, (int)tile.Y];
                if (tile2 != null)
                {
                    var propertyValue = tile2.Properties.ContainsKey("Passable") ?
                        tile2.Properties["Passable"].ToString() : null;
                    if (propertyValue == null || propertyValue == "F")
                        return false;
                }
            }
            return true;
        }

        private List<Vector2> GetNeighbors(Vector2 tile)
        {
            return new List<Vector2>
            {
                new Vector2(tile.X, tile.Y - 1),
                new Vector2(tile.X, tile.Y + 1),
                new Vector2(tile.X - 1, tile.Y),
                new Vector2(tile.X + 1, tile.Y)
            };
        }

        private float ManhattanDistance(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private List<Vector2> ReconstructPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
        {
            var path = new List<Vector2> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }
            return path;
        }

        #endregion

        // ===================================================================
        #region Internal data types
        // ===================================================================

        class PathNode
        {
            public Vector2 Position { get; }
            public float Score { get; }

            public PathNode(Vector2 position, float score)
            {
                Position = position;
                Score = score;
            }
        }

        class Stone
        {
            public Stone(Object obj)
            {
                SpriteIndex = obj.ParentSheetIndex;
                this.obj = obj;
            }

            public Object obj { get; }
            public int SpriteIndex { get; }
        }

        class LadderStone : Stone
        {
            public LadderStone(Object obj) : base(obj)
            {
                BoundingBox = obj.GetBoundingBox();
            }

            public Rectangle BoundingBox { get; }
        }

        #endregion

        // ===================================================================
        #region GMCM config menu
        // ===================================================================

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi != null)
            {
                RegisterModConfigMenu(gmcmApi);
            }
            else
            {
                Monitor.Log("Failed to access GMCM API. Is the Generic Mod Config Menu installed?", LogLevel.Warn);
            }
        }

        private void RegisterModConfigMenu(IGenericModConfigMenuApi api)
        {
            api.Register(
                mod: ModManifest,
                reset: ResetConfig,
                save: SaveConfig
            );
            AddOptions(api);
            // Color changes are handled directly by the highlight-color option's
            // setValue (SetColorByName), so no OnFieldChanged hook is needed.
        }

        private void ResetConfig()
        {
            _config = new ModConfig();
            _pendingColorName = ColorToName(_config.HighlightRectangleRGBA);
        }

        private void SaveConfig()
        {
            Helper.WriteConfig(_config);
            RadarNode.Initialize(_config);
        }

        private void AddOptions(IGenericModConfigMenuApi api)
        {
            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.FindWayOrNot,
                setValue: value => SetPathfinding(value, announce: false),
                name: () => "Enable Pathfinding",
                tooltip: () => "Draw a trail of dots showing the best route to the next ladder/shaft."
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.FindWay,
                setValue: value => _config.FindWay = value,
                name: () => "Pathfinding Toggle Key",
                tooltip: () => "Key to toggle pathfinding on/off in the mine."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.HighlightOrNot,
                setValue: value => _config.HighlightOrNot = value,
                name: () => "Highlight Ladder Tiles",
                tooltip: () => "Draw a box around ladders/shafts and the stones that may spawn one."
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.ToggleHighlightKey,
                setValue: value => _config.ToggleHighlightKey = value,
                name: () => "Highlight Ladder Tiles Toggle Key",
                tooltip: () => "Key to toggle the ladder highlight on/off in the mine."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.ForceShafts,
                setValue: value => _config.ForceShafts = value,
                name: () => "Force Shafts",
                tooltip: () => "Force the next drop to be a shaft (falls several floors). Skull Cavern only."
            );

            api.AddTextOption(
                mod: ModManifest,
                getValue: () => string.Join(", ", _config.PathTypes),
                setValue: value => _config.PathTypes = new HashSet<PathType>(value.Split(',').Select(Enum.Parse<PathType>)),
                name: () => "Path Type",
                allowedValues: Enum.GetNames(typeof(PathType)),
                tooltip: () => "Fast = shortest route to a ladder. Valuable = route that passes valuable ores (uses the values below)."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.IncludeOres,
                setValue: value => _config.IncludeOres = value,
                name: () => "Count Ores",
                tooltip: () => "If off, the Valuable path ignores ores (Copper/Iron/Gold/Iridium/Radioactive). Their values below are kept."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.IncludeGems,
                setValue: value => _config.IncludeGems = value,
                name: () => "Count Gems",
                tooltip: () => "If off, the Valuable path ignores gems (Diamond, Ruby, ... Prismatic Shard, Mystic Stone). Their values below are kept."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.IncludeGeodes,
                setValue: value => _config.IncludeGeodes = value,
                name: () => "Count Geodes",
                tooltip: () => "If off, the Valuable path ignores geodes (Geode/Frozen/Magma/Omni). Their values below are kept."
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.IncludeCinderShard,
                setValue: value => _config.IncludeCinderShard = value,
                name: () => "Count Cinder Shards",
                tooltip: () => "If off, the Valuable path ignores cinder shards. Their value below is kept."
            );

            api.AddTextOption(
                mod: ModManifest,
                getValue: GetColorName,
                setValue: SetColorByName,
                name: () => "Highlight Color",
                tooltip: () => "Color of the highlight boxes and the pathfinding dots.",
                allowedValues: _colorChoices.Select(c => c.Name).ToArray(),
                fieldId: "highlight-color"
            );

            api.AddComplexOption(
                mod: ModManifest,
                name: () => "Color Preview",
                beforeMenuOpened: () => _pendingColorName = ColorToName(_config.HighlightRectangleRGBA),
                draw: (sb, pos) =>
                {
                    Color rgb = NameToColor(GetColorName());
                    Color preview = new Color(rgb.R, rgb.G, rgb.B, (byte)255);
                    var box = new Rectangle((int)pos.X, (int)pos.Y, 64, 48);
                    sb.Draw(_pixelTexture, new Rectangle(box.X - 2, box.Y - 2, box.Width + 4, box.Height + 4), Color.Black);
                    sb.Draw(_pixelTexture, box, preview);
                },
                height: () => 56
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => (float)_config.HighlightAlpha,
                setValue: value => _config.HighlightAlpha = (decimal)value,
                min: 0.0f,
                max: 1.0f,
                interval: 0.05f,
                name: () => "Highlight Opacity",
                tooltip: () => "Transparency of the boxes and dots. 0 = invisible, 1 = fully opaque."
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.CycleAlpha,
                setValue: value => _config.CycleAlpha = value,
                name: () => "Cycle Opacity Key",
                tooltip: () => "Key to step the opacity up in-game (wraps back to low after the max)."
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.WalkCost,
                setValue: value => _config.WalkCost = value,
                name: () => "Walk Cost",
                tooltip: () => "Pathfinding cost to move through an empty tile. Higher favors mining shortcuts."
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.MineCost,
                setValue: value => _config.MineCost = value,
                name: () => "Mine Cost",
                tooltip: () => "Pathfinding cost to mine through a stone tile. Higher favors walking around."
            );
            
            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.CopperValue,
                setValue: value => _config.CopperValue = value,
                name: () => "Copper Ore Value",
                tooltip: () => "Worth used by the Valuable path type. The values below work the same way."
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.IronValue,
                setValue: value => _config.IronValue = value,
                name: () => "Iron Ore Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.GoldValue,
                setValue: value => _config.GoldValue = value,
                name: () => "Gold Ore Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.IridiumValue,
                setValue: value => _config.IridiumValue = value,
                name: () => "Iridium Ore Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.RadioactiveValue,
                setValue: value => _config.RadioactiveValue = value,
                name: () => "Radioactive Ore Value"
            );
            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.DiamondValue,
                setValue: value => _config.DiamondValue = value,
                name: () => "Diamond Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.RubyValue,
                setValue: value => _config.RubyValue = value,
                name: () => "Ruby Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.JadeValue,
                setValue: value => _config.JadeValue = value,
                name: () => "Jade Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.AmethystValue,
                setValue: value => _config.AmethystValue = value,
                name: () => "Amethyst Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.TopazValue,
                setValue: value => _config.TopazValue = value,
                name: () => "Topaz Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.EmeraldValue,
                setValue: value => _config.EmeraldValue = value,
                name: () => "Emerald Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.AquamarineValue,
                setValue: value => _config.AquamarineValue = value,
                name: () => "Aquamarine Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.GemValue,
                setValue: value => _config.GemValue = value,
                name: () => "Prismatic Shard Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.MysticValue,
                setValue: value => _config.MysticValue = value,
                name: () => "Mystic Stone Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.GeodeValue,
                setValue: value => _config.GeodeValue = value,
                name: () => "Geode Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.FrozenGeodeValue,
                setValue: value => _config.FrozenGeodeValue = value,
                name: () => "Frozen Geode Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.MagmaGeodeValue,
                setValue: value => _config.MagmaGeodeValue = value,
                name: () => "Magma Geode Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.OmniGeodeValue,
                setValue: value => _config.OmniGeodeValue = value,
                name: () => "Omni Geode Value"
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => _config.CinderShardValue,
                setValue: value => _config.CinderShardValue = value,
                name: () => "Cinder Shard Value"
            );
        }

        #endregion
    }
}