using System.Numerics;
using BusinessTourFiveRealms;

int fits=0,insets=0;
foreach(int count in new[]{32,40})
foreach(int winding in new[]{-1,1})
foreach(float radius in new[]{1f,4f,10f})
foreach(float angle in new[]{-.6f,0f,.7f})
{
    var geometry=CanonicalBoardGeometry.Create(new Vector2(2f,-3f),radius,radius*.57f,count,winding,.64f);
    foreach(var cell in geometry.Cells)
    {
        // Same native shape on very different source/clone cell widths. The
        // full visible box must fit the polygon, including its slanted edges.
        Vector2[] quad=new[]{Rotate(cell.InnerStart),Rotate(cell.OuterStart),Rotate(cell.OuterEnd),Rotate(cell.InnerEnd)};
        Vector2[] inset=BoardArtworkLayoutRules.InsetChanceGround(quad);
        Vector2 midpoint=(quad[0]+quad[1]+quad[2]+quad[3])*.25f;
        for(int index=0;index<4;index++)
        {
            Require(Vector2.Distance(inset[index],Vector2.Lerp(midpoint,quad[index],.86f))<1e-5f,
                "Baked wheel shrink must be bounded, not modify the cell footprint.");
            for(int edgeIndex=0;edgeIndex<4;edgeIndex++)
            {
                Vector2 edge=quad[(edgeIndex+1)%4]-quad[edgeIndex];
                double area=Enumerable.Range(0,4).Sum(i=>(double)Cross(quad[i],quad[(i+1)%4]));
                Require(Cross(edge,inset[index]-quad[edgeIndex])*Math.Sign(area)>0f,
                    "Baked Chance texture needs a visible gutter inside its own cell.");
            }
        }
        insets++;
        Vector2 center=BoardArtworkLayoutRules.BaseCenter(quad);
        foreach(float width in new[]{.05f,.5f,2f,8f})
        foreach(float height in new[]{.05f,.7f,3f})
        foreach(float artAngle in new[]{-.9f,0f,.4f})
        {
            var offsets=new Vector2[8];
            for(int index=0;index<8;index++)
            {
                // A tilted/thick sprite projects eight native bounds corners.
                Vector2 p=new((index&1)==0?-width*.5f:width*.5f,(index&2)==0?-height*.5f:height*.5f);
                p+=new Vector2((index&4)==0?-.03f:.03f,0f);
                offsets[index]=new Vector2(p.X*MathF.Cos(artAngle)-p.Y*MathF.Sin(artAngle),
                    p.X*MathF.Sin(artAngle)+p.Y*MathF.Cos(artAngle));
            }
            float scale=BoardArtworkLayoutRules.FitPlanarScale(quad,center,offsets);
            Require(float.IsFinite(scale)&&scale>0f&&scale<=1f,"Native artwork scale must shrink only.");
            float shortest=Enumerable.Range(0,4).Min(i=>Vector2.Distance(quad[i],quad[(i+1)%4]));
            double area=Enumerable.Range(0,4).Sum(i=>(double)Cross(quad[i],quad[(i+1)%4]));
            foreach(Vector2 offset in offsets)
            for(int edgeIndex=0;edgeIndex<4;edgeIndex++)
            {
                Vector2 edge=quad[(edgeIndex+1)%4]-quad[edgeIndex];
                double clearance=Cross(edge,center+offset*scale-quad[edgeIndex])*Math.Sign(area)/edge.Length();
                Require(clearance>=shortest*BoardArtworkLayoutRules.EdgePaddingFraction-2e-5f,
                    "Entire Chance bounds spill across a slanted cell boundary.");
            }
            fits++;
        }
    }
    Vector2 Rotate(Vector2 p)=>new(p.X*MathF.Cos(angle)-p.Y*MathF.Sin(angle),p.X*MathF.Sin(angle)+p.Y*MathF.Cos(angle));
}
var square=new[]{Vector2.Zero,Vector2.UnitY,Vector2.One,Vector2.UnitX};
var small=new[]{new Vector2(-.05f,-.05f),new Vector2(.05f,-.05f),new Vector2(.05f,.05f),new Vector2(-.05f,.05f)};
Require(BoardArtworkLayoutRules.FitPlanarScale(square,BoardArtworkLayoutRules.BaseCenter(square),small)==1f,
    "Small native art should keep its scale.");
