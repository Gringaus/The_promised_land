using System;
using UnityEngine;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.World;

namespace FoundersLands.Presentation
{
    /// <summary>
    /// An interactive map view over a <see cref="ColonyHost"/> (GDD §18 camera/selection, §10 placement).
    /// Pipeline-agnostic IMGUI so it works the moment you press Play, with no scene or prefab setup:
    /// <list type="bullet">
    ///   <item>pan with a middle/right-mouse drag or the arrow keys, zoom with the scroll wheel;</item>
    ///   <item>hover to highlight a tile, left-click to select it and read its details;</item>
    ///   <item>buildings are drawn as markers (amber while under construction, blue once done);</item>
    ///   <item>pick a building from the palette and left-click an empty tile to place a blueprint —
    ///         the colony's builders raise it from there. Locked types wait on research (§16).</item>
    /// </list>
    /// A richer, interactive sibling to <see cref="ColonyDebugOverlay"/>; use one or the other.
    /// </summary>
    [RequireComponent(typeof(ColonyHost))]
    [DisallowMultipleComponent]
    public sealed class ColonyMapView : MonoBehaviour
    {
        [SerializeField, Range(2, 32)] private int pixelsPerTile = 8;
        [SerializeField, Min(160f)] private float panelWidth = 320f;

        private ColonyHost _host;
        private Texture2D _mapTexture;
        private WorldMap _texturedMap;
        private GUIStyle _rich;

        private Vector2 _pan;            // top-left of the map within the viewport, in pixels
        private bool _panInitialised;
        private int _selX = -1, _selY = -1;
        private bool _hasBuildType;
        private BuildingType _buildType;
        private string _message;
        private float _messageUntil;

        private void Awake() => _host = GetComponent<ColonyHost>();

        private void OnGUI()
        {
            if (_host == null) return;
            if (_host.Colony == null || _host.Map == null)
            {
                GUI.Label(new Rect(12, 12, 520, 24), "Founder's Lands — press Play to generate a world…");
                return;
            }

            EnsureMapTexture();
            Rect viewport = new Rect(0, 0, Mathf.Max(0f, Screen.width - panelWidth), Screen.height);
            if (!_panInitialised) { CenterOnSettlement(viewport); _panInitialised = true; }

            HandleInput(viewport);
            DrawMap(viewport);
            DrawPanel(viewport);
        }

        // ---------------------------------------------------------------- input

        private void HandleInput(Rect viewport)
        {
            Event e = Event.current;
            int pps = Mathf.Max(1, pixelsPerTile);

            // Keyboard: arrow keys pan, Esc cancels a build. Handled through IMGUI events (not
            // UnityEngine.Input), so it works whatever the project's input backend is.
            if (e.type == EventType.KeyDown)
            {
                float step = pps * 3f;
                switch (e.keyCode)
                {
                    case KeyCode.Escape: if (_hasBuildType) { _hasBuildType = false; e.Use(); } break;
                    case KeyCode.LeftArrow: _pan.x += step; e.Use(); break;
                    case KeyCode.RightArrow: _pan.x -= step; e.Use(); break;
                    case KeyCode.UpArrow: _pan.y += step; e.Use(); break;
                    case KeyCode.DownArrow: _pan.y -= step; e.Use(); break;
                }
            }

            if (!viewport.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                // Zoom toward the cursor: keep the tile under the mouse pinned.
                Vector2 before = ScreenToTileF(e.mousePosition, viewport, pps);
                pixelsPerTile = Mathf.Clamp(pixelsPerTile - (int)Mathf.Sign(e.delta.y) * Mathf.Max(1, pixelsPerTile / 8), 2, 32);
                int npps = Mathf.Max(1, pixelsPerTile);
                Vector2 local = e.mousePosition - viewport.position;
                _pan.x = local.x - before.x * npps;
                _pan.y = local.y - (_host.Map.Height - before.y) * npps;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                _pan += e.delta;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (ScreenToTile(e.mousePosition, viewport, pps, out int tx, out int ty))
                {
                    if (_hasBuildType) TryPlace(tx, ty);
                    else { _selX = tx; _selY = ty; }
                    e.Use();
                }
            }
        }

        private void TryPlace(int tx, int ty)
        {
            Settlement c = _host.Colony;
            if (!c.IsBuildingUnlocked(_buildType)) { Flash($"{Name(_buildType)} is not researched yet"); return; }
            if (BuildingAt(tx, ty) != null) { Flash("That tile is already occupied"); return; }

            Building b = c.PlaceBlueprint(_buildType, tx, ty);
            if (b == null) { Flash($"Cannot place {Name(_buildType)} here"); return; }
            _selX = tx; _selY = ty;
            Flash($"Blueprint placed: {Name(_buildType)} at ({tx},{ty})");
        }

