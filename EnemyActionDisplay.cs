using System.IO;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace devLibra;

internal sealed unsafe class EnemyActionDisplay : IDisposable
{
    internal sealed record ActionInfo(uint Id, string Name, uint Icon, double Seconds, uint Group);
    internal readonly Dictionary<uint, ActionInfo[]> Jobs = new();
    internal readonly Dictionary<uint, string> JobNames = new();
    private readonly Dictionary<uint, ActionInfo> actions = new();
    private readonly Dictionary<(uint Enemy, uint Action), (double End, uint Sequence, double Duration)> timers = new();
    private readonly Dictionary<uint, uint> enemies = new();
    private readonly ConcurrentQueue<(uint Actor, uint Action, uint Sequence, double Time)> observations = new();
    private Hook<Delegates.Receive>? hook;
    private uint territory;
    private ulong owner;
    private long nextLog;
    internal string? Error;
    internal string State = "待機中";
    private IFontHandle? timerMeiryo;
    private IFontHandle? timerMiedinger;
    internal bool TestMode;
    internal bool EditPosition;
    private bool dragging;
    private bool Enabled => Plugin.Configuration.ForPvpBattleEnabled && Plugin.Configuration.EnemyActionsEnabled;
    internal uint[] CurrentEnemyJobs()
    {
        var jobs = new HashSet<uint>();
        void Add(uint id)
        {
            if (Plugin.ObjectTable.SearchByEntityId(id) is IPlayerCharacter player &&
                player.GameObjectId != Plugin.ObjectTable.LocalPlayer?.GameObjectId && Jobs.ContainsKey(player.ClassJob.RowId))
                jobs.Add(player.ClassJob.RowId);
        }
        // Configuration remains usable while tracking is disabled; dead opponents
        // still belong to the enemy team and must not disappear from this selector.
        if (TestMode)
        {
            var game = GameMain.Instance();
            if (Plugin.Condition[ConditionFlag.InDuelingArea] && game != null) Add(game->PvPDuelManager.EnemyEntityId);
        }
        else if (PvpBattleMacroIcons.InCrystallineConflict)
        {
            var pronouns = PronounModule.Instance();
            if (pronouns != null)
                for (var i = 1; i <= 5; i++)
                {
                    var target = pronouns->ResolvePlaceholder($"<e{i}>", 0, 0);
                    if (target != null) Add(target->EntityId);
                }
        }
        return jobs.OrderBy(j => j).ToArray();
    }