var large=small.Select(p=>p*60f).ToArray();
float first=BoardArtworkLayoutRules.FitPlanarScale(square,BoardArtworkLayoutRules.BaseCenter(square),large);
float repeated=BoardArtworkLayoutRules.FitPlanarScale(square,BoardArtworkLayoutRules.BaseCenter(square),large);
Require(first==repeated,"Repeated fitting must always evaluate native bounds, without cumulative shrink.");
Require(BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(2f),small)==0f,"Off-cell artwork accepted.");
Reject(()=>BoardArtworkLayoutRules.BaseCenter(null));
Reject(()=>BoardArtworkLayoutRules.InsetChanceGround(null));
Reject(()=>BoardArtworkLayoutRules.BaseCenter(new[]{Vector2.Zero,Vector2.One,Vector2.UnitY,Vector2.UnitX}));
Reject(()=>BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(float.NaN,0f),small));
Reject(()=>BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(.5f),new[]{Vector2.Zero,Vector2.Zero,Vector2.Zero}));
Reject(()=>BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(.5f),new[]{Vector2.Zero,Vector2.One,new Vector2(float.NaN,0f)}));
Reject(()=>BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(.5f),small,.5f));
Reject(()=>BoardArtworkLayoutRules.FitPlanarScale(square,new Vector2(.5f),small,-.1f));
using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NativeBoardArtworkSource.cs")
    ?? throw new Exception("Missing native artwork source contract.");
using var reader=new StreamReader(stream);
string source=reader.ReadToEnd();
Require(source.Contains("if (!ModState.IsSpecialMapActive) { Restore(); return; }")&&
    source.Contains("GetCellData(cell._cellId)?.CellType==CellType.Chance")&&
    source.Contains("planarImage:_nativeChance?image:null")&&
    source.Contains("planarImage:_nativeChance?_cell._tournamentView?._tournamentRenderer:null")&&
    source.Contains("if(_nativeChance)target=BoardArtworkLayoutRules.InsetChanceGround(target);"),
    "Chance resizing must cover both baked texture and separate native crown, with native cell semantics.");
Require(source.Contains("SyncArtwork(camera ?? Camera.main);")&&source.Contains("Time.unscaledTime+.1f")&&
    source.Contains("DiscoverArtwork();"),"Late native artwork must be refreshed through the existing throttled sync.");
Require(source.Contains("IsDescendant(transform,anchor.Transform)")&&source.Contains("IsDescendant(_artAnchors[index].Transform,transform)"),
    "Nested owners must never move artwork twice.");
Require(source.Contains("Transform.GetInstanceID()==InstanceId")&&source.Contains("_cellRoot.GetInstanceID()==_cellRootId")&&
    source.Contains("Transform.parent?.Pointer==_parent?.Pointer")&&source.Contains("_planarImage.GetInstanceID()==_rendererId"),
    "Artwork bindings must guard generation and parent ownership.");
Require(source.Contains("Same(Transform.localPosition,_assignedPosition)")&&source.Contains("Same(Transform.localScale,_assignedScale)"),
    "Cleanup must restore only transforms still owned by the fit.");
Require(source.Contains("_movieClipFlags?._clip?.transform,centerBase:true")&&
    source.Contains("if(_planarImage==null || !IsOwned")&&source.Contains("_nativeLocalScale*scale"),
    "Upright animated roots must be translated without scaling their internal art.");
Require(!source.Contains("GetComponentsInChildren<SpriteRenderer>")&&!source.Contains("Transform.rotation=camera.transform.rotation"),
    "Artwork fit must never sweep unrelated descendants or flatten native rotation.");
Console.WriteLine($"PASS: {fits:N0} complete native icon bounds and {insets:N0} baked Chance texture insets contained in their own cells across both windings, slanted edges, rotated views and sizes; no enlargement/cumulative shrink; invalid bounds rejected; native scope, dynamic refresh, nested owners and cleanup source contracts.");

static float Cross(Vector2 a,Vector2 b)=>a.X*b.Y-a.Y*b.X;
static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
static void Reject(Action action)
{
    try { action(); }catch(ArgumentException){ return; }
    throw new Exception("Invalid artwork footprint accepted.");
}
