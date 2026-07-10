using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PeanutsPlugin.Windows;

/// <summary>
/// Wiederverwendbare, selbst gezeichnete Diagramm-Bausteine auf Basis von
/// ImGuis Draw-List (kein fertiges Chart-Widget in ImGui vorhanden).
/// </summary>
public static class ChartHelpers
{
    /// <summary>Feste Farbpalette für Diagrammsegmente, zyklisch wiederverwendet.</summary>
    public static readonly Vector4[] Palette =
    {
        new(0.90f, 0.55f, 0.10f, 1f), // Orange
        new(0.30f, 0.65f, 0.95f, 1f), // Blau
        new(0.35f, 0.80f, 0.40f, 1f), // Grün
        new(0.85f, 0.30f, 0.35f, 1f), // Rot
        new(0.65f, 0.45f, 0.90f, 1f), // Violett
        new(0.95f, 0.80f, 0.25f, 1f), // Gelb
        new(0.30f, 0.80f, 0.75f, 1f), // Türkis
        new(0.90f, 0.55f, 0.75f, 1f), // Pink
        new(0.55f, 0.55f, 0.55f, 1f), // Grau
        new(0.60f, 0.75f, 0.30f, 1f), // Olive
    };

    public record Segment(string Label, float Value, Vector4 Color);

    /// <summary>
    /// Zeichnet ein Donut-Diagramm (Kreisdiagramm mit Loch in der Mitte) für
    /// die übergebenen Segmente, plus eine Legende mit Prozentwerten darunter.
    /// </summary>
    public static void DrawDonutChart(List<Segment> segments, float radius, string? centerLabel = null)
    {
        var total = segments.Sum(s => s.Value);
        if (total <= 0f)
        {
            ImGui.TextDisabled(Loc.Get("Keine Daten für das Diagramm.", "No data for the chart."));
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var size = radius * 2f;
        var center = new Vector2(cursor.X + radius, cursor.Y + radius);

        var startAngle = -MathF.PI / 2f; // 12-Uhr-Position als Start
        foreach (var seg in segments)
        {
            if (seg.Value <= 0f)
                continue;

            var fraction = seg.Value / total;
            var endAngle = startAngle + fraction * MathF.PI * 2f;
            var color = ImGui.ColorConvertFloat4ToU32(seg.Color);

            drawList.PathArcTo(center, radius, startAngle, endAngle, 64);
            drawList.PathLineTo(center);
            drawList.PathFillConvex(color);

            startAngle = endAngle;
        }

        // Donut-Loch: innerer Kreis in der Theme-Hintergrundfarbe, damit es
        // sowohl in dunklen als auch in hellen Dalamud-Themes zum Hintergrund
        // passt (früher fest dunkelgrau -> heller Fleck in hellen Themes).
        // Über GetColorU32 (kein Zeiger, kein unsafe nötig); Alpha per
        // 0xFF000000 auf voll gesetzt, damit das Diagramm nicht durchscheint.
        var bgColor = ImGui.GetColorU32(ImGuiCol.WindowBg) | 0xFF000000u;
        drawList.AddCircleFilled(center, radius * 0.55f, bgColor, 64);

        ImGui.Dummy(new Vector2(size, size));

        if (!string.IsNullOrEmpty(centerLabel))
        {
            var textSize = ImGui.CalcTextSize(centerLabel);
            var savedPos = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(center.X - textSize.X / 2f, center.Y - textSize.Y / 2f));
            ImGui.TextUnformatted(centerLabel);
            ImGui.SetCursorScreenPos(savedPos);
        }

        ImGui.Spacing();
        foreach (var seg in segments)
        {
            if (seg.Value <= 0f)
                continue;

            var pct = seg.Value / total * 100f;
            ImGui.ColorButton($"##legend_{seg.Label}", seg.Color,
                ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoBorder, new Vector2(14, 14));
            ImGui.SameLine();
            ImGui.TextUnformatted($"{seg.Label}: {seg.Value:N0} ({pct:F1}%)");
        }
    }

    /// <summary>
    /// Zeichnet einen einzelnen horizontalen gestapelten Balken (die
    /// Segmente ergeben zusammen 100% der Balkenbreite), plus Legende
    /// mit Prozentwerten darunter.
    /// </summary>
    public static void DrawStackedBar(List<Segment> segments, float width, float height)
    {
        var total = segments.Sum(s => s.Value);
        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();

        if (total <= 0f)
        {
            drawList.AddRectFilled(cursor, new Vector2(cursor.X + width, cursor.Y + height),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, 1f)));
        }
        else
        {
            var x = cursor.X;
            foreach (var seg in segments)
            {
                if (seg.Value <= 0f)
                    continue;

                var segWidth = width * (seg.Value / total);
                var color = ImGui.ColorConvertFloat4ToU32(seg.Color);
                drawList.AddRectFilled(new Vector2(x, cursor.Y), new Vector2(x + segWidth, cursor.Y + height), color);
                x += segWidth;
            }
        }

        ImGui.Dummy(new Vector2(width, height));

        if (total <= 0f)
            return;

        foreach (var seg in segments)
        {
            if (seg.Value <= 0f)
                continue;

            var pct = seg.Value / total * 100f;
            ImGui.ColorButton($"##legend_{seg.Label}", seg.Color,
                ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoBorder, new Vector2(12, 12));
            ImGui.SameLine();
            ImGui.TextUnformatted($"{seg.Label}: {seg.Value:N0} ({pct:F1}%)");
        }
    }

    /// <summary>Fortschrittsbalken für Slot-Belegung, in der übergebenen Ampelfarbe eingefärbt.</summary>
    public static void DrawSlotBar(int free, int max, Vector4 color, float width = 80f)
    {
        var fraction = max > 0 ? Math.Clamp(free / (float)max, 0f, 1f) : 0f;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, color);
        ImGui.ProgressBar(fraction, new Vector2(width, 16), max > 0 ? $"{free}/{max}" : "-");
        ImGui.PopStyleColor();
    }
}
