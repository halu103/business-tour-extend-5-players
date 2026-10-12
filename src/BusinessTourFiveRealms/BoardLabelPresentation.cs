using System;
using System.Collections.Generic;
using BusinessTour;
using TMPro;
using UnityEngine;
using NVector2=System.Numerics.Vector2;
using NVector3=System.Numerics.Vector3;

namespace BusinessTourFiveRealms;

internal enum BoardLabelRole { Name, Rent }

// Fit only the existing native text. Localized content, rent amounts,
// buildings and controller bindings remain native.
internal sealed class BoardLabelPresentation
{
    private static readonly List<GlyphState> PendingMaterialCleanup=new();
    private readonly Vector3[] _quad;
    private readonly List<GlyphState> _glyphs=new();
    private readonly BoardLabelRole _role;
    private bool _reported;

    internal BoardLabelPresentation(TextView view, Vector3[] quad, bool planeXY, float depth,
        float innerBand=BoardLabelLayoutRules.NameInnerFraction,
        float outerBand=BoardLabelLayoutRules.NameOuterFraction,BoardLabelRole role=BoardLabelRole.Name)
    {
        _role=role;
        _quad=new[]{Vector3.Lerp(quad[0],quad[1],innerBand),Vector3.Lerp(quad[0],quad[1],outerBand),
            Vector3.Lerp(quad[3],quad[2],outerBand),Vector3.Lerp(quad[3],quad[2],innerBand)};
        if (view == null) return;
        if (view._textMesh != null) Capture(view._textMesh.transform,planeXY,depth,view._textMesh,null);
        if (view._textMeshPro != null) Capture(view._textMeshPro.transform,planeXY,depth,null,view._textMeshPro);
    }

    private void Capture(Transform transform,bool xy,float depth,TextMesh textMesh,TMP_Text tmp)
    {
        foreach (GlyphState glyph in _glyphs) if (glyph.Transform.Pointer==transform.Pointer) return;
        MeshRenderer renderer=transform.GetComponent<MeshRenderer>();
        if (renderer != null) _glyphs.Add(new GlyphState(transform,renderer,xy,depth,_role,textMesh,tmp));
    }

    internal void Fit(Camera camera)
    {
        if (camera == null || !camera.orthographic) return;
        try
        {
            var quad=new NVector2[4];
            for(int index=0;index<4;index++)
            {
                Vector3 point=camera.transform.InverseTransformPoint(_quad[index]);
                quad[index]=new NVector2(point.x,point.y);
            }
            // The inner band leaves room for native houses/pawns towards the
            // outer side. Never use a neighboring cell for a long name.
            Vector3 anchor=((_quad[0]+_quad[3])+(_quad[1]+_quad[2]))*.25f;
            Vector3 center=camera.transform.InverseTransformPoint(anchor);
            float shortest=float.PositiveInfinity;
            for(int index=0;index<4;index++) shortest=Math.Min(shortest,NVector2.Distance(quad[index],quad[(index+1)%4]));
            foreach(GlyphState glyph in _glyphs) glyph.Fit(camera,quad,new NVector2(center.x,center.y),anchor,
                shortest*BoardLabelLayoutRules.EdgePaddingFraction);
        }
        catch(Exception ex)
        {
            if (!_reported) { _reported=true; Plugin.ModLog.LogWarning($"Tile label fitting skipped safely: {ex.Message}"); }
        }
    }

