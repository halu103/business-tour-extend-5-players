using System;
using System.Collections.Generic;
using BusinessTour;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace BusinessTourFiveRealms;

// The board silhouette must come from one shared geometry, not the four
// differently sized native corner sprites. Native textures are decoration on
// these explicit tile surfaces; controllers and the 32-cell graph stay native.
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
                radiusY * 1.13f, cells.Count, Math.Sign(cross));
            prepared = new BoardState(map, geometry, reference.PlaneXY);
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2 tangent = reference.Source[(index + 1) % cells.Count] -
                    reference.Source[(index + cells.Count - 1) % cells.Count];
                var snapshot = new TileState(cells[index], faces[index], reference.Center, tangent,
                    geometry.Cells[index], reference.PlaneXY);
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
        foreach (TileState tile in _current.Tiles) tile.Sync();
    }

    internal static void Sync(CellRenderer renderer)
    {
        if (_current == null || renderer?._renderer == null) return;
        foreach (TileState tile in _current.Tiles)
            if (tile.Renderer.Pointer == renderer._renderer.Pointer) { tile.Sync(); return; }
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
        internal readonly List<TileState> Tiles = new();
        private readonly List<Surface> _surfaces = new();
        private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, bool ForceOff)> _hidden = new();
        private Camera _camera;
        private Vector3 _cameraPosition;
        private float _cameraSize;

        internal BoardState(MapView map, CanonicalBoardGeometry geometry, bool planeXY)
        { Map = map; Geometry = geometry; PlaneXY = planeXY; }

        internal bool Matches(MapView map, List<CellView> cells)
        {
            if (Map.Pointer != map.Pointer || cells.Count != Tiles.Count) return false;
            for (int index = 0; index < cells.Count; index++)
                if (!Tiles[index].Matches(cells[index])) return false;
            return true;
        }

        internal Vector3 Point(NVector2 point) => PlaneXY ? new Vector3(point.X, point.Y, 0f) : new Vector3(point.X, 0f, point.Y);

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
            float size=Mathf.Max((right-left+1.4f)/(2f*camera.aspect*0.76f), (top-bottom+1.7f)/(2f*0.78f));
            camera.transform.position += camera.transform.right * ((left+right)*0.5f-0.17f*size*camera.aspect) +
                camera.transform.up * ((bottom+top)*0.5f+0.04f*size);
            camera.orthographicSize=size;
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

        internal TileState(CellView cell,Vector3[] face,Vector2 boardCenter,Vector2 tangent,
            CanonicalBoardCell target,bool planeXY)
        {
            _cell=cell; Renderer=cell._cellRenderer?._renderer;
            if(Renderer==null || Renderer.sprite==null)throw new InvalidOperationException("Missing native ground texture.");
            _rootPosition=cell.transform.position; _press=cell._pressCellStartPosition;
            _sourceMatrix=Renderer.transform.localToWorldMatrix; _forceOff=Renderer.forceRenderingOff;
            _planeXY=planeXY; _corners=cell._cornersArray; _slots=cell._slots;
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
                SampleUv(Renderer.sprite,_sourceQuad[index]);
            }
            FaceCoordinates(face[0]);
            _center=World(target.Center);
        }
        internal bool Matches(CellView cell)=>_cell.Pointer==cell.Pointer &&
            _cell._cellRenderer?._renderer?.Pointer==Renderer.Pointer;
        private Vector3 World(NVector2 point)=>_planeXY?new Vector3(point.X,point.Y,0f):new Vector3(point.X,0f,point.Y);
        private Vector2 Planar(Vector3 point)=>new(point.x,_planeXY?point.y:point.z);

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

        internal void Sync()
        {
            if(Renderer==null)return;
            Sprite sprite=Renderer.sprite;
            if(sprite==null)return;
            IntPtr material = Renderer.sharedMaterial?.Pointer ?? IntPtr.Zero;
            if(_ground==null || _sprite!=sprite.Pointer || _nativeMaterial!=material || _flipX!=Renderer.flipX || _flipY!=Renderer.flipY)
            {
                var uv=new Vector2[4];
                for(int index=0;index<4;index++)uv[index]=SampleUv(sprite,_sourceQuad[_renderIndices[index]]);
                Surface replacement=Surface.Textured(_cell.transform,Renderer,_renderQuad,uv);
                _ground?.Dispose(); _ground=replacement;
                _sprite=sprite.Pointer;_nativeMaterial=material;_flipX=Renderer.flipX;_flipY=Renderer.flipY;
            }
            _ground.Sync(Renderer); _bed.SyncOrder(Renderer.sortingOrder-1);
            Renderer.forceRenderingOff=true;
        }

        private Vector2 SampleUv(Sprite sprite,Vector3 point)
        {
            var vertices=sprite.vertices;var uv=sprite.uv;var triangles=sprite.triangles;
            Vector2 p=Planar(point);float best=float.NegativeInfinity;Vector2 result=Vector2.zero;
            for(int index=0;index+2<triangles.Length;index+=3)
            {
                int ia=triangles[index],ib=triangles[index+1],ic=triangles[index+2];
                Vector2 a=SpritePoint(vertices[ia]),b=SpritePoint(vertices[ib]),c=SpritePoint(vertices[ic]);
                Vector2 x=b-a,y=c-a,q=p-a;float d=x.x*y.y-x.y*y.x;
                if(Mathf.Abs(d)<0.000001f)continue;
                float v=(q.x*y.y-q.y*y.x)/d,w=(x.x*q.y-x.y*q.x)/d,u=1f-v-w;
                float score=Mathf.Min(u,Mathf.Min(v,w));
                if(score>best){best=score;result=uv[ia]*u+uv[ib]*v+uv[ic]*w;}
            }
            if(float.IsNegativeInfinity(best))throw new InvalidOperationException("No usable native sprite UV triangles.");
            if(best < -0.025f)
                throw new InvalidOperationException($"Cell {_cell.name}, sprite {sprite.name}: logical top corner outside native texture coverage (score={best:F4}, point={point}).");
            return result;
        }
        private Vector2 SpritePoint(Vector2 point)
        {
            if(Renderer.flipX)point.x=-point.x;if(Renderer.flipY)point.y=-point.y;
            return Planar(_sourceMatrix.MultiplyPoint3x4(new Vector3(point.x,point.y,0f)));
        }

        internal void Restore()
        {
            _ground?.Dispose();_bed?.Dispose();
            if(_cell==null)return;
            _cell.transform.position=_rootPosition;_cell._pressCellStartPosition=_press;
            _cell._cornersArray=_corners;_cell._slots=_slots;_cell._iconVisualPositions=_points;_cell.IconVisualOrders=_orders;
            for(int index=0;index<_pointValues.Length;index++)_points[index]=_pointValues[index];
            for(int index=0;index<_slotPositions.Count;index++)_slots[index].position=_slotPositions[index];
            if(_nativeFreePlaces!=null){_nativeFreePlaces.Clear();foreach(int place in _freePlaces)_nativeFreePlaces.Add(place);}
            if(_collider!=null){_collider.pathCount=_colliderPaths.Count;for(int path=0;path<_colliderPaths.Count;path++)_collider.SetPath(path,_colliderPaths[path]);}
            if(Renderer!=null)Renderer.forceRenderingOff=_forceOff;
            if(_extra!=null)UnityEngine.Object.Destroy(_extra.gameObject);
        }
    }

    private sealed class Surface
    {
        private readonly GameObject _holder;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties=new();
        private Surface(Transform parent,SpriteRenderer native,IReadOnlyList<Vector3> points,Vector2[] uv,Color color,int order,Texture texture, bool nativeMaterial=false)
        {
            _holder=new GameObject("FiveRealmsCanonicalSurface");_holder.layer=native.gameObject.layer;
            _holder.transform.SetParent(parent,false);
            var vertices=new Vector3[points.Count];for(int index=0;index<points.Count;index++)vertices[index]=_holder.transform.InverseTransformPoint(points[index]);
            var triangles=new int[(points.Count-2)*3];for(int face=0;face<points.Count-2;face++){triangles[face*3]=0;triangles[face*3+1]=face+1;triangles[face*3+2]=face+2;}
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
        internal static Surface Textured(Transform parent,SpriteRenderer native,Vector3[] points,Vector2[] uv)=>new(parent,native,points,uv,Color.white,native.sortingOrder,native.sprite.texture,true);
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
