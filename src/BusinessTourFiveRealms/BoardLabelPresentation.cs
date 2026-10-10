using System;
using System.Collections.Generic;
using BusinessTour;
using UnityEngine;
using NVector2=System.Numerics.Vector2;

namespace BusinessTourFiveRealms;

// Change only the native localized text transforms. Buildings, rent markers,
// IDs, localized content, colors and controller bindings remain native.
internal sealed class BoardLabelPresentation
{
    private readonly Vector3[] _quad;
    private readonly List<GlyphState> _glyphs=new();
    private bool _reported;

    internal BoardLabelPresentation(TextView view, Vector3[] quad, bool planeXY, float depth,
        float innerBand=0f,float outerBand=.52f)
    {
        _quad=new[]{Vector3.Lerp(quad[0],quad[1],innerBand),Vector3.Lerp(quad[0],quad[1],outerBand),
            Vector3.Lerp(quad[3],quad[2],outerBand),Vector3.Lerp(quad[3],quad[2],innerBand)};
        if (view == null) return;
        if (view._textMesh != null) Capture(view._textMesh.transform,planeXY,depth);
        if (view._textMeshPro != null) Capture(view._textMeshPro.transform,planeXY,depth);
    }

    private void Capture(Transform transform,bool xy,float depth)
    {
        foreach (GlyphState glyph in _glyphs) if (glyph.Transform.Pointer==transform.Pointer) return;
        MeshRenderer renderer=transform.GetComponent<MeshRenderer>();
        if (renderer != null) _glyphs.Add(new GlyphState(transform,renderer,xy,depth));
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
            foreach(GlyphState glyph in _glyphs) glyph.Fit(camera,quad,new NVector2(center.x,center.y),anchor,shortest*.04f);
        }
        catch(Exception ex)
        {
            if (!_reported) { _reported=true; Plugin.ModLog.LogWarning($"Tile label fitting skipped safely: {ex.Message}"); }
        }
    }

    internal void Restore()
    {
        foreach(GlyphState glyph in _glyphs)
            try { glyph.Restore(); } catch(Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
    }

    private sealed class GlyphState
    {
        internal readonly Transform Transform;
        private readonly MeshRenderer _renderer;
        private readonly Transform _parent;
        private readonly Vector3 _position, _scale, _depthOffset;
        private readonly Quaternion _rotation;

        internal GlyphState(Transform transform,MeshRenderer renderer,bool xy,float depth)
        {
            Transform=transform; _renderer=renderer; _parent=transform.parent;
            _position=transform.localPosition; _scale=transform.localScale; _rotation=transform.localRotation;
            _depthOffset=xy?new Vector3(0f,0f,transform.position.z-depth):new Vector3(0f,transform.position.y-depth,0f);
        }

        internal void Fit(Camera camera,NVector2[] quad,NVector2 center,Vector3 anchor,float padding)
        {
            if (Transform==null || _renderer==null || !Transform.gameObject.activeInHierarchy ||
                Transform.parent?.Pointer!=_parent?.Pointer) return;
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
                float scale=BoardLabelLayoutRules.FitScale(quad,center,right-left,top-bottom,padding);
                if (scale<=0f) throw new InvalidOperationException("No space for a label in its own tile.");
                Transform.localScale=_scale*scale;
                Vector3 actualCenter=_renderer.transform.TransformPoint(bounds.center);
                Transform.position+=anchor+_depthOffset-actualCenter;
            }
            catch
            {
                Transform.localPosition=priorPosition;Transform.localScale=priorScale;Transform.localRotation=priorRotation;
                throw;
            }
        }

        internal void Restore()
        {
            if(Transform==null || Transform.parent?.Pointer!=_parent?.Pointer) return;
            Transform.localPosition=_position;Transform.localScale=_scale;Transform.localRotation=_rotation;
        }
    }
}