    internal void Restore()
    {
        foreach(GlyphState glyph in _glyphs)
        {
            try { glyph.Restore(); } catch(Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
            if(glyph.HasPendingMaterial && !PendingMaterialCleanup.Contains(glyph))PendingMaterialCleanup.Add(glyph);
        }
    }

    internal static void RetryPendingMaterialCleanup()
    {
        for(int index=PendingMaterialCleanup.Count-1;index>=0;index--)
        {
            GlyphState glyph=PendingMaterialCleanup[index];
            glyph.ReleaseRentMaterial();
            if(!glyph.HasPendingMaterial)PendingMaterialCleanup.RemoveAt(index);
        }
    }

    private sealed class GlyphState
    {
        internal readonly Transform Transform;
        private readonly MeshRenderer _renderer;
        private readonly Transform _parent;
        private readonly Vector3 _position, _scale, _depthOffset;
        private readonly Quaternion _rotation;
        private readonly int _transformId,_rendererId,_parentId,_tmpId;
        private readonly BoardLabelRole _role;
        private readonly TextMesh _textMesh;
        private readonly TMP_Text _tmp;
        private int _nativeLayer,_nativeOrder,_assignedLayer,_assignedOrder;
        private bool _ownsSorting;
        private bool _reported;
        private Material _nativeMaterial,_rentMaterial;
        private int _nativeMaterialId,_rentMaterialId;
        private bool _materialWarning;
        internal bool HasPendingMaterial=>_rentMaterial!=null;

        internal GlyphState(Transform transform,MeshRenderer renderer,bool xy,float depth,BoardLabelRole role,TextMesh textMesh,TMP_Text tmp)
        {
            Transform=transform; _renderer=renderer; _parent=transform.parent;
            _position=transform.localPosition; _scale=transform.localScale; _rotation=transform.localRotation;
            _depthOffset=xy?new Vector3(0f,0f,transform.position.z-depth):new Vector3(0f,transform.position.y-depth,0f);
            _transformId=transform.GetInstanceID(); _rendererId=renderer.GetInstanceID();
            _parentId=_parent?.GetInstanceID()??0;_tmpId=tmp?.GetInstanceID()??0;
            _role=role; _nativeLayer=renderer.sortingLayerID; _nativeOrder=renderer.sortingOrder;
            _textMesh=textMesh; _tmp=tmp;
        }

        private bool IsOwned => Transform!=null && _renderer!=null &&
            Transform.GetInstanceID()==_transformId && _renderer.GetInstanceID()==_rendererId &&
            Transform.parent?.Pointer==_parent?.Pointer && (_parent?.GetInstanceID()??0)==_parentId;

        private void FitBoardSorting()
        {
            int foreground=SortingLayer.NameToID(BoardLabelLayoutRules.BoardInformationLayerName);
            int characters=SortingLayer.NameToID(BoardLabelLayoutRules.CharacterLayerName);
            if (!SortingLayer.IsValid(foreground) || foreground==0 || !SortingLayer.IsValid(characters) ||
                SortingLayer.GetLayerValueFromID(foreground)>=SortingLayer.GetLayerValueFromID(characters))
                throw new InvalidOperationException("The verified board-information sorting layer is unavailable.");
            // Native CellView.SetOrder can reset the glyph between frames.
            // Remember each native property independently, never our own
            // previous override, and restore only values still owned by us.
            if (!_ownsSorting || _renderer.sortingLayerID!=_assignedLayer)
                _nativeLayer=_renderer.sortingLayerID;
            if (!_ownsSorting || _renderer.sortingOrder!=_assignedOrder)
                _nativeOrder=_renderer.sortingOrder;
            if (SortingLayer.GetLayerValueFromID(_nativeLayer)>SortingLayer.GetLayerValueFromID(foreground))
                return;
            _assignedLayer=foreground; _assignedOrder=_nativeOrder;
            _ownsSorting=true;
            _renderer.sortingLayerID=_assignedLayer; _renderer.sortingOrder=_assignedOrder;
        }

        private void FitRentMaterial()
        {
            if (_role!=BoardLabelRole.Rent || !IsOwned) return;
            try
            {
                if (_tmp==null || _tmp.GetInstanceID()!=_tmpId)
                    throw new InvalidOperationException("This rent view is not the verified native TMP family.");
                if (_rentMaterial!=null && _rentMaterial.GetInstanceID()==_rentMaterialId &&
                    SameMaterial(_renderer.sharedMaterial,_rentMaterial) && SameMaterial(_tmp.fontSharedMaterial,_rentMaterial))
                    return;
                // TMP can replace its per-instance material during native text
                // rebuilds. Release our previous clone before observing the new
                // native source, never clone a previously assigned override.
                ReleaseRentMaterial();
                if(_rentMaterial!=null)return; // Retry incomplete cleanup on the next sync.
                Material source=_renderer.sharedMaterial;
                if(source==null || !SameMaterial(source,_tmp.fontSharedMaterial))
                    throw new InvalidOperationException("Native font/renderer material binding changed.");
                string overlayName=BoardLabelLayoutRules.RentOverlayShaderFor(source.shader?.name);
                Shader overlay=overlayName==null?null:Shader.Find(overlayName);
                if(overlay==null || source.mainTexture==null)
                    throw new InvalidOperationException("A matching native TMP Overlay shader/atlas is unavailable.");
                _nativeMaterial=source;_nativeMaterialId=source.GetInstanceID();
                _rentMaterial=new Material(source){name="FiveRealmsRentBoardMaterial"};
                _rentMaterialId=_rentMaterial.GetInstanceID();
                _rentMaterial.shader=overlay;
                // Native GAF pawns are transparent queue3000. The Overlay
                // shader avoids opaque-house depth without moving the glyph
                // near the camera; board sorting still precedes Characters.
                _rentMaterial.renderQueue=BoardLabelLayoutRules.BoardTransparentRenderQueue;
                if(_rentMaterial.mainTexture?.Pointer!=source.mainTexture.Pointer ||
                    !_rentMaterial.HasProperty("_GradientScale") || !_rentMaterial.HasProperty("_TextureWidth") ||
                    !_rentMaterial.HasProperty("_TextureHeight"))
                    throw new InvalidOperationException("Native TMP atlas properties were not preserved.");
                // fontSharedMaterial belongs to this TMP instance, not the
                // shared font asset. Keep its future native rebuilds consistent.
                _tmp.fontSharedMaterial=_rentMaterial;
                _renderer.sharedMaterial=_rentMaterial;
            }
            catch(Exception ex)
            {
                ReleaseRentMaterial();
                if(!_materialWarning)
                {
                    _materialWarning=true;
                    Plugin.ModLog.LogWarning($"Rent depth workaround skipped safely for {Transform.name}: {ex.Message} Native material retained; house occlusion may remain.");
                }
            }
        }

        private static bool SameMaterial(Material a,Material b)=>a!=null && b!=null &&
            a.Pointer==b.Pointer && a.GetInstanceID()==b.GetInstanceID();

        internal void ReleaseRentMaterial()
        {
            Material owned=_rentMaterial;
            if(owned==null) { _rentMaterial=null;_nativeMaterial=null;return; }
            bool pending=false;
            try
            {
                if(owned.GetInstanceID()!=_rentMaterialId)return;
                Material native=_nativeMaterial!=null && _nativeMaterial.GetInstanceID()==_nativeMaterialId?
                    _nativeMaterial:null;
                // Restore only exact references still assigned by this glyph.
                // A native material/font swap is not overwritten during cleanup.
                try
                {
                    if(_tmp!=null && _tmp.GetInstanceID()==_tmpId && SameMaterial(_tmp.fontSharedMaterial,owned))
                        _tmp.fontSharedMaterial=native;
                }
                catch(Exception ex) { pending=true;Plugin.ModLog.LogDebug($"Rent TMP cleanup will retry: {ex.Message}"); }
                try
                {
                    if(_renderer!=null && _renderer.GetInstanceID()==_rendererId && SameMaterial(_renderer.sharedMaterial,owned))
                        _renderer.sharedMaterial=native;
                }
                catch(Exception ex) { pending=true;Plugin.ModLog.LogDebug($"Rent renderer cleanup will retry: {ex.Message}"); }
            }
            catch(Exception ex) { pending=true;Plugin.ModLog.LogDebug($"Rent material cleanup will retry: {ex.Message}"); }
            finally
            {
                // Do not destroy a material while a failed native setter may
                // still leave a live glyph referencing it.
                if(!pending)
                {
                    try
                    {
                        if(owned!=null && owned.GetInstanceID()==_rentMaterialId)UnityEngine.Object.Destroy(owned);
                        _rentMaterial=null;_nativeMaterial=null;
                    }
                    catch(Exception ex) { Plugin.ModLog.LogDebug($"Rent material disposal will retry: {ex.Message}"); }
                }
            }
        }

        internal void Fit(Camera camera,NVector2[] quad,NVector2 center,Vector3 anchor,float padding)
        {
            if (!IsOwned || !Transform.gameObject.activeInHierarchy) return;
            Bounds bounds=_renderer.localBounds;
            if (bounds.size.x<=.00001f || bounds.size.y<=.00001f) return;
            Vector3 priorPosition=Transform.localPosition, priorScale=Transform.localScale;
            Quaternion priorRotation=Transform.localRotation;
            try
            {
                Transform.rotation=camera.transform.rotation;
                Transform.localScale=_scale;
                float left=float.PositiveInfinity,right=float.NegativeInfinity;
                float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
                for(int index=0;index<8;index++)
                {
                    Vector3 corner=bounds.center+new Vector3((index&1)==0?-bounds.extents.x:bounds.extents.x,
                        (index&2)==0?-bounds.extents.y:bounds.extents.y,(index&4)==0?-bounds.extents.z:bounds.extents.z);
                    Vector3 point=camera.transform.InverseTransformPoint(_renderer.transform.TransformPoint(corner));
                    left=Math.Min(left,point.x);right=Math.Max(right,point.x);
                    bottom=Math.Min(bottom,point.y);top=Math.Max(top,point.y);
                }
                int lineCount=1;
                if (_tmp!=null) lineCount=Math.Max(1,_tmp.textInfo?.lineCount??1);
                else if (_textMesh!=null)
                    foreach(char character in _textMesh.text??string.Empty) if(character=='\n') lineCount++;
                float readability=BoardLabelLayoutRules.ReadabilityScale(top-bottom,camera.orthographicSize,
                    camera.pixelHeight,_role==BoardLabelRole.Rent,lineCount);
                float scale=BoardLabelLayoutRules.FitScale(quad,center,right-left,top-bottom,padding,readability);
                if (scale<=0f) throw new InvalidOperationException("No space for a label in its own tile.");
                Transform.localScale=_scale*scale;
                Vector3 actualCenter=_renderer.transform.TransformPoint(bounds.center);
                Vector3 forward=camera.transform.forward;
                NVector3 depth=BoardLabelLayoutRules.CameraDepthOffset(
                    new NVector3(_depthOffset.x,_depthOffset.y,_depthOffset.z),
                    new NVector3(forward.x,forward.y,forward.z));
                Transform.position+=anchor+new Vector3(depth.X,depth.Y,depth.Z)-actualCenter;
                FitBoardSorting();
                FitRentMaterial();
                if (!_reported)
                {
                    _reported=true;
                    Plugin.ModLog.LogInfo($"Five-player {_role} label {Transform.name}: native-depth strip, board layer={_renderer.sortingLayerName}, order={_renderer.sortingOrder}, lines={lineCount}, pixelsPerLine={(top-bottom)*scale*camera.pixelHeight/(2f*camera.orthographicSize*lineCount):F1}, nativeBounds={right-left:F3}x{top-bottom:F3}, targetScale={readability:F2}, fitScale={scale:F2}, rentOverlay={_rentMaterial!=null}; native content/atlas unchanged.");
                }
            }
            catch
            {
                Transform.localPosition=priorPosition;Transform.localScale=priorScale;Transform.localRotation=priorRotation;
                throw;
            }
        }

        internal void Restore()
        {
            ReleaseRentMaterial();
            if(!IsOwned) return;
            if (_ownsSorting)
            {
                if (_renderer.sortingLayerID==_assignedLayer) _renderer.sortingLayerID=_nativeLayer;
                if (_renderer.sortingOrder==_assignedOrder) _renderer.sortingOrder=_nativeOrder;
                _ownsSorting=false;
            }
            Transform.localPosition=_position;Transform.localScale=_scale;Transform.localRotation=_rotation;
        }
    }
}