        // ---------------------------------------------------------------- drawing

        private void DrawMap(Rect viewport)
        {
            WorldMap map = _host.Map;
            int pps = Mathf.Max(1, pixelsPerTile);

            GUI.BeginGroup(viewport);
            GUI.DrawTexture(new Rect(_pan.x, _pan.y, map.Width * pps, map.Height * pps),
                _mapTexture, ScaleMode.StretchToFill, false);

            // Buildings: amber while rising, blue once complete.
            var buildings = _host.Colony.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                Color col = b.Complete ? new Color(0.30f, 0.65f, 1f) : new Color(1f, 0.65f, 0.15f);
                FillTile(b.X, b.Y, pps, col, 0.82f);
            }

            // Settlement centre.
            Outline(_host.Colony.CenterX, _host.Colony.CenterY, pps, new Color(1f, 0.95f, 0.2f));

            // Hover ghost (in build mode) and current selection.
            if (_hasBuildType && ScreenToTile(Event.current.mousePosition, viewport, pps, out int hx, out int hy))
            {
                bool ok = BuildingAt(hx, hy) == null && _host.Colony.IsBuildingUnlocked(_buildType);
                FillTile(hx, hy, pps, ok ? new Color(0.4f, 1f, 0.4f, 0.5f) : new Color(1f, 0.3f, 0.3f, 0.5f), 1f);
            }
            if (_selX >= 0) Outline(_selX, _selY, pps, Color.white);

            GUI.EndGroup();
        }

        private void DrawPanel(Rect viewport)
        {
            Settlement c = _host.Colony;
            var clock = c.Clock;
            var area = new Rect(viewport.xMax, 0, panelWidth, Screen.height);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("<b>FOUNDER'S LANDS</b>", Rich());
            GUILayout.Label($"{clock.Season} · day {clock.DayOfSeason + 1}/{clock.DaysPerSeason} · year {clock.Year}");
            GUILayout.Label($"Pop {c.AlivePopulation}   Health {c.AverageHealth:0.0}   Food {c.FoodUnits():0}");
            GUILayout.Label($"Buildings {c.BuildingsComplete} done · {c.BuildingsUnderConstruction} building");
            if (c.Config.EnableTrade) GUILayout.Label($"Silver {c.TradeLedger.Silver:0}");
            if (c.Config.EnableTechnology) GUILayout.Label($"Tech {c.UnlockedTechs.Count}/{c.Techs.Count} (+{c.TechWorkBonus * 100f:0}%)");
            if (c.Config.EnableThreats) GUILayout.Label($"Threat {c.Threat.Stage} · raids {c.Threat.TotalRaids}");

            GUILayout.Space(6);
            DrawSelection();

            GUILayout.Space(6);
            DrawBuildPalette();

            GUILayout.FlexibleSpace();
            DrawMessage();
            DrawControls();
            GUILayout.EndArea();
        }

        private void DrawSelection()
        {
            GUILayout.Label("<b>Selected tile</b>", Rich());
            if (_selX < 0 || !_host.Map.InBounds(_selX, _selY)) { GUILayout.Label("(left-click the map)"); return; }

            Tile t = _host.Map.Get(_selX, _selY);
            GUILayout.Label($"({_selX},{_selY})  {t.Biome}{(t.IsWater ? " · water" : "")}");
            GUILayout.Label($"height {t.Height:0.00} · fertility {t.Fertility:0.00}");
            if (t.Resource != ResourceNodeKind.None) GUILayout.Label($"resource: {t.Resource} ({t.ResourceAmount})");

            Building b = BuildingAt(_selX, _selY);
            if (b != null)
            {
                GUILayout.Label($"<b>{Name(b.Type)}</b> — {b.Stage}", Rich());
                if (!b.Complete) GUILayout.Label($"work {b.WorkFraction * 100f:0}% · materials {b.DeliveredFraction * 100f:0}%");
            }
        }

