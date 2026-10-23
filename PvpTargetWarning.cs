using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace devLibra;

internal sealed unsafe class PvpTargetWarning : IDisposable
{
    private IFontHandle? meiryo;
    private IFontHandle? miedinger;
    private long previewUntil;
    private bool dragging;
    private bool wasDetected;
    private bool soundTestRequested;
    private readonly List<uint> targetingJobs = new(5);
    private static readonly uint[] PreviewJobs = [19, 34, 33]; // Paladin, Samurai, Astrologian.
    internal void TestSound() => soundTestRequested = true;
    internal bool EditPosition;
    private string? fontError;
    internal string? FontError => fontError ?? (meiryo?.LoadException != null || miedinger?.LoadException != null ? "フォントの読み込みに失敗しました。標準フォントで表示します。" : null);
    internal int TargetCount { get; private set; }
    internal void Preview() => previewUntil = Environment.TickCount64 + 10000;

    internal PvpTargetWarning()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "meiryo.ttc");
            meiryo = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.Font = tk.AddFontFromFile(path, new SafeFontConfig { SizePx = 28, GlyphRanges = new ushort[] { 0x20, 0xFFEF, 0 } })));
            miedinger = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
            {
                var font = tk.AddGameGlyphs(new GameFontStyle(GameFontFamily.MiedingerMid, 28), new ushort[] { 0x20, 0x7E, 0 }, default);
                tk.AddFontFromFile(path, new SafeFontConfig { SizePx = 28, MergeFont = font, GlyphRanges = new ushort[] { 0x80, 0xFFEF, 0 } });
                tk.Font = font;
            }));
        }
        catch (Exception ex) { fontError = "フォントを読み込めないため標準フォントを使用します。"; Plugin.Log.Warning(ex, "Warning font initialization failed."); }
    }

    internal void Update()
    {
        var previouslyDetected = wasDetected;
        wasDetected = false;
        TargetCount = 0;
        targetingJobs.Clear();
        if (soundTestRequested)
        {
            soundTestRequested = false;
            if (Plugin.Configuration.PvpWarningSoundEnabled) PlayWarningSound();
        }
        var c = Plugin.Configuration;
        var self = Plugin.ObjectTable.LocalPlayer;
        if (!c.ForPvpBattleEnabled || !c.PvpTargetWarningEnabled || self == null || self.CurrentHp == 0 ||
            !PvpBattleMacroIcons.InCrystallineConflict || !Plugin.Condition[ConditionFlag.InCombat]) return;
        var pronouns = PronounModule.Instance();
        if (pronouns == null) return;
        var seen = new HashSet<ulong>();
        for (var i = 1; i <= 5; i++)
        {
            var target = pronouns->ResolvePlaceholder($"<e{i}>", 0, 0);
            if (target == null) continue;
            foreach (var obj in Plugin.ObjectTable)
                if (obj.Address == (nint)target && obj is IPlayerCharacter enemy && enemy.CurrentHp > 0 &&
                    enemy.TargetObjectId == self.GameObjectId && seen.Add(enemy.GameObjectId))
                {
                    TargetCount++;
                    targetingJobs.Add(enemy.ClassJob.RowId);
                    break;
                }
        }
        wasDetected = TargetCount >= Math.Clamp(c.PvpWarningTargetThreshold, 2, 5);
        if (!previouslyDetected && wasDetected && c.PvpWarningSoundEnabled) PlayWarningSound();
    }

    private static void PlayWarningSound()
    {
        try
        {
            var c = Plugin.Configuration;
            if (!c.PvpWarningSoundEnabled) return;
            var volume = float.IsFinite(c.PvpWarningSoundVolume) ? Math.Clamp(c.PvpWarningSoundVolume, 0, 100) / 100 : 1;
            if (volume <= 0) return;
            FFXIVClientStructs.FFXIV.Client.Sound.SoundData* pad = null;
            FFXIVClientStructs.FFXIV.Client.Sound.SoundData* sound = null;
            // Chat SE 1..16 map to UI sound IDs 37..52. Only touch the instances
            // returned by this playback; never modify a global sound bus or config.
            FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlaySoundEffect((uint)Math.Clamp(c.PvpWarningSoundId, 1, 16) + 36, &pad, &sound);
            if (sound != null) sound->SetVolume(sound->GetVolume() * volume);
            if (pad != null && pad != sound) pad->SetVolume(pad->GetVolume() * volume);
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "Failed to play PvP warning sound."); }
    }

    internal void Draw()
    {
        var c = Plugin.Configuration;
        if (!EditPosition && Environment.TickCount64 >= previewUntil &&
            !(c.ForPvpBattleEnabled && c.PvpTargetWarningEnabled && TargetCount >= Math.Clamp(c.PvpWarningTargetThreshold, 2, 5) && PvpBattleMacroIcons.InCrystallineConflict && Plugin.Condition[ConditionFlag.InCombat])) return;
        using var font = (c.PvpWarningFont == 1 ? miedinger : meiryo)?.Push();
        var viewport = ImGui.GetMainViewport();
        var position = new Vector2(c.PvpWarningX, c.PvpWarningY);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) position = new Vector2(400, 200);
        position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - new Vector2(40)));
        ImGui.SetNextWindowPos(viewport.Pos + position, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(EditPosition ? 0.65f : 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
        if (!EditPosition) flags |= ImGuiWindowFlags.NoInputs;
        if (ImGui.Begin("##devLibraTargetWarning", flags))
        {
            var text = string.IsNullOrWhiteSpace(c.PvpWarningText) ? "Multiple enemy targets detected!" : c.PvpWarningText;
            var size = float.IsFinite(c.PvpWarningFontSize) ? Math.Clamp(c.PvpWarningFontSize, 12, 72) : 28;
            var thickness = c.PvpWarningOutlineEnabled
                ? (float.IsFinite(c.PvpWarningOutlineThickness) ? Math.Clamp(c.PvpWarningOutlineThickness, 0.5f, 3) : 1) : 0;
            IReadOnlyList<uint> jobs = Environment.TickCount64 < previewUntil || EditPosition ? PreviewJobs : targetingJobs;
            var textSize = ImGui.CalcTextSize(text, false) * (size / ImGui.GetFontSize());
            var iconSize = Math.Max(24, size);
            var gap = 4f;
            var iconsWidth = jobs.Count == 0 ? 0 : jobs.Count * iconSize + (jobs.Count - 1) * gap;
            var glowRadius = c.PvpWarningRainbowEnabled ? Math.Clamp(size * 0.18f, 4, 10) : 0;
            var padding = Math.Max(thickness, glowRadius);
            var width = Math.Max(textSize.X + padding * 2, iconsWidth);
            var origin = ImGui.GetCursorScreenPos();
            var textPosition = origin + new Vector2((width - textSize.X) / 2, padding);
            var iconsY = textSize.Y + padding * 2 + 6;
            ImGui.Dummy(new Vector2(width, jobs.Count == 0 ? textSize.Y + padding * 2 : iconsY + iconSize));
            var draw = ImGui.GetWindowDrawList();
            var textColor = c.PvpWarningTextColor;
            if (c.PvpWarningRainbowEnabled)
            {
                // Smooth six-second hue cycle; no strobing or global style changes.
                var hue = (float)(ImGui.GetTime() % 6 / 6);
                float red = 0, green = 0, blue = 0;
                ImGui.ColorConvertHSVtoRGB(hue, 0.72f, 1, &red, &green, &blue);
                textColor = new Vector4(red, green, blue, 1);
                for (var ring = 3; ring >= 1; ring--)
                {
                    var glow = new Vector4(red, green, blue, ring == 1 ? 0.10f : 0.035f);
                    for (var i = 0; i < 12; i++)
                    {
                        var angle = i * MathF.Tau / 12;
                        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (glowRadius * ring / 3);
                        draw.AddText(ImGui.GetFont(), size, textPosition + offset, ImGui.GetColorU32(glow), text);
                    }
                }
            }
            if (thickness > 0)
                for (var i = 0; i < 16; i++)
                {
                    var angle = i * MathF.Tau / 16;
                    var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * thickness;
                    draw.AddText(ImGui.GetFont(), size, textPosition + offset, ImGui.GetColorU32(c.PvpWarningOutlineColor), text);
                }
            draw.AddText(ImGui.GetFont(), size, textPosition, ImGui.GetColorU32(textColor), text);
            for (var i = 0; i < jobs.Count; i++)
            {
                if (jobs[i] == 0) continue;
                var texture = Plugin.TextureProvider.GetFromGameIcon(62100 + jobs[i]).GetWrapOrDefault();
                if (texture == null) continue;
                var start = origin + new Vector2((width - iconsWidth) / 2 + i * (iconSize + gap), iconsY);
                draw.AddImage(texture.Handle, start, start + new Vector2(iconSize));
            }
            if (EditPosition && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) dragging = true;
            if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                position += ImGui.GetIO().MouseDelta;
                position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - ImGui.GetWindowSize()));
                c.PvpWarningX = position.X; c.PvpWarningY = position.Y;
            }
            if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { dragging = false; Plugin.SaveConfiguration(); }
        }
        ImGui.End();
    }
    public void Dispose() { meiryo?.Dispose(); miedinger?.Dispose(); }
}






