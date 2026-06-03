using UnityEngine;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.World;

namespace FoundersLands.Presentation
{
    /// <summary>
    /// A pipeline-agnostic IMGUI overlay over a <see cref="ColonyHost"/>: it draws the procedural map
    /// and a live read-out of the colony, plus speed controls. This is debug scaffolding for the
    /// Unity stages — a real HDRP world and UI Toolkit HUD replace it later (GDD §15, §18) — but it
    /// makes the simulation visible and steerable the moment you press Play.
    /// </summary>
    [RequireComponent(typeof(ColonyHost))]
    [DisallowMultipleComponent]
    public sealed class ColonyDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showMap = true;
        [SerializeField, Range(1, 10)] private int mapScale = 4;

        private ColonyHost _host;
        private Texture2D _mapTexture;
        private WorldMap _texturedMap;
        private GUIStyle _panel;

        private void Awake() => _host = GetComponent<ColonyHost>();

        private void OnGUI()
        {
            if (_host == null) return;

            if (_host.Colony == null)
            {
                GUI.Label(new Rect(12, 12, 480, 24), "Founder's Lands — press Play to generate a world…");
                return;
            }

            EnsureMapTexture();
            float mapRight = 12f;
            if (showMap && _mapTexture != null) mapRight = DrawMap() + 24f;
            DrawPanel(mapRight);
        }

        private void EnsureMapTexture()
        {
            if (_host.Map == null) return;
            if (_mapTexture != null && ReferenceEquals(_texturedMap, _host.Map)) return; // map is static once generated
            _mapTexture = WorldMapTextureBuilder.Build(_host.Map);
            _texturedMap = _host.Map;
        }

        // Draws the map at the top-left and returns its right edge in screen pixels.
        private float DrawMap()
        {
            WorldMap map = _host.Map;
            int scale = Mathf.Max(1, mapScale);
            // Keep the map within a sensible slice of the screen.
            while (scale > 1 && (map.Width * scale > Screen.width * 0.6f || map.Height * scale > Screen.height - 24)) scale--;

            var rect = new Rect(12, 12, map.Width * scale, map.Height * scale);
            GUI.DrawTexture(rect, _mapTexture, ScaleMode.StretchToFill, false);

            // Mark the settlement. Texture row 0 is the map's bottom, so flip Y for screen space.
            float mx = rect.x + _host.Colony.CenterX * scale;
            float my = rect.y + (map.Height - 1 - _host.Colony.CenterY) * scale;
            DrawMarker(new Rect(mx - 3, my - 3, 7, 7), new Color(1f, 0.95f, 0.2f));

            GUI.Label(new Rect(rect.x, rect.yMax + 2, rect.width, 20),
                $"{map.Width}x{map.Height} world · ● settlement");
            return rect.xMax;
        }

        private void DrawPanel(float x)
        {
            Settlement c = _host.Colony;
            SimClockInfo(out string season, out int day, out int year, out int dayOfSeason, out int daysPerSeason);

            var area = new Rect(x, 12, 320, Screen.height - 24);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("<b>FOUNDER'S LANDS</b>", Rich());
            GUILayout.Label($"{season}  ·  day {dayOfSeason + 1}/{daysPerSeason}  ·  year {year}");
            GUILayout.Space(4);

            GUILayout.Label($"Population   {c.AlivePopulation}");
            GUILayout.Label($"Avg health   {c.AverageHealth:0.0}");
            GUILayout.Label($"Food         {c.FoodUnits():0}   (≈{c.StoredNutrition():0} nutrition)");
            GUILayout.Label($"Firewood     {c.Storehouse.Count(ResourceType.Firewood):0}");

            Stock(c, "Wood", ResourceType.Wood);
            Stock(c, "Stone", ResourceType.Stone);
            Stock(c, "Planks", ResourceType.Planks);
            Stock(c, "Tools", ResourceType.Tools);
            Stock(c, "Grain", ResourceType.Grain);
            Stock(c, "Bread", ResourceType.Bread);

            GUILayout.Space(4);
            GUILayout.Label($"Buildings    {c.BuildingsComplete} done · {c.BuildingsUnderConstruction} building");
            GUILayout.Label($"Housing      {c.HousingCapacity}");

            if (c.Config.EnableThreats)
            {
                GUILayout.Space(4);
                GUILayout.Label($"<b>Threat</b>   {c.Threat.Stage}  (pressure {c.Threat.Pressure:0})", Rich());
                GUILayout.Label($"raids {c.Threat.TotalRaids} · stolen {c.Threat.TotalStolen:0}");
            }

            if (c.Config.EnablePopulationDynamics)
            {
                GUILayout.Space(4);
                GUILayout.Label("<b>People</b>", Rich());
                GUILayout.Label($"born {c.TotalBirths} · arrived {c.TotalImmigrants}");
                GUILayout.Label($"old age {c.NaturalDeaths} · left {c.TotalLeft} · died {c.TotalDeaths}");
            }

            GUILayout.FlexibleSpace();
            DrawControls();
            GUILayout.EndArea();
        }

        private void DrawControls()
        {
            GUILayout.Label($"<b>Speed</b>  {_host.DaysPerSecond:0.#} days/s {(_host.Running ? "" : "(paused)")}", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_host.Running ? "Pause" : "Resume")) _host.ToggleRunning();
            if (GUILayout.Button("1×")) _host.DaysPerSecond = 1f;
            if (GUILayout.Button("4×")) _host.DaysPerSecond = 4f;
            if (GUILayout.Button("16×")) _host.DaysPerSecond = 16f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Regenerate")) { _host.Begin(); _mapTexture = null; }
        }

        private static void Stock(Settlement c, string label, ResourceType type)
        {
            float n = c.Storehouse.Count(type);
            if (n > 0.5f) GUILayout.Label($"{label,-12} {n:0}");
        }

        private void SimClockInfo(out string season, out int day, out int year, out int dayOfSeason, out int daysPerSeason)
        {
            var clock = _host.Colony.Clock;
            season = clock.Season.ToString();
            day = clock.Day;
            year = clock.Year;
            dayOfSeason = clock.DayOfSeason;
            daysPerSeason = clock.DaysPerSeason;
        }

        private GUIStyle Rich()
        {
            if (_panel == null) _panel = new GUIStyle(GUI.skin.label) { richText = true };
            return _panel;
        }

        private static void DrawMarker(Rect r, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
