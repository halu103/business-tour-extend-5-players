using System;
using System.Collections.Generic;
using BusinessTour;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace BusinessTourFiveRealms;

// The board silhouette must come from one shared geometry, not the four
// differently sized native corner sprites. Native textures are decoration on
// these explicit tile surfaces; native controllers consume the expanded graph.
internal static class CanonicalBoardPresentation
{
    private static BoardState _current;
    internal static bool IsActive => _current != null;

    internal static void Apply(MapView map, ICameraService cameraService)
    {
        if (!ModState.IsSpecialMapActive) { Restore(); return; }
        BoardState prepared = null;
        try
        {
            var ordered = new SortedDictionary<int, CellView>();
            foreach (int id in map.CellsViewIds)
            {
                ICellView view = map.GetCellViewById(id);
                if (view != null) ordered[id] = new CellView(view.Pointer);
            }
            var cells = new List<CellView>(ordered.Values);
            if (_current != null && _current.Matches(map, cells)) { SyncAll(); return; }
            PentagonalSpriteGeometry.Reset();
            if (cells.Count < 8 || cells.Count % 4 != 0)
                throw new InvalidOperationException("Unsupported native board graph.");
            var faces = new List<Vector3[]>();
            var centers = new Vector3[cells.Count];
            for (int index = 0; index < cells.Count; index++)
            {
                Vector3[] face = ReadFace(cells[index]);
                faces.Add(face);
                foreach (Vector3 point in face) centers[index] += point / face.Length;
            }
            PentagonWarp reference = PentagonWarp.Create(centers);
            float radiusX = 0f, radiusY = 0f;
            foreach (Vector2 point in reference.Target)
            {
                radiusX = Mathf.Max(radiusX, Mathf.Abs(point.x - reference.Center.x));
                radiusY = Mathf.Max(radiusY, Mathf.Abs(point.y - reference.Center.y));
            }
            float cross = (reference.Source[0].x-reference.Center.x)*(reference.Source[1].y-reference.Center.y)-
                (reference.Source[0].y-reference.Center.y)*(reference.Source[1].x-reference.Center.x);
            var geometry = CanonicalBoardGeometry.Create(
                new NVector2(reference.Center.x, reference.Center.y), radiusX * 1.13f,
                radiusY * 1.13f, cells.Count, Math.Sign(cross),
                cells.Count==FivePlayerTopologyRules.ExpandedCellCount?
                    BoardLabelLayoutRules.FivePlayerInnerScale:CanonicalBoardGeometry.DefaultInnerScale);
            float depth=0f;
            foreach(Vector3 point in centers) depth+=(reference.PlaneXY?point.z:point.y)/centers.Length;
            prepared = new BoardState(map, geometry, reference.PlaneXY, depth);
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2 tangent = reference.Source[(index + 1) % cells.Count] -
                    reference.Source[(index + cells.Count - 1) % cells.Count];
                var snapshot = new TileState(cells[index], faces[index], reference.Center, tangent,
                    geometry.Cells[index], reference.PlaneXY,IsNativeChance(map,cells[index]));
                prepared.Tiles.Add(snapshot);
            }
            // Validate every footprint/UV before changing the first native cell.
            foreach (TileState snapshot in prepared.Tiles) snapshot.Apply();
            map.SortCellsByOrder();
            cameraService?.SetCameraToDefaultOnBoardSettings(map.MapSize);
            prepared.Fit(cameraService?.MainCamera ?? Camera.main);
            prepared.CreateBase();
            _current = prepared;
            Plugin.ModLog.LogInfo($"Canonical pentagonal board prepared: {cells.Count} native cells, shared tile boundaries, uniform base; no prefab-outline warp.");
        }
        catch (Exception ex)
        {
            prepared?.Restore();
            Plugin.ModLog.LogError($"Canonical board failed safely; native presentation restored: {ex}");
        }
    }

    private static bool IsNativeChance(MapView map,CellView cell)
    {
        // Read the native model; the same art catalogue is also used by
        // upright service prefabs and must not identify those by their names.
        try { return map._locationManager?.Map?.MapConfig?.GetCellData(cell._cellId)?.CellType==CellType.Chance; }
        catch { return false; }
    }

    private static Vector3[] ReadFace(CellView cell)
    {
        var corners = cell._cornersArray;
        if (corners == null || corners.Length != 4)
            throw new InvalidOperationException($"Cell {cell.name} does not have four logical top corners.");
        var result = new Vector3[4];
        for (int index = 0; index < 4; index++) result[index] = cell.transform.TransformPoint(corners[index]);
        return result;
    }

    internal static void SyncAll()
    {
        if (_current == null) return;
        foreach (TileState tile in _current.Tiles) tile.Sync(_current.LabelCamera);
    }

    internal static void Sync(CellRenderer renderer)
    {
        if (_current == null || renderer?._renderer == null) return;
        foreach (TileState tile in _current.Tiles)
            if (tile.Renderer.Pointer == renderer._renderer.Pointer) { tile.Sync(_current.LabelCamera); return; }
    }

    internal static void ReplaceBackground(GameView view)
    {
        if (_current == null || view?._background == null) return;
        _current.HideBackground(view._background);
    }

    internal static void Restore()
    {
        BoardState previous = _current;
        _current = null;
        previous?.Restore();
    }

    private sealed class BoardState
    {
        internal readonly MapView Map;
        internal readonly CanonicalBoardGeometry Geometry;
        internal readonly bool PlaneXY;
        private readonly float _planeDepth;
        internal readonly List<TileState> Tiles = new();
        private readonly List<Surface> _surfaces = new();
        private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, bool ForceOff)> _hidden = new();
        private Camera _camera;
        private Vector3 _cameraPosition;
        private float _cameraSize;
        internal Camera LabelCamera=>_camera ?? Camera.main;

        internal BoardState(MapView map, CanonicalBoardGeometry geometry, bool planeXY, float planeDepth)
        { Map = map; Geometry = geometry; PlaneXY = planeXY; _planeDepth=planeDepth; }

        internal bool Matches(MapView map, List<CellView> cells)
        {
            if (Map.Pointer != map.Pointer || cells.Count != Tiles.Count) return false;
            for (int index = 0; index < cells.Count; index++)
                if (!Tiles[index].Matches(cells[index])) return false;
            return true;
        }

        internal Vector3 Point(NVector2 point) => PlaneXY ? new Vector3(point.X, point.Y, _planeDepth) : new Vector3(point.X, _planeDepth, point.Y);

        internal void CreateBase()
        {
            if (Tiles.Count == 0) return;
            SpriteRenderer source = Tiles[0].Renderer;
            int order = int.MaxValue;
            foreach (TileState tile in Tiles) order = Math.Min(order, tile.Renderer.sortingOrder);
            var outer = new List<Vector3>();
            var inner = new List<Vector3>();
            foreach (NVector2 point in Geometry.OuterCorners) outer.Add(Point(point));
            foreach (NVector2 point in Geometry.InnerCorners) inner.Add(Point(point));
            // One plinth below the entire board replaces four baked square rims.
            var down = new List<Vector3>();
            foreach (Vector3 point in outer) down.Add(point + (PlaneXY ? Vector3.down : Vector3.back) * 0.20f);
            Transform parent = Tiles[0].RootParent;
            _surfaces.Add(Surface.Solid(parent, source, down, new Color(0.40f, 0.34f, 0.29f, 1f), order-4));
            _surfaces.Add(Surface.Solid(parent, source, outer, new Color(0.85f, 0.84f, 0.79f, 1f), order-3));
            _surfaces.Add(Surface.Solid(parent, source, inner, new Color(0.63f, 0.72f, 0.38f, 1f), order-2));
            // The inner field has its own five-sided park ring, independent of
            // the square bitmap's baked road. Leave the playable faces outside.
            for(int edge=0;edge<Geometry.InnerCorners.Count;edge++)
            {
                NVector2 start=Geometry.InnerCorners[edge]-Geometry.Center;
                NVector2 end=Geometry.InnerCorners[(edge+1)%Geometry.InnerCorners.Count]-Geometry.Center;
                var road=new[]{Point(Geometry.Center+start*0.985f),Point(Geometry.Center+end*0.985f),
                    Point(Geometry.Center+end*0.895f),Point(Geometry.Center+start*0.895f)};
                _surfaces.Add(Surface.Solid(parent,source,road,new Color(0.47f,0.50f,0.49f,1f),order-1));
                var grass=new[]{Point(Geometry.Center),Point(Geometry.Center+start*0.875f),Point(Geometry.Center+end*0.875f)};
                _surfaces.Add(Surface.Solid(parent,source,grass,edge%2==0?new Color(0.67f,0.76f,0.41f,1f):new Color(0.65f,0.74f,0.40f,1f),order-1));
                for(int dash=0;dash<7;dash++)
                {
                    float from=(dash+0.27f)/7f,to=(dash+0.60f)/7f;
                    NVector2 a=NVector2.Lerp(start,end,from),b=NVector2.Lerp(start,end,to);
                    var stripe=new[]{Point(Geometry.Center+a*0.945f),Point(Geometry.Center+b*0.945f),
                        Point(Geometry.Center+b*0.936f),Point(Geometry.Center+a*0.936f)};
                    _surfaces.Add(Surface.Solid(parent,source,stripe,new Color(0.87f,0.88f,0.82f,1f),order));
                }
            }
        }

        internal void Fit(Camera camera)
        {
            if (camera == null || !camera.orthographic) return;
            _camera = camera; _cameraPosition = camera.transform.position; _cameraSize = camera.orthographicSize;
            float left=float.PositiveInfinity, right=float.NegativeInfinity, bottom=float.PositiveInfinity, top=float.NegativeInfinity;
            foreach (NVector2 point in Geometry.OuterCorners)
            {
                Vector3 local=camera.transform.InverseTransformPoint(Point(point));
                left=Mathf.Min(left,local.x); right=Mathf.Max(right,local.x);
                bottom=Mathf.Min(bottom,local.y); top=Mathf.Max(top,local.y);
            }
            OrthographicBoardFrame frame=BoardViewportLayoutRules.Fit(left,right,bottom,top,camera.aspect);
            camera.transform.position += camera.transform.right * frame.Offset.X + camera.transform.up * frame.Offset.Y;
            camera.orthographicSize=frame.Size;
            foreach(TileState tile in Tiles) tile.FitLabel(camera);
        }

        internal void HideBackground(LocationBackground background)
        {
            HideStaticSprites(background.transform, background);
        }

        private void HideStaticSprites(Transform node, LocationBackground background)
        {
            // Never hide the native map, characters, timer or money objects.
            if (node == null || node.Pointer == background._locationParent?.transform.Pointer ||
                node.Pointer == background._charactersParent?.transform.Pointer || node.Pointer == background._timer?.transform.Pointer) return;
            if (background._inventoryContainers != null)
                foreach (Transform container in background._inventoryContainers)
                    if (container != null && node.Pointer == container.Pointer) return;
            SpriteRenderer renderer=node.GetComponent<SpriteRenderer>();
            if (renderer != null && !_hidden.ContainsKey(renderer.Pointer))
            {
                _hidden.Add(renderer.Pointer,(renderer,renderer.forceRenderingOff));
                renderer.forceRenderingOff=true;
                Plugin.ModLog.LogInfo($"Replaced static native square background sprite {node.name} with canonical pentagonal base.");
            }
            for(int child=0;child<node.childCount;child++) HideStaticSprites(node.GetChild(child),background);
        }

        internal void Restore()
        {
            foreach (TileState tile in Tiles) try { tile.Restore(); } catch(Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
            try { Map.SortCellsByOrder(); } catch { }
            foreach(Surface surface in _surfaces) surface.Dispose();
            foreach(var entry in _hidden.Values) try { if(entry.Renderer!=null)entry.Renderer.forceRenderingOff=entry.ForceOff; } catch { }
            try { if(_camera!=null){_camera.transform.position=_cameraPosition;_camera.orthographicSize=_cameraSize;} } catch { }
        }
    }

    private sealed class TileState
    {
        private readonly CellView _cell;
        internal readonly SpriteRenderer Renderer;
        internal Transform RootParent => _cell.transform.parent;
        private readonly Vector3 _rootPosition;
        private readonly Matrix4x4 _sourceMatrix;
        private readonly Vector3[] _sourceQuad;
        private readonly Vector3[] _targetQuad;
        private readonly Vector3[] _renderQuad;
        private readonly int[] _renderIndices;
        private readonly Vector3 _center;
        private readonly bool _planeXY;
        private readonly float _planeDepth;
        private readonly bool _forceOff;
        private readonly Il2CppStructArray<Vector3> _corners;
        private readonly Il2CppReferenceArray<Transform> _slots;
        private readonly Il2CppStructArray<Vector3> _points;
        private readonly Vector3[] _pointValues;
        private readonly Il2CppStructArray<int> _orders;
        private readonly List<Vector3> _slotPositions=new();
        private readonly List<int> _freePlaces=new();
        private readonly Il2CppSystem.Collections.Generic.List<int> _nativeFreePlaces;
        private readonly PolygonCollider2D _collider;
        private readonly List<Il2CppStructArray<Vector2>> _colliderPaths=new();
        private readonly Vector3 _press;
        private Transform _extra;
        private Surface _ground;
        private Surface _bed;
        private IntPtr _sprite;
        private IntPtr _nativeMaterial;
        private bool _flipX, _flipY;
        private readonly BoardLabelPresentation _label;
        private readonly BoardLabelPresentation _rentLabel;
        private readonly List<ArtAnchor> _artAnchors=new();
        private readonly HashSet<int> _discoveredArt=new();
        private readonly bool _nativeChance;
        private readonly int _cellInstanceId,_cellTransformId,_rendererId;
        private readonly Vector3 _appliedRootPosition;
        private bool _applied;
        private float _nextArtRefresh;
        private bool _artWarning;

        internal TileState(CellView cell,Vector3[] face,Vector2 boardCenter,Vector2 tangent,
            CanonicalBoardCell target,bool planeXY,bool nativeChance)
        {
            _cell=cell; Renderer=cell._cellRenderer?._renderer;
            if(Renderer==null || Renderer.sprite==null)throw new InvalidOperationException("Missing native ground texture.");
            _rootPosition=cell.transform.position; _press=cell._pressCellStartPosition;
            _nativeChance=nativeChance;_cellInstanceId=cell.GetInstanceID();
            _cellTransformId=cell.transform.GetInstanceID();_rendererId=Renderer.GetInstanceID();
            _sourceMatrix=Renderer.transform.localToWorldMatrix; _forceOff=Renderer.forceRenderingOff;
            _planeXY=planeXY; _corners=cell._cornersArray; _slots=cell._slots;
            foreach(Vector3 point in face) _planeDepth+=(planeXY?point.z:point.y)/face.Length;
            _points=cell._iconVisualPositions; _orders=cell.IconVisualOrders;
            _nativeFreePlaces=cell.FreePlaces; _collider=cell._collider2D;
            if (_slots == null || _points == null || _orders == null ||
                _slots.Length < 5 || _points.Length < _slots.Length || _orders.Length < _slots.Length)
                throw new InvalidOperationException("Incomplete native tile positions.");
            _pointValues = new Vector3[_points.Length];
            for (int index = 0; index < _points.Length; index++) _pointValues[index] = _points[index];
            foreach(Transform slot in _slots)
            {
                if (slot == null) throw new InvalidOperationException("Missing native tile position transform.");
                _slotPositions.Add(slot.position);
            }
            if(_nativeFreePlaces!=null) foreach(int place in _nativeFreePlaces) _freePlaces.Add(place);
            if(_collider!=null)
                for(int path=0;path<_collider.pathCount;path++)_colliderPaths.Add(PentagonalBoardPatch.ReadColliderPath(_collider,path));
            _sourceQuad=OrderFace(face,boardCenter,tangent,planeXY);
            _targetQuad=new[]{World(target.InnerStart),World(target.OuterStart),World(target.OuterEnd),World(target.InnerEnd)};
            _renderIndices=target.Quad[1].Equals(target.OuterStart)?new[]{0,1,2,3}:new[]{0,3,2,1};
            _renderQuad=new Vector3[4];
            for(int index=0;index<4;index++)
            {
                _renderQuad[index]=_targetQuad[_renderIndices[index]];
            }
            ProjectSprite(Renderer.sprite);
            FaceCoordinates(face[0]);
            _center=World(target.Center);
            Vector3 sourceCenter=Vector3.zero;foreach(Vector3 point in face)sourceCenter+=point*.25f;
            _appliedRootPosition=_rootPosition+_center-sourceCenter;
            // Native service prefabs have no rent view. Their localized
            // captions can use the whole face instead of sharing the narrow
            // ownable-cell name strip with a price they never display.
            _label=new BoardLabelPresentation(cell._labelView,_targetQuad,planeXY,_planeDepth,
                BoardLabelLayoutRules.NameInnerFraction,cell._rentView==null?
                    BoardLabelLayoutRules.ServiceNameOuterFraction:BoardLabelLayoutRules.NameOuterFraction);
            _rentLabel=new BoardLabelPresentation(cell._rentView,_targetQuad,planeXY,_planeDepth,
                BoardLabelLayoutRules.RentInnerFraction,BoardLabelLayoutRules.RentOuterFraction,BoardLabelRole.Rent);
            DiscoverArtwork();
        }

        private bool OwnsCell=>_cell!=null && Renderer!=null &&
            _cell.GetInstanceID()==_cellInstanceId && _cell.transform.GetInstanceID()==_cellTransformId &&
            Renderer.GetInstanceID()==_rendererId && _cell._cellRenderer?._renderer?.Pointer==Renderer.Pointer;

        private void DiscoverArtwork()
        {
            _discoveredArt.Clear();
            // Move only named native owners. GAF creates animated children
            // later: fitting its owning clip keeps all those local offsets,
            // rotations, scales and frame updates native.
            CaptureArtAnchor(_cell._buildParent,.60f);
            CaptureArtAnchor(_cell._festivalView?._movieClipFlags?._clip?.transform,centerBase:true);
            CaptureArtAnchor(_cell._festivalView?._fireworks?._clip?.transform,centerBase:true);
            CaptureArtAnchor(_cell._tournamentView?.transform,
                planarImage:_nativeChance?_cell._tournamentView?._tournamentRenderer:null);
            CaptureArtAnchor(_cell._startCellView?.transform);
            CaptureArtAnchor(_cell._monopolyView?._element?.transform,centerBase:true);
            CaptureArtAnchor(_cell._trapView?.transform);
            CaptureArtAnchor(_cell._saleEffectHolder);
            CaptureArtAnchor(_cell._cardEffectHolder?.transform);
            CaptureArtAnchor(_cell._diceNumbersView?.transform);
            CaptureArtAnchor(_cell._blackoutsView?.transform);
            CaptureArtAnchor(_cell._starRatingStamp?.transform);
            // Service cells have no BuildingView/build parent. Their native
            // upright ship/stadium/island/start art is explicitly cataloged
            // by this orderable component, independently of the ground sprite.
            CellObjectsRenderer objects=_cell._cellRenderer?.GetComponent<CellObjectsRenderer>();
            if(objects?._imagesArray!=null)
                foreach(SpriteRenderer image in objects._imagesArray)
                    if(image!=null && image.Pointer!=Renderer.Pointer)
                        CaptureArtAnchor(image.transform,.60f,planarImage:_nativeChance?image:null);
            for(int index=_artAnchors.Count-1;index>=0;index--)
                if(!_artAnchors[index].IsOwned || !_discoveredArt.Contains(_artAnchors[index].InstanceId))
                { _artAnchors[index].Restore();_artAnchors.RemoveAt(index); }
            _artAnchors.Sort((first,second)=>first.Depth.CompareTo(second.Depth));
        }
        internal bool Matches(CellView cell)=>_cell.Pointer==cell.Pointer &&
            _cell._cellRenderer?._renderer?.Pointer==Renderer.Pointer;
        private Vector3 World(NVector2 point)=>_planeXY?new Vector3(point.X,point.Y,_planeDepth):new Vector3(point.X,_planeDepth,point.Y);
        private Vector2 Planar(Vector3 point)=>new(point.x,_planeXY?point.y:point.z);

        private void CaptureArtAnchor(Transform transform,float minimumOuterFraction=0f,
            bool centerBase=false,SpriteRenderer planarImage=null)
        {
            if(transform==null || transform.Pointer==_cell.transform.Pointer ||
                transform.Pointer==Renderer.transform.Pointer) return;
            int instanceId=transform.GetInstanceID();
            _discoveredArt.Add(instanceId);
            foreach(ArtAnchor anchor in _artAnchors)
            {
                if(anchor.IsOwned && anchor.InstanceId==instanceId)return;
                // A captured owner already moves every child. Capturing the
                // child as well would erase its native relative offset.
                if(anchor.IsOwned && IsDescendant(transform,anchor.Transform))return;
            }
            int depth=0;
            Transform ancestor=transform;
            while(ancestor!=null && ancestor.Pointer!=_cell.transform.Pointer)
            { depth++; ancestor=ancestor.parent; }
            // Never move a shared scene object or an unrelated prefab owner.
            if(ancestor==null)return;
            // Native loading may reveal a broader owner after a child was
            // catalogued. Release our child edits before capturing that owner.
            for(int index=_artAnchors.Count-1;index>=0;index--)
                if(IsDescendant(_artAnchors[index].Transform,transform))
                { _artAnchors[index].Restore();_artAnchors.RemoveAt(index); }
            var source=new NVector2[4];var target=new NVector2[4];
            for(int index=0;index<4;index++)
            {
                Vector2 a=Planar(_sourceQuad[index]),b=Planar(_targetQuad[index]);
                source[index]=new NVector2(a.x,a.y);target[index]=new NVector2(b.x,b.y);
            }
            Vector3 native=transform.position;
            if(_applied)native-=_cell.transform.position-_rootPosition;
            Vector2 planar=Planar(native);
            NVector2 mapped=centerBase?BoardArtworkLayoutRules.BaseCenter(target):
                CanonicalTileProjection.ProjectAnchor(source,target,
                    new NVector2(planar.x,planar.y),.1f,minimumOuterFraction);
            Vector3 position=_planeXY?new Vector3(mapped.X,mapped.Y,native.z):
                new Vector3(mapped.X,native.y,mapped.Y);
            _artAnchors.Add(new ArtAnchor(transform,_cell.transform,position-_appliedRootPosition,depth,planarImage));
        }

        private static bool IsDescendant(Transform candidate,Transform owner)
        {
            if(candidate==null || owner==null)return false;
            Transform parent=candidate.parent;
            while(parent!=null)
            { if(parent.Pointer==owner.Pointer)return true;parent=parent.parent; }
            return false;
        }

        private void SyncArtwork(Camera camera)
        {
            if(!_applied || !OwnsCell || Time.unscaledTime<_nextArtRefresh)return;
            _nextArtRefresh=Time.unscaledTime+.1f;
            try
            {
                DiscoverArtwork();
                foreach(ArtAnchor anchor in _artAnchors)
                {
                    anchor.Apply();
                    anchor.FitPlanar(camera,_targetQuad);
                }
            }
            catch(Exception ex)
            {
                if(!_artWarning)
                { _artWarning=true;Plugin.ModLog.LogWarning($"Native artwork fitting skipped safely on cell {_cell._cellId}: {ex.Message}"); }
            }
        }

        private static Vector3[] OrderFace(Vector3[] face,Vector2 center,Vector2 tangent,bool xy)
        {
            Vector3 average=Vector3.zero;foreach(Vector3 point in face)average+=point*0.25f;
            var ordered=new List<Vector3>(face);
            ordered.Sort((a,b)=>Mathf.Atan2(xy?a.y-average.y:a.z-average.z,a.x-average.x).CompareTo(
                Mathf.Atan2(xy?b.y-average.y:b.z-average.z,b.x-average.x)));
            int outer=0;float greatest=float.NegativeInfinity;
            for(int index=0;index<4;index++)
            {
                Vector3 midpoint=(ordered[index]+ordered[(index+1)%4])*0.5f;
                float distance=(new Vector2(midpoint.x,xy?midpoint.y:midpoint.z)-center).sqrMagnitude;
                if(distance>greatest){greatest=distance;outer=index;}
            }
            Vector3 aOuter=ordered[outer], bOuter=ordered[(outer+1)%4];
            Vector2 edge=new(bOuter.x-aOuter.x,xy?bOuter.y-aOuter.y:bOuter.z-aOuter.z);
            return Vector2.Dot(edge,tangent)>=0f
                ?new[]{ordered[(outer+3)%4],aOuter,bOuter,ordered[(outer+2)%4]}
                :new[]{ordered[(outer+2)%4],bOuter,aOuter,ordered[(outer+3)%4]};
        }

        internal void Apply()
        {
            Vector3 sourceCenter=Vector3.zero;foreach(Vector3 point in _sourceQuad)sourceCenter+=point*0.25f;
            _extra=PentagonalBoardPatch.EnsureFifthPawnPlace(_cell);
            _cell.transform.position+=_center-sourceCenter;
            _applied=true;
            foreach(ArtAnchor anchor in _artAnchors)anchor.Apply();
            var corners=new Il2CppStructArray<Vector3>(4);
            for(int index=0;index<4;index++)corners[index]=_cell.transform.InverseTransformPoint(_targetQuad[index]);
            _cell._cornersArray=corners;
            if(_collider!=null)
            {
                var collider=_collider;
                var points=new Il2CppStructArray<Vector2>(4);
                for(int index=0;index<4;index++)
                {
                    Vector3 local=collider.transform.InverseTransformPoint(_renderQuad[index]);
                    points[index]=new Vector2(local.x,local.y)-collider.offset;
                }
                collider.pathCount=1;collider.SetPath(0,points);
            }
            for(int index=0;index<_cell._slots.Length;index++)
            {
                // Compact native peripheral positions onto the visible top.
                Vector3 source=index<_slotPositions.Count?_slotPositions[index]:_extra.position-(_center-sourceCenter);
                Vector2 uv=FaceCoordinates(source);
                Vector3 position=Vector3.Lerp(Vector3.Lerp(_targetQuad[0],_targetQuad[3],Mathf.Clamp(uv.x,0.1f,0.9f)),
                    Vector3.Lerp(_targetQuad[1],_targetQuad[2],Mathf.Clamp(uv.x,0.1f,0.9f)),Mathf.Clamp(uv.y,0.1f,0.9f));
                _cell._slots[index].position=position;_cell._iconVisualPositions[index]=position;
            }
            _cell._pressCellStartPosition=_cell.transform.position;
            _bed=Surface.Solid(_cell.transform,Renderer,_renderQuad,new Color(0.91f,0.90f,0.85f,1f),Renderer.sortingOrder-1);
            Sync();
        }

        private Vector2 FaceCoordinates(Vector3 point)
        {
            Vector2 a=Planar(_sourceQuad[0]), x=Planar(_sourceQuad[3])-a, y=Planar(_sourceQuad[1])-a, p=Planar(point)-a;
            float determinant=x.x*y.y-x.y*y.x;
            if(Mathf.Abs(determinant)<0.00001f)throw new InvalidOperationException("Degenerate logical tile face.");
            return new Vector2((p.x*y.y-p.y*y.x)/determinant,(x.x*p.y-x.y*p.x)/determinant);
        }

        internal void FitLabel(Camera camera)
        {
            _label?.Fit(camera);
            _rentLabel?.Fit(camera);
        }

        internal void Sync(Camera camera=null)
        {
            if(Renderer==null)return;
            SyncArtwork(camera ?? Camera.main);
            Sprite sprite=Renderer.sprite;
            if(sprite==null)return;
            IntPtr material = Renderer.sharedMaterial?.Pointer ?? IntPtr.Zero;
            if(_sprite!=sprite.Pointer || _nativeMaterial!=material || _flipX!=Renderer.flipX || _flipY!=Renderer.flipY)
            {
                CanonicalTileMesh mesh=ProjectSprite(sprite);
                Surface replacement=mesh.IsEmpty?null:Surface.Textured(_cell.transform,Renderer,mesh,World);
                _ground?.Dispose(); _ground=replacement;
                if(mesh.IsEmpty)Plugin.ModLog.LogWarning($"Cell {_cell.name}, sprite {sprite.name}: no top artwork coverage; showing the tile bed only.");
                _sprite=sprite.Pointer;_nativeMaterial=material;_flipX=Renderer.flipX;_flipY=Renderer.flipY;
            }
            _ground?.Sync(Renderer); _bed.SyncOrder(Renderer.sortingOrder-1);
            Renderer.forceRenderingOff=true;
            FitLabel(camera ?? Camera.main);
        }

        private CanonicalTileMesh ProjectSprite(Sprite sprite)
        {
            var vertices=sprite.vertices;var uv=sprite.uv;var triangles=sprite.triangles;
            var source=new NVector2[4];var target=new NVector2[4];
            for(int index=0;index<4;index++)
            {
                Vector2 a=Planar(_sourceQuad[index]),b=Planar(_targetQuad[index]);
                source[index]=new NVector2(a.x,a.y);target[index]=new NVector2(b.x,b.y);
            }
            // The basic CellService has no catalogue icon: its Chance wheel
            // is baked into the native tile texture. Inset only that painted
            // surface; the full canonical bed, collider, labels and route
            // remain unchanged. Separate tournament/crown art is fitted too.
            if(_nativeChance)target=BoardArtworkLayoutRules.InsetChanceGround(target);
            var positions=new NVector2[vertices.Length];var texture=new NVector2[uv.Length];
            for(int index=0;index<vertices.Length;index++)
            {
                Vector2 point=SpritePoint(vertices[index]);positions[index]=new NVector2(point.x,point.y);
            }
            for(int index=0;index<uv.Length;index++)texture[index]=new NVector2(uv[index].x,uv[index].y);
            var indices=new int[triangles.Length];for(int index=0;index<triangles.Length;index++)indices[index]=triangles[index];
            try{return CanonicalTileProjection.Project(source,target,positions,texture,indices);}
            catch(Exception ex){throw new InvalidOperationException($"Native tile {_cell.name}, sprite {sprite.name} cannot be safely projected.",ex);}
        }
        private Vector2 SpritePoint(Vector2 point)
        {
            if(Renderer.flipX)point.x=-point.x;if(Renderer.flipY)point.y=-point.y;
            return Planar(_sourceMatrix.MultiplyPoint3x4(new Vector3(point.x,point.y,0f)));
        }

        internal void Restore()
        {
            _ground?.Dispose();_bed?.Dispose();
            try
            {
                if(_cell==null)return;
                _label?.Restore();
                _rentLabel?.Restore();
                foreach(ArtAnchor anchor in _artAnchors)anchor.Restore();
                _cell.transform.position=_rootPosition;_cell._pressCellStartPosition=_press;
                _cell._cornersArray=_corners;_cell._slots=_slots;_cell._iconVisualPositions=_points;_cell.IconVisualOrders=_orders;
                for(int index=0;index<_pointValues.Length;index++)_points[index]=_pointValues[index];
                for(int index=0;index<_slotPositions.Count;index++)_slots[index].position=_slotPositions[index];
                if(_nativeFreePlaces!=null){_nativeFreePlaces.Clear();foreach(int place in _freePlaces)_nativeFreePlaces.Add(place);}
                if(_collider!=null){_collider.pathCount=_colliderPaths.Count;for(int path=0;path<_colliderPaths.Count;path++)_collider.SetPath(path,_colliderPaths[path]);}
            }
            finally
            {
                try { if(Renderer!=null)Renderer.forceRenderingOff=_forceOff; } catch { }
                try { if(_extra!=null)UnityEngine.Object.Destroy(_extra.gameObject); } catch { }
            }
        }

        private sealed class ArtAnchor
        {
            internal readonly Transform Transform;
            internal readonly int Depth;
            internal readonly int InstanceId;
            private readonly Transform _parent;
            private readonly Transform _cellRoot;
            private readonly int _parentId,_cellRootId,_rendererId;
            private readonly SpriteRenderer _planarImage;
            private readonly Vector3 _nativeLocalPosition,_nativeLocalScale,_targetOffset;
            private Vector3 _assignedPosition,_assignedScale;
            private bool _ownsPosition,_ownsScale;
            internal ArtAnchor(Transform transform,Transform cellRoot,Vector3 targetOffset,int depth,SpriteRenderer planarImage)
            {
                Transform=transform;Depth=depth;_parent=transform.parent;
                InstanceId=transform.GetInstanceID();_parentId=_parent?.GetInstanceID()??0;
                _cellRoot=cellRoot;_cellRootId=cellRoot.GetInstanceID();
                _nativeLocalPosition=transform.localPosition;_nativeLocalScale=transform.localScale;
                _targetOffset=targetOffset;_planarImage=planarImage;_rendererId=planarImage?.GetInstanceID()??0;
            }
            internal bool IsOwned=>Transform!=null && Transform.GetInstanceID()==InstanceId &&
                Transform.parent?.Pointer==_parent?.Pointer && (_parent?.GetInstanceID()??0)==_parentId &&
                _cellRoot!=null && _cellRoot.GetInstanceID()==_cellRootId && IsDescendant(Transform,_cellRoot) &&
                (_planarImage==null || _planarImage.GetInstanceID()==_rendererId);
            internal void Apply()
            {
                if(!IsOwned)return;
                Transform.position=_cellRoot.position+_targetOffset;
                _assignedPosition=Transform.localPosition;_ownsPosition=true;
            }

            internal void FitPlanar(Camera camera,Vector3[] targetQuad)
            {
                // Upright flags, buildings and animated GAF clips are never
                // flattened, billboarded or scaled by the footprint policy.
                if(_planarImage==null || !IsOwned || camera==null || !camera.orthographic ||
                    !_planarImage.enabled || !_planarImage.gameObject.activeInHierarchy || _planarImage.sprite==null)return;
                Bounds bounds=_planarImage.localBounds;
                if(!Finite(bounds.center) || !Finite(bounds.extents) || bounds.size.x<=.00001f ||
                    bounds.size.y<=.00001f || bounds.extents.z<0f)return;
                // Evaluate the original scale without temporarily enlarging
                // the live renderer. Include all eight bounds corners, not its
                // pivot or a single width, in the camera-plane containment.
                Matrix4x4 matrix=(_parent==null?Matrix4x4.identity:_parent.localToWorldMatrix)*
                    Matrix4x4.TRS(Transform.localPosition,Transform.localRotation,_nativeLocalScale);
                Vector3 visibleCenter=matrix.MultiplyPoint3x4(bounds.center);
                Vector3 cameraCenter=camera.transform.InverseTransformPoint(visibleCenter);
                var offsets=new NVector2[8];var quad=new NVector2[4];
                for(int index=0;index<8;index++)
                {
                    Vector3 corner=bounds.center+new Vector3((index&1)==0?-bounds.extents.x:bounds.extents.x,
                        (index&2)==0?-bounds.extents.y:bounds.extents.y,(index&4)==0?-bounds.extents.z:bounds.extents.z);
                    Vector3 point=camera.transform.InverseTransformPoint(matrix.MultiplyPoint3x4(corner));
                    offsets[index]=new NVector2(point.x-cameraCenter.x,point.y-cameraCenter.y);
                }
                for(int index=0;index<4;index++)
                {
                    Vector3 point=camera.transform.InverseTransformPoint(targetQuad[index]);
                    quad[index]=new NVector2(point.x,point.y);
                }
                NVector2 center=BoardArtworkLayoutRules.BaseCenter(quad);
                float scale=BoardArtworkLayoutRules.FitPlanarScale(quad,center,offsets);
                if(scale<=0f)return;
                Transform.localScale=_nativeLocalScale*scale;
                // Preserve the native distance along the view axis. Only the
                // visible x/y center is shifted into this cell's own footprint.
                Vector3 actualCenter=Transform.TransformPoint(bounds.center);
                Vector3 actualCamera=camera.transform.InverseTransformPoint(actualCenter);
                Transform.position+=camera.transform.right*(center.X-actualCamera.x)+
                    camera.transform.up*(center.Y-actualCamera.y);
                _assignedPosition=Transform.localPosition;_assignedScale=Transform.localScale;
                _ownsPosition=true;_ownsScale=true;
            }

            internal void Restore()
            {
                if(!IsOwned)return;
                // A native animation or a new owner may have written these
                // values since our last fit. Restore only our remaining edits.
                if(_ownsPosition && Same(Transform.localPosition,_assignedPosition))Transform.localPosition=_nativeLocalPosition;
                if(_ownsScale && Same(Transform.localScale,_assignedScale))Transform.localScale=_nativeLocalScale;
                _ownsPosition=false;_ownsScale=false;
            }
            private static bool Same(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<1e-10f;
            private static bool Finite(Vector3 point)=>float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z);
        }
    }

    private sealed class Surface
    {
        private readonly GameObject _holder;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties=new();
        private Surface(Transform parent,SpriteRenderer native,IReadOnlyList<Vector3> points,Vector2[] uv,Color color,int order,Texture texture, bool nativeMaterial=false,int[] meshTriangles=null)
        {
            _holder=new GameObject("FiveRealmsCanonicalSurface");_holder.layer=native.gameObject.layer;
            _holder.transform.SetParent(parent,false);
            var vertices=new Vector3[points.Count];for(int index=0;index<points.Count;index++)vertices[index]=_holder.transform.InverseTransformPoint(points[index]);
            int[] triangles=meshTriangles;
            if(triangles==null)
            {
                triangles=new int[(points.Count-2)*3];for(int face=0;face<points.Count-2;face++){triangles[face*3]=0;triangles[face*3+1]=face+1;triangles[face*3+2]=face+2;}
            }
            _mesh=new Mesh{name="FiveRealmsCanonicalTile"};_mesh.vertices=new Il2CppStructArray<Vector3>(vertices);
            _mesh.uv=new Il2CppStructArray<Vector2>(uv??new Vector2[points.Count]);
            var colors=new Color[points.Count];for(int index=0;index<colors.Length;index++)colors[index]=color;
            _mesh.colors=new Il2CppStructArray<Color>(colors);_mesh.triangles=new Il2CppStructArray<int>(triangles);_mesh.RecalculateBounds();
            _holder.AddComponent<MeshFilter>().sharedMesh=_mesh;_renderer=_holder.AddComponent<MeshRenderer>();
            if(nativeMaterial && native.sharedMaterial!=null) _material=new Material(native.sharedMaterial);
            else
            {
                Shader shader=Shader.Find("Sprites/Default");
                if(shader==null) throw new InvalidOperationException("Missing native sprite shader.");
                _material=new Material(shader);
            }
            _material.mainTexture=texture;
            _renderer.sharedMaterial=_material;_renderer.sortingLayerID=native.sortingLayerID;_renderer.sortingOrder=order;
            _renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;_renderer.receiveShadows=false;
        }
        internal static Surface Solid(Transform parent,SpriteRenderer native,IReadOnlyList<Vector3> points,Color color,int order)=>new(parent,native,points,null,color,order,Texture2D.whiteTexture);
        internal static Surface Textured(Transform parent,SpriteRenderer native,CanonicalTileMesh mesh,Func<NVector2,Vector3> world)
        {
            var points=new Vector3[mesh.Positions.Count];var uv=new Vector2[mesh.UV.Count];var triangles=new int[mesh.Triangles.Count];
            for(int index=0;index<points.Length;index++){points[index]=world(mesh.Positions[index]);uv[index]=new Vector2(mesh.UV[index].X,mesh.UV[index].Y);}
            for(int index=0;index<triangles.Length;index++)triangles[index]=mesh.Triangles[index];
            return new(parent,native,points,uv,Color.white,native.sortingOrder,native.sprite.texture,true,triangles);
        }
        internal void SyncOrder(int order){if(_renderer!=null)_renderer.sortingOrder=order;}
        internal void Sync(SpriteRenderer native)
        {
            if(_renderer==null)return;_properties.Clear();native.GetPropertyBlock(_properties);
            _properties.SetTexture("_MainTex",native.sprite.texture);_properties.SetColor("_RendererColor",native.color);_properties.SetVector("_Flip",Vector4.one);
            _renderer.SetPropertyBlock(_properties);_renderer.sortingOrder=native.sortingOrder;_renderer.enabled=native.enabled;
        }
        internal void Dispose()
        {
            try{if(_holder!=null){_holder.SetActive(false);UnityEngine.Object.Destroy(_holder);}if(_mesh!=null)UnityEngine.Object.Destroy(_mesh);if(_material!=null)UnityEngine.Object.Destroy(_material);}catch{}
        }
    }
}
