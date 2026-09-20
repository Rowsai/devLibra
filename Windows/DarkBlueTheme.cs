using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace devLibra.Windows;

/// <summary>Scoped to devLibra windows; never changes another plugin's theme.</summary>
internal sealed class DarkBlueTheme : IDisposable
{
    private int colors;
    private int variables;

    public DarkBlueTheme()
    {
        Color(ImGuiCol.Text, 0.88f, 0.94f, 1f);
        Color(ImGuiCol.TextDisabled, 0.51f, 0.64f, 0.78f);
        Color(ImGuiCol.WindowBg, 0.035f, 0.065f, 0.12f);
        Color(ImGuiCol.ChildBg, 0.045f, 0.085f, 0.15f);
        Color(ImGuiCol.PopupBg, 0.055f, 0.10f, 0.18f);
        Color(ImGuiCol.Border, 0.13f, 0.24f, 0.37f);
        Color(ImGuiCol.TitleBg, 0.035f, 0.07f, 0.13f);
        Color(ImGuiCol.TitleBgActive, 0.065f, 0.15f, 0.26f);
        Color(ImGuiCol.TitleBgCollapsed, 0.035f, 0.07f, 0.13f);
        Color(ImGuiCol.FrameBg, 0.075f, 0.135f, 0.22f);
        Color(ImGuiCol.FrameBgHovered, 0.10f, 0.22f, 0.35f);
        Color(ImGuiCol.FrameBgActive, 0.11f, 0.28f, 0.44f);
        Color(ImGuiCol.CheckMark, 0.30f, 0.75f, 1f);
        Color(ImGuiCol.SliderGrab, 0.21f, 0.57f, 0.83f);
        Color(ImGuiCol.SliderGrabActive, 0.38f, 0.80f, 1f);
        Color(ImGuiCol.Button, 0.09f, 0.22f, 0.36f);
        Color(ImGuiCol.ButtonHovered, 0.13f, 0.35f, 0.54f);
        Color(ImGuiCol.ButtonActive, 0.18f, 0.44f, 0.65f);
        Color(ImGuiCol.Header, 0.085f, 0.20f, 0.33f);
        Color(ImGuiCol.HeaderHovered, 0.12f, 0.30f, 0.46f);
        Color(ImGuiCol.HeaderActive, 0.15f, 0.37f, 0.56f);
        Color(ImGuiCol.Tab, 0.055f, 0.12f, 0.21f);
        Color(ImGuiCol.TabHovered, 0.12f, 0.32f, 0.50f);
        Color(ImGuiCol.TabActive, 0.10f, 0.27f, 0.43f);
        Color(ImGuiCol.TabUnfocused, 0.055f, 0.10f, 0.18f);
        Color(ImGuiCol.TabUnfocusedActive, 0.08f, 0.21f, 0.34f);
        Color(ImGuiCol.TableHeaderBg, 0.065f, 0.16f, 0.28f);
        Color(ImGuiCol.TableBorderStrong, 0.13f, 0.25f, 0.39f);
        Color(ImGuiCol.TableBorderLight, 0.08f, 0.16f, 0.27f);
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0, 0, 0, 0)); colors++;
        Color(ImGuiCol.TableRowBgAlt, 0.05f, 0.10f, 0.18f);
        Color(ImGuiCol.Separator, 0.13f, 0.26f, 0.40f);
        Color(ImGuiCol.SeparatorHovered, 0.23f, 0.56f, 0.80f);
        Color(ImGuiCol.SeparatorActive, 0.3f, 0.7f, 1f);
        Color(ImGuiCol.ScrollbarBg, 0.035f, 0.065f, 0.12f);
        Color(ImGuiCol.ScrollbarGrab, 0.13f, 0.25f, 0.38f);
        Color(ImGuiCol.ScrollbarGrabHovered, 0.18f, 0.37f, 0.54f);
        Color(ImGuiCol.ScrollbarGrabActive, 0.23f, 0.48f, 0.7f);
        Color(ImGuiCol.ResizeGrip, 0.12f, 0.29f, 0.44f);
        Color(ImGuiCol.ResizeGripHovered, 0.2f, 0.5f, 0.73f);
        Color(ImGuiCol.ResizeGripActive, 0.3f, 0.7f, 1f);
        Color(ImGuiCol.TextSelectedBg, 0.13f, 0.32f, 0.5f);
        Color(ImGuiCol.NavHighlight, 0.3f, 0.7f, 1f);
        Color(ImGuiCol.DragDropTarget, 0.4f, 0.8f, 1f);
        Vector(ImGuiStyleVar.WindowPadding, 20, 16);
        Vector(ImGuiStyleVar.FramePadding, 10, 6);
        Vector(ImGuiStyleVar.ItemSpacing, 10, 9);
        Vector(ImGuiStyleVar.CellPadding, 10, 9);
        Scalar(ImGuiStyleVar.WindowRounding, 9);
        Scalar(ImGuiStyleVar.ChildRounding, 7);
        Scalar(ImGuiStyleVar.FrameRounding, 5);
        Scalar(ImGuiStyleVar.PopupRounding, 6);
        Scalar(ImGuiStyleVar.TabRounding, 5);
        Scalar(ImGuiStyleVar.GrabRounding, 4);
    }

    private void Color(ImGuiCol name, float r, float g, float b)
    { ImGui.PushStyleColor(name, new Vector4(r, g, b, 1)); colors++; }
    private void Vector(ImGuiStyleVar name, float x, float y)
    { ImGui.PushStyleVar(name, new Vector2(x, y)); variables++; }
    private void Scalar(ImGuiStyleVar name, float value)
    { ImGui.PushStyleVar(name, value); variables++; }
    public void Dispose()
    {
        ImGui.PopStyleVar(variables);
        ImGui.PopStyleColor(colors);
    }
}