    internal EnemyActionDisplay()
    {
        try
        {
            timerMeiryo = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.Font = tk.AddFontFromFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "meiryo.ttc"),
                    new SafeFontConfig { SizePx = 28, GlyphRanges = new ushort[] { 0x20, 0x7E, 0 } })));
            timerMiedinger = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.MiedingerMid, 28));
            var currentIds = new HashSet<uint>();
            foreach (var group in Plugin.DataManager.GetSubrowExcelSheet<Lumina.Excel.Sheets.PvPActionSort>())
                foreach (var entry in group)
                {
                    if (entry.ActionType == 1) currentIds.Add(entry.Action.RowId);
                    else if (entry.ActionType == 2)
                    {
                        var route = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ActionComboRoute>().GetRowOrDefault(entry.Action.RowId);
                        if (route.HasValue) foreach (var step in route.Value.Action) if (step.RowId != 0) currentIds.Add(step.RowId);
                    }
                }
            var rows = Plugin.DataManager.GetExcelSheet<ActionRow>().Where(a => currentIds.Contains(a.RowId) && a.IsPvP && a.Icon > 0 && !string.IsNullOrWhiteSpace(a.Name.ExtractText())).ToArray();
            foreach (var job in Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().Where(j => j.JobIndex > 0))
            {
                var property = typeof(Lumina.Excel.Sheets.ClassJobCategory).GetProperty(job.Abbreviation.ExtractText());
                if (property == null) continue;
                JobNames[job.RowId] = job.Name.ExtractText();
                Jobs[job.RowId] = rows.Where(a => a.ClassJobCategory.RowId != 0 && property.GetValue(a.ClassJobCategory.Value) is true)
                    .Select(a => new ActionInfo(a.RowId, a.Name.ExtractText(), a.Icon, a.Recast100ms / 10d, a.CooldownGroup)).OrderBy(a => a.Name).ToArray();
            }
            foreach (var job in Jobs.Where(j => j.Value.Length == 0).Select(j => j.Key).ToArray()) { Jobs.Remove(job); JobNames.Remove(job); }
            if (Jobs.Count == 0) throw new InvalidOperationException("Current PvP action list is empty.");
            foreach (var action in Jobs.Values.SelectMany(a => a)) actions[action.Id] = action;
            hook = Plugin.InteropProvider.HookFromAddress<Delegates.Receive>(MemberFunctionPointers.Receive, OnAction);
            hook.Enable();
        }
        catch (Exception ex) { Error = "アクション情報・検知処理の初期化に失敗しました。"; Plugin.Log.Error(ex, "Enemy actions initialization failed"); }
    }

    internal void Update()
    {
        var previous = new Dictionary<uint, uint>(enemies);
        enemies.Clear();
        var self = Plugin.ObjectTable.LocalPlayer;
        if (owner != (self?.GameObjectId ?? 0) || territory != Plugin.ClientState.TerritoryType)
        { timers.Clear(); owner = self?.GameObjectId ?? 0; territory = Plugin.ClientState.TerritoryType; }
        if (!Enabled || self == null || Error != null) { timers.Clear(); observations.Clear(); State = "無効・未ログイン"; return; }
        if (TestMode)
        {
            State = "デュエルエリア・対戦相手待ち";
            var game = GameMain.Instance();
            if (Plugin.Condition[ConditionFlag.InDuelingArea] && game != null) AddEnemy(game->PvPDuelManager.EnemyEntityId);
        }
        else if (PvpBattleMacroIcons.InCrystallineConflict)
        {
            State = "クリスタルコンフリクトで検知中";
            var pronouns = PronounModule.Instance();
            if (pronouns != null)
                for (var i = 1; i <= 5; i++)
                {
                    var target = pronouns->ResolvePlaceholder($"<e{i}>", 0, 0);
                    if (target != null) AddEnemy(target->EntityId);
                }
        }
        else State = "クリスタルコンフリクト参加待ち";
        foreach (var key in timers.Keys.ToArray())
            if (!enemies.TryGetValue(key.Enemy, out var job) || !previous.TryGetValue(key.Enemy, out var oldJob) || oldJob != job) timers.Remove(key);
        if (TestMode && enemies.Count > 0) State = "デュエル相手を検知中（推定）";
        while (observations.TryDequeue(out var observation))
        {
            if (!enemies.TryGetValue(observation.Actor, out var job) || !Jobs.TryGetValue(job, out var available) ||
                !actions.TryGetValue(observation.Action, out var used)) continue;
            foreach (var action in available)
                if (action.Id == used.Id || (used.Group != 0 && action.Group == used.Group))
                {
                    var key = (observation.Actor, action.Id);
                    if (timers.TryGetValue(key, out var old) && old.Sequence == observation.Sequence) continue;
                    timers[key] = (observation.Time + used.Seconds, observation.Sequence, used.Seconds);
                }
        }
    }
    private void AddEnemy(uint id)
    {
        if (Plugin.ObjectTable.SearchByEntityId(id) is IPlayerCharacter p && p.GameObjectId != owner && p.CurrentHp > 0) enemies[id] = p.ClassJob.RowId;
    }
    private void OnAction(uint actor, Character* caster, Vector3* position, Header* header, TargetEffects* effects, GameObjectId* targets)
    {
        hook!.Original(actor, caster, position, header, effects, targets);
        try
        {
            if (!Enabled || header == null || header->ActionType != 1 || observations.Count >= 1024) return;
            // Copy packet values only; native pointers never survive this callback.
            observations.Enqueue((actor, header->ActionId, header->GlobalSequence, Environment.TickCount64 / 1000d));
        }
        catch (Exception ex) { if (Environment.TickCount64 >= nextLog) { nextLog = Environment.TickCount64 + 5000; Plugin.Log.Warning(ex, "Enemy action observation failed"); } }
    }
    internal void Draw()
    {
        if (!EditPosition && (!Enabled || Error != null || enemies.Count == 0)) return;
        var c = Plugin.Configuration;
        var viewport = ImGui.GetMainViewport();
        var position = new Vector2(c.EnemyActionsX, c.EnemyActionsY);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) position = new(500, 300);
        position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - new Vector2(50)));
        ImGui.SetNextWindowPos(viewport.Pos + position, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.75f);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
        if (c.EnemyActionsTransparentBackground) flags |= ImGuiWindowFlags.NoBackground;
        if (!EditPosition) flags |= ImGuiWindowFlags.NoInputs;
        if (ImGui.Begin("##devLibraEnemyActionsHud", flags))
        {
            var rows = enemies.ToArray();
            if (rows.Length == 0 && EditPosition)
            {
                ImGui.TextUnformatted("配置プレビュー：対戦相手待ち");
                foreach (var job in Jobs.Keys.Where(j => c.EnemyActionSelections.TryGetValue(j, out var ids) && ids != null && ids.Count > 0).Take(5))
                    DrawHudRow(0, job, $"サンプル / {JobNames[job]}");
                ImGui.TextDisabled("このパネルをドラッグして移動できます。");
            }
            foreach (var row in rows)
            {
                var player = Plugin.ObjectTable.SearchByEntityId(row.Key) as IPlayerCharacter;
                if (player == null) continue;
                DrawHudRow(row.Key, row.Value, $"{player.Name.TextValue} / {JobNames.GetValueOrDefault(row.Value, "")}");
            }
            if (EditPosition && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) dragging = true;
            if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                position = Vector2.Clamp(position + ImGui.GetIO().MouseDelta, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - ImGui.GetWindowSize()));
                c.EnemyActionsX = position.X; c.EnemyActionsY = position.Y;
            }
            if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { dragging = false; Plugin.SaveConfiguration(); }
        }
        ImGui.End();
    }

    private void DrawHudRow(uint enemy, uint job, string name)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(name);
        if (!Plugin.Configuration.EnemyActionSelections.TryGetValue(job, out var selected) || selected == null || selected.Count == 0)
        { ImGui.TextDisabled("表示アクション未設定"); return; }
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var ids = selected.Where(id => actions.ContainsKey(id) && Jobs.TryGetValue(job, out var available) && available.Any(a => a.Id == id)).OrderBy(id => id).ToArray();
        var c = Plugin.Configuration;
        var size = (float)Math.Clamp(c.EnemyActionIconSize, 16, 80);
        var step = size + 4;
        var jobTexture = Plugin.TextureProvider.GetFromGameIcon(62100 + job).GetWrapOrDefault();
        if (jobTexture != null) draw.AddImage(jobTexture.Handle, origin, origin + new Vector2(size));
        // Wrap action rows while keeping the job icon in a separate left column.
        var columns = Math.Max(1, Math.Min(8, (int)((ImGui.GetMainViewport().Size.X - 40 - step) / step)));
        ImGui.Dummy(new Vector2(step + Math.Min(columns, ids.Length) * step, Math.Max(1, (ids.Length + columns - 1) / columns) * step));
        using var timerFont = (c.EnemyActionTimerFont == 1 ? timerMiedinger : timerMeiryo)?.Push();
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            var texture = Plugin.TextureProvider.GetFromGameIcon(actions[id].Icon).GetWrapOrDefault();
            if (texture == null) continue;
            var start = origin + new Vector2(step + i % columns * step, i / columns * step);
            draw.AddImage(texture.Handle, start, start + new Vector2(size));
            var known = timers.TryGetValue((enemy, id), out var timer);
            var remaining = known ? Math.Max(0, timer.End - Environment.TickCount64 / 1000d) : 0;
            var label = !known ? "?" : remaining > 0 ? $"{Math.Ceiling(remaining):0}" : "0";
            if (!known) draw.AddRectFilled(start, start + new Vector2(size), 0x99000000);
            else if (remaining > 0 && timer.Duration > 0)
            {
                var progress = (float)Math.Clamp(1 - remaining / timer.Duration, 0, 1);
                var center = start + new Vector2(size / 2);
                // Start at twelve o'clock and reveal clockwise. Small angular sectors
                // feather the leading edge; the rectangle stays fully covered at use.
                Vector2 Edge(float fraction)
                {
                    var angle = fraction * MathF.Tau - MathF.PI / 2;
                    var ray = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    return center + ray * (size / 2 / Math.Max(Math.Abs(ray.X), Math.Abs(ray.Y)));
                }
                for (var sector = 0; sector < 96; sector++)
                {
                    var fraction = (sector + 0.5f) / 96;
                    var opacity = Math.Clamp((fraction - progress) / 0.06f, 0, 1);
                    if (opacity <= 0) continue;
                    var shade = (uint)(opacity * 190) << 24;
                    draw.AddTriangleFilled(center, Edge(sector / 96f), Edge((sector + 1) / 96f), shade);
                }
            }
            var measured = ImGui.CalcTextSize(label);
            var boldWidth = c.EnemyActionTimerBold ? (float.IsFinite(c.EnemyActionTimerBoldThickness) ? Math.Clamp(c.EnemyActionTimerBoldThickness, 0.3f, 1.5f) : 0.6f) : 0;
            var outlineWidth = c.EnemyActionTimerOutlineEnabled ? (float.IsFinite(c.EnemyActionTimerOutlineThickness) ? Math.Clamp(c.EnemyActionTimerOutlineThickness, 0.5f, 3) : 1) : 0;
            var margin = boldWidth + outlineWidth;
            var scale = Math.Min(size * 0.52f / ImGui.GetFontSize(), Math.Max(1, size - 4 - margin * 2) / Math.Max(1, measured.X));
            var textPosition = start + (new Vector2(size) - measured * scale) / 2;
            if (outlineWidth > 0)
                for (var direction = 0; direction < 16; direction++)
                {
                    var angle = direction * MathF.Tau / 16;
                    draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale,
                        textPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * margin,
                        ImGui.GetColorU32(c.EnemyActionTimerOutlineColor), label);
                }
            if (c.EnemyActionTimerBold)
            {
                var thickness = float.IsFinite(c.EnemyActionTimerBoldThickness) ? Math.Clamp(c.EnemyActionTimerBoldThickness, 0.3f, 1.5f) : 0.6f;
                for (var direction = 0; direction < 8; direction++)
                {
                    var angle = direction * MathF.PI / 4;
                    draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale, textPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * thickness, 0xFFFFFFFF, label);
                }
            }
            draw.AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale, textPosition, 0xFFFFFFFF, label);
        }
    }
    public void Dispose() { timerMeiryo?.Dispose(); timerMiedinger?.Dispose(); hook?.Dispose(); timers.Clear(); enemies.Clear(); observations.Clear(); }
}





