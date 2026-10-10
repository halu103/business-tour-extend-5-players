using System;
using System.Collections.Generic;
using BusinessTour;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Pawn capacity is gameplay initialization, not a side effect of a successful
// visual mesh. Keep it available even when presentation falls back to native.
internal static class FifthPawnPlacement
{
    private static IntPtr _map;
    private static readonly List<Snapshot> Cells = new();

    internal static void Ensure(MapView map)
    {
        if (!ModState.IsSpecialMapActive) return;
        var ordered=new SortedDictionary<int,CellView>();
        foreach(int id in map.CellsViewIds)
        {
            ICellView cell=map.GetCellViewById(id);
            if(cell!=null)ordered[id]=new CellView(cell.Pointer);
        }
        var cells=new List<CellView>(ordered.Values);
        bool same=_map==map.Pointer && cells.Count==Cells.Count;
        for(int index=0;same && index<cells.Count;index++)same=Cells[index].Pointer==cells[index].Pointer;
        if(same)return;
        // Visual snapshots use the expanded positions as their native baseline.
        // Release them first, then put the exact original pawn arrays back.
        PentagonalSpriteGeometry.Reset();
        Restore();
        try
        {
            foreach(CellView cell in cells)
            {
                var snapshot=new Snapshot(cell);Cells.Add(snapshot);snapshot.Expand();
            }
            _map=map.Pointer;
            Plugin.ModLog.LogInfo($"Prepared fifth native peripheral pawn place on {Cells.Count} cells independently of board visuals.");
        }
        catch
        {
            Restore();throw;
        }
    }

    internal static void Restore()
    {
        foreach(Snapshot snapshot in Cells)
            try{snapshot.Restore();}catch(Exception ex){Plugin.ModLog.LogDebug($"Pawn cell released: {ex.Message}");}
        Cells.Clear();_map=IntPtr.Zero;
    }

    private sealed class Snapshot
    {
        private readonly CellView _cell;
        internal IntPtr Pointer=>_cell.Pointer;
        private readonly Il2CppReferenceArray<Transform> _slots;
        private readonly Il2CppStructArray<Vector3> _points;
        private readonly Il2CppStructArray<int> _orders;
        private readonly Il2CppSystem.Collections.Generic.List<int> _free;
        private readonly List<int> _freeValues=new();
        private Transform _extra;

        internal Snapshot(CellView cell)
        {
            _cell=cell;_slots=cell._slots;_points=cell._iconVisualPositions;
            _orders=cell.IconVisualOrders;_free=cell.FreePlaces;
            if(_slots==null || _slots.Length<5 || _points==null || _points.Length<_slots.Length ||
                _orders==null || _orders.Length<_slots.Length || _free==null)
                throw new InvalidOperationException("Incomplete native pawn placement data.");
            foreach(Transform slot in _slots)
                if(slot==null)throw new InvalidOperationException("Missing native pawn position.");
            foreach(int value in _free)_freeValues.Add(value);
        }
        internal void Expand()=>_extra=PentagonalBoardPatch.EnsureFifthPawnPlace(_cell);
        internal void Restore()
        {
            try
            {
                if(_cell!=null){_cell._slots=_slots;_cell._iconVisualPositions=_points;_cell.IconVisualOrders=_orders;}
                _free.Clear();foreach(int value in _freeValues)_free.Add(value);
            }
            finally
            {
                try{if(_extra!=null)UnityEngine.Object.Destroy(_extra.gameObject);}catch{}
            }
        }
    }
}
