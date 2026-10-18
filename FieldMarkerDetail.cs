using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace devLibra;

internal sealed unsafe class FieldMarkerDetail
{
    internal readonly record struct Marker(bool Active, Vector3 Position);
    internal Marker[] Markers { get; private set; } = new Marker[8];
    internal string Message { get; private set; } = string.Empty;
    internal bool Busy => queued != null || waiting != null || sequence.Count > 0;
    internal int Generation { get; private set; }
    private sealed record Request(int Index, Vector3 Position, uint Territory, ulong Owner, bool Active = true);
    private readonly Queue<Request> sequence = new();
    private long nextPlacement;
    private Request? queued;
    private Request? waiting;
    private long deadline;
    private uint territory;
    private ulong owner;
    internal bool Available { get; private set; }
    internal static readonly string[] Names = ["A", "B", "C", "D", "1", "2", "3", "4"];
    internal static readonly uint[] Icons = [61241, 61242, 61243, 61247, 61244, 61245, 61246, 61248];

    internal static (string Key, string Name) CurrentContent()
    {
        var id = Plugin.ClientState.TerritoryType;
        var territoryRow = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(id);
        if (territoryRow is { } row)
        {
            var content = row.ContentFinderCondition;
            if (content.RowId != 0) return ($"content:{content.RowId}", content.Value.Name.ExtractText());
            return ($"territory:{id}", row.PlaceName.Value.Name.ExtractText());
        }
        return ($"territory:{id}", $"エリア{id}");
    }

    internal void RequestSet(SavedFieldMarkerSet set)
    {
        if (Busy || !Available || !set.Valid) return;
        for (var i = 0; i < 8; i++)
            sequence.Enqueue(new Request(i, set.Markers[i].Position, territory, owner, set.Markers[i].Active));
        Message = "マーカーセットを順番に反映しています…";
    }

    internal void CancelSet()
    {
        sequence.Clear(); queued = waiting = null;
        Message = "以降の配置を中止しました。反映済みのマーカーはそのままです。";
    }

    internal void RequestPlacement(int index, Vector3 position)
    {
        if (Busy || !Available || index < 0 || index >= 8) return;
        queued = new Request(index, position, territory, owner);
        Message = $"{Names[index]}：配置を要求しています…";
    }

    internal void Update()
    {
        var currentOwner = Plugin.ObjectTable.LocalPlayer?.GameObjectId ?? 0;
        if (owner != currentOwner || territory != Plugin.ClientState.TerritoryType)
        {
            owner = currentOwner; territory = Plugin.ClientState.TerritoryType;
            queued = waiting = null; Message = string.Empty; Generation++;
            sequence.Clear(); nextPlacement = 0;
        }
        var controller = currentOwner == 0 ? null : MarkingController.Instance();
        Available = controller != null;
        var snapshot = new Marker[8];
        if (controller != null)
            for (var i = 0; i < 8; i++)
            {
                var marker = controller->FieldMarkers[i];
                snapshot[i] = new Marker(marker.Active, marker.Position);
            }
        Markers = snapshot;
        if (!Available) { queued = waiting = null; sequence.Clear(); return; }
        if (Busy && Plugin.Condition[ConditionFlag.InCombat])
        {
            CancelSet(); Message = "戦闘が始まったため配置を中止しました。"; return;
        }
        if (waiting is { } pending)
        {
            if (snapshot[pending.Index].Active == pending.Active && (!pending.Active || FieldMarkerRules.Matches(snapshot[pending.Index].Position, pending.Position)))
            {
                Message = sequence.Count == 0 ? "マーカーの反映を確認しました。" : $"{Names[pending.Index]}：反映済み。残り{sequence.Count}件…"; waiting = null;
            }
            else if (Environment.TickCount64 >= deadline)
            {
                Message = "配置を確認できないため中止しました。反映済みのマーカーはそのままです。"; waiting = null; sequence.Clear();
            }
        }
        if (waiting != null || Environment.TickCount64 < nextPlacement) return;
        if (queued == null && sequence.Count > 0) queued = sequence.Dequeue();
        if (queued is not { } request) return;
        queued = null;
        if (request.Owner != owner || request.Territory != territory) return;
        if (Plugin.Condition[ConditionFlag.InCombat])
        {
            Message = "戦闘中はフィールドマーカーを配置できません。";
            return;
        }
        try
        {
            var position = request.Position;
            var result = request.Active ? controller->PlaceFieldMarker((uint)request.Index, &position) :
                snapshot[request.Index].Active ? controller->ClearFieldMarker((uint)request.Index) : (byte)0;
            nextPlacement = Environment.TickCount64 + 600;
            if (result != 0)
            {
                Message = $"ゲームが配置を受け付けませんでした（結果コード：{result}）。配置制限や連続操作を確認してください。";
                sequence.Clear();
                return;
            }
            waiting = request; deadline = Environment.TickCount64 + 3000;
            Message = $"{Names[request.Index]}：配置結果を確認しています…";
        }
        catch (Exception ex)
        {
            Message = "配置処理に失敗しました。";
            sequence.Clear();
            Plugin.Log.Error(ex, "Field Marker Detail placement failed.");
        }
    }
}