        private void DrawBuildPalette()
        {
            Settlement c = _host.Colony;
            GUILayout.Label(_hasBuildType ? $"<b>Build:</b> {Name(_buildType)} (Esc to cancel)" : "<b>Build</b> (pick, then click a tile)", Rich());
            GUILayout.Label("<size=10>greyed = needs research (§16)</size>", Rich());

            int col = 0;
            foreach (BuildingType bt in Enum.GetValues(typeof(BuildingType)))
            {
                if (col == 0) GUILayout.BeginHorizontal();
                bool selected = _hasBuildType && _buildType == bt;
                string label = Name(bt) + (selected ? "  <" : ""); // ASCII-safe; locked types are greyed out
                GUI.enabled = c.IsBuildingUnlocked(bt);
                if (GUILayout.Button(label, GUILayout.Width(panelWidth / 2f - 8f)))
                {
                    _hasBuildType = !selected;
                    _buildType = bt;
                }
                GUI.enabled = true;
                if (++col == 2) { col = 0; GUILayout.EndHorizontal(); }
            }
            if (col == 1) GUILayout.EndHorizontal();
        }

        private void DrawControls()
        {
            GUILayout.Label($"<b>Speed</b> {_host.DaysPerSecond:0.#}/s {(_host.Running ? "" : "(paused)")}", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_host.Running ? "Pause" : "Resume")) _host.ToggleRunning();
            if (GUILayout.Button("1×")) _host.DaysPerSecond = 1f;
            if (GUILayout.Button("4×")) _host.DaysPerSecond = 4f;
            if (GUILayout.Button("16×")) _host.DaysPerSecond = 16f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Regenerate world"))
            {
                _host.Begin();
                _mapTexture = null; _panInitialised = false; _selX = -1; _hasBuildType = false;
            }
        }

        private void DrawMessage()
        {
            if (_message == null) return;
            if (Time.realtimeSinceStartup > _messageUntil) { _message = null; return; }
            GUILayout.Label($"<i>{_message}</i>", Rich());
        }

        // ---------------------------------------------------------------- helpers

        private void Flash(string msg) { _message = msg; _messageUntil = Time.realtimeSinceStartup + 3f; }

        private string Name(BuildingType type) => _host.Colony.BuildingCatalog.Get(type).Name;

        private Building BuildingAt(int x, int y)
        {
            var b = _host.Colony.Buildings;
            for (int i = 0; i < b.Count; i++) if (b[i].X == x && b[i].Y == y) return b[i];
            return null;
        }

        private void CenterOnSettlement(Rect viewport)
        {
            int pps = Mathf.Max(1, pixelsPerTile);
            _pan.x = viewport.width * 0.5f - _host.Colony.CenterX * pps;
            _pan.y = viewport.height * 0.5f - (_host.Map.Height - _host.Colony.CenterY) * pps;
        }

        // Screen point -> integer tile (Y flipped: texture row 0 is the map's south edge).
        private bool ScreenToTile(Vector2 screen, Rect viewport, int pps, out int tx, out int ty)
        {
            Vector2 f = ScreenToTileF(screen, viewport, pps);
            tx = Mathf.FloorToInt(f.x);
            ty = Mathf.FloorToInt(f.y);
            return _host.Map.InBounds(tx, ty) && viewport.Contains(screen);
        }

        private Vector2 ScreenToTileF(Vector2 screen, Rect viewport, int pps)
        {
            Vector2 local = screen - viewport.position - _pan;
            float tileX = local.x / pps;
            float tileY = _host.Map.Height - local.y / pps;
            return new Vector2(tileX, tileY);
        }

        private void FillTile(int x, int y, int pps, Color color, float scale)
        {
            float size = pps * scale;
            float pad = (pps - size) * 0.5f;
            float sx = _pan.x + x * pps + pad;
            float sy = _pan.y + (_host.Map.Height - 1 - y) * pps + pad;
            DrawRect(new Rect(sx, sy, size, size), color);
        }

        private void Outline(int x, int y, int pps, Color color)
        {
            float sx = _pan.x + x * pps;
            float sy = _pan.y + (_host.Map.Height - 1 - y) * pps;
            float th = Mathf.Max(1f, pps * 0.12f);
            DrawRect(new Rect(sx, sy, pps, th), color);
            DrawRect(new Rect(sx, sy + pps - th, pps, th), color);
            DrawRect(new Rect(sx, sy, th, pps), color);
            DrawRect(new Rect(sx + pps - th, sy, th, pps), color);
        }

        private static void DrawRect(Rect r, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void EnsureMapTexture()
        {
            if (_mapTexture != null && ReferenceEquals(_texturedMap, _host.Map)) return;
            _mapTexture = WorldMapTextureBuilder.Build(_host.Map);
            _texturedMap = _host.Map;
        }

        private GUIStyle Rich()
        {
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true };
            return _rich;
        }
    }
}
