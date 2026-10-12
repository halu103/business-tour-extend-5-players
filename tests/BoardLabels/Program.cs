using System.Numerics;
using BusinessTourFiveRealms;

int checks=0;
if (BoardLabelLayoutRules.NameInnerFraction < 0f || BoardLabelLayoutRules.RentOuterFraction>1f ||
    BoardLabelLayoutRules.NameOuterFraction >= BoardLabelLayoutRules.RentInnerFraction ||
    BoardLabelLayoutRules.RentInnerFraction >= BoardLabelLayoutRules.RentOuterFraction ||
    BoardLabelLayoutRules.NameInnerFraction >= BoardLabelLayoutRules.NameOuterFraction)
    throw new Exception("Rent and name bands must be disjoint and inside their own tile.");
foreach(int count in new[]{32,40})
foreach(int winding in new[]{-1,1})
foreach(float angle in new[]{-.37f,0f,.63f})
foreach(float radius in new[]{1f,4f,10f})
{
    var geometry=CanonicalBoardGeometry.Create(new Vector2(2f,-3f),radius,radius*.57f,count,winding,
        count==40?BoardLabelLayoutRules.FivePlayerInnerScale:CanonicalBoardGeometry.DefaultInnerScale);
    foreach(var cell in geometry.Cells)
    foreach(var band in new[]{(From:0f,To:1f),
        (From:BoardLabelLayoutRules.NameInnerFraction,To:BoardLabelLayoutRules.ServiceNameOuterFraction),
        (From:BoardLabelLayoutRules.NameInnerFraction,To:BoardLabelLayoutRules.NameOuterFraction),
        (From:BoardLabelLayoutRules.RentInnerFraction,To:BoardLabelLayoutRules.RentOuterFraction)})
    {
        var quad=new[]{Rotate(Vector2.Lerp(cell.InnerStart,cell.OuterStart,band.From)),
            Rotate(Vector2.Lerp(cell.InnerStart,cell.OuterStart,band.To)),
            Rotate(Vector2.Lerp(cell.InnerEnd,cell.OuterEnd,band.To)),
            Rotate(Vector2.Lerp(cell.InnerEnd,cell.OuterEnd,band.From))};
        Vector2 center=(quad[0]+quad[1]+quad[2]+quad[3])*.25f;
        float shortest=Enumerable.Range(0,4).Min(i=>Vector2.Distance(quad[i],quad[(i+1)%4]));
        float padding=shortest*BoardLabelLayoutRules.EdgePaddingFraction;
        foreach(float width in new[]{.1f,1f,4f,12f})
        foreach(float height in new[]{.05f,.25f,1f})
        foreach(float maximumScale in new[]{1f,BoardLabelLayoutRules.MaximumLabelEnlargement})
        {
            float scale=BoardLabelLayoutRules.FitScale(quad,center,width,height,padding,maximumScale);
            if(!float.IsFinite(scale)||scale<=0f||scale>maximumScale) throw new Exception("Invalid label scale.");
            float area=0f;
            for(int i=0;i<4;i++) area+=Cross(quad[i],quad[(i+1)%4]);
            foreach(float sx in new[]{-1f,1f})
            foreach(float sy in new[]{-1f,1f})
            {
                Vector2 corner=center+new Vector2(sx*width,sy*height)*(.5f*scale);
                for(int i=0;i<4;i++)
                {
                    Vector2 edge=quad[(i+1)%4]-quad[i];
                    float distance=Cross(edge,corner-quad[i])*Math.Sign(area)/edge.Length();
                    if(distance<padding-1e-5f) throw new Exception("Label spills across its cell boundary.");
                }
            }
            checks++;
        }
    }
    Vector2 Rotate(Vector2 p)=>new(p.X*MathF.Cos(angle)-p.Y*MathF.Sin(angle),p.X*MathF.Sin(angle)+p.Y*MathF.Cos(angle));
}
var square=new[]{Vector2.Zero,Vector2.UnitX,Vector2.One,Vector2.UnitY};
if(BoardLabelLayoutRules.FitScale(square,new Vector2(2f,2f),1f,1f)!=0f) throw new Exception("Off-cell label accepted.");
Reject(()=>BoardLabelLayoutRules.FitScale(square,Vector2.Zero,float.NaN,1f));
Reject(()=>BoardLabelLayoutRules.FitScale(square,Vector2.Zero,1f,0f));
Reject(()=>BoardLabelLayoutRules.FitScale(new[]{Vector2.Zero,Vector2.One,Vector2.UnitX,Vector2.UnitY},Vector2.Zero,1f,1f));
Reject(()=>BoardLabelLayoutRules.FitScale(square,new Vector2(.5f),1f,1f,0f,float.NaN));
Reject(()=>BoardLabelLayoutRules.FitScale(square,new Vector2(.5f),1f,1f,0f,0f));
int readabilityChecks=0;
foreach(float height in new[]{.1f,.5f,1f})
foreach(float size in new[]{3f,8f,15f})
foreach(int pixels in new[]{720,1080,1440})
foreach(bool rent in new[]{false,true})
foreach(int lines in new[]{1,2,3})
{
    float scale=BoardLabelLayoutRules.ReadabilityScale(height,size,pixels,rent,lines);
    float actual=height*scale*pixels/(2f*size*lines),target=rent?BoardLabelLayoutRules.RentTargetPixels:BoardLabelLayoutRules.NameTargetPixels;
    if (scale<=0f || scale>BoardLabelLayoutRules.MaximumLabelEnlargement || actual>target+.0001f ||
        (scale<BoardLabelLayoutRules.MaximumLabelEnlargement && Math.Abs(actual-target)>.0001f))
        throw new Exception("Label readability projection is not bounded or misses its pixel target.");
    readabilityChecks++;
}
Reject(()=>BoardLabelLayoutRules.ReadabilityScale(0f,3f,1080,false));
Reject(()=>BoardLabelLayoutRules.ReadabilityScale(float.NaN,3f,1080,false));
Reject(()=>BoardLabelLayoutRules.ReadabilityScale(1f,-1f,1080,false));
Reject(()=>BoardLabelLayoutRules.ReadabilityScale(1f,3f,0,false));
Reject(()=>BoardLabelLayoutRules.ReadabilityScale(1f,3f,1080,false,0));
var priorBoard=CanonicalBoardGeometry.Create(Vector2.Zero,9f,5f,40,1);
var widerTiles=CanonicalBoardGeometry.Create(Vector2.Zero,9f,5f,40,1,BoardLabelLayoutRules.FivePlayerInnerScale);
if (!priorBoard.OuterBoundary.SequenceEqual(widerTiles.OuterBoundary))
    throw new Exception("More readable forty-cell tiles must not enlarge the outer frame into the HUD.");
foreach(var pair in priorBoard.Cells.Zip(widerTiles.Cells))
{
    float priorDepth=Vector2.Distance(pair.First.InnerStart,pair.First.OuterStart);
    float newDepth=Vector2.Distance(pair.Second.InnerStart,pair.Second.OuterStart);
    if (newDepth/priorDepth<1.28f || newDepth/priorDepth>1.29f)
        throw new Exception("The forty-cell annulus does not provide the bounded readability expansion.");
}
int depthChecks=0;
foreach(float pitch in new[]{-.6f,0f,.4f})
foreach(float yaw in new[]{-.3f,0f,.7f})
foreach(Vector3 source in new[]{new Vector3(0f,0f,-.2f),new Vector3(0f,.1f,0f),new Vector3(.1f,.2f,.3f)})
{
    Quaternion camera=Quaternion.CreateFromYawPitchRoll(yaw,pitch,0f);
    Vector3 forward=Vector3.Transform(Vector3.UnitZ,camera);
    Vector3 right=Vector3.Transform(Vector3.UnitX,camera),up=Vector3.Transform(Vector3.UnitY,camera);
    Vector3 offset=BoardLabelLayoutRules.CameraDepthOffset(source,forward);
    if(Math.Abs(Vector3.Dot(offset,right))>1e-6f || Math.Abs(Vector3.Dot(offset,up))>1e-6f ||
        Math.Abs(Vector3.Dot(offset,forward)-Vector3.Dot(source,forward))>1e-6f)
        throw new Exception("Native label depth shifts its fitted camera-plane footprint.");
    depthChecks++;
}
Reject(()=>BoardLabelLayoutRules.CameraDepthOffset(Vector3.UnitZ,Vector3.Zero));
Reject(()=>BoardLabelLayoutRules.CameraDepthOffset(new Vector3(float.NaN,0f,0f),Vector3.UnitZ));
Reject(()=>BoardLabelLayoutRules.CameraDepthOffset(new Vector3(float.MaxValue),Vector3.Normalize(Vector3.One)));
foreach(string family in new[]{"TextMeshPro/Distance Field","TextMeshPro/Mobile/Distance Field"})
{
    if(BoardLabelLayoutRules.RentOverlayShaderFor(family)!=family+" Overlay" ||
        BoardLabelLayoutRules.RentOverlayShaderFor(family+" Overlay")!=family+" Overlay")
        throw new Exception("Rent overlay must use the matching native TMP family only.");
}
foreach(string unsupported in new string[]{null,"","Sprites/Default","TextMeshPro/Bitmap",
    "TextMeshPro/Distance Field (Surface)","Custom/Distance Field"})
    if(BoardLabelLayoutRules.RentOverlayShaderFor(unsupported)!=null)
        throw new Exception("An unknown font shader must keep its native material.");
if(BoardLabelLayoutRules.BoardInformationLayerName!="GameFieldOver" ||
    BoardLabelLayoutRules.CharacterLayerName!="Characters" || BoardLabelLayoutRules.BoardTransparentRenderQueue!=3000)
    throw new Exception("Board text must render in the verified transparent queue below Characters.");
int frames=0;
foreach(int count in new[]{32,40})
foreach(int winding in new[]{-1,1})
foreach(float angle in new[]{-.37f,0f,.63f})
foreach(float radius in new[]{1f,4f,10f})
foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f})
{
    var board=CanonicalBoardGeometry.Create(new Vector2(2f,-3f),radius,radius*.57f,count,winding);
    var points=board.OuterCorners.Select(p=>new Vector2(p.X*MathF.Cos(angle)-p.Y*MathF.Sin(angle),
        p.X*MathF.Sin(angle)+p.Y*MathF.Cos(angle))).ToArray();
    float left=points.Min(p=>p.X),right=points.Max(p=>p.X),bottom=points.Min(p=>p.Y),top=points.Max(p=>p.Y);
    OrthographicBoardFrame frame=BoardViewportLayoutRules.Fit(left,right,bottom,top,aspect);
    float oldSize=Math.Max((right-left+1.4f)/(2f*aspect*.76f),(top-bottom+1.7f)/(2f*.78f));
    float enlargement=oldSize/frame.Size;
    if(enlargement<1.045f || enlargement>1.285f) throw new Exception("Board enlargement exceeds the bounded viewport policy.");
    float priorFrame=Math.Max((right-left+1.4f)/(2f*aspect*.78f),(top-bottom+1.7f)/(2f*.83f));
    if(priorFrame/frame.Size<1.025f || priorFrame/frame.Size>1.23f)
        throw new Exception("The new framing does not enlarge the prior five-player board safely.");
    foreach(Vector2 p in points)
    {
        Vector2 viewport=new(.5f+(p.X-frame.Offset.X)/(2f*frame.Size*aspect),
            .5f+(p.Y-frame.Offset.Y)/(2f*frame.Size));
        if(viewport.X<BoardViewportLayoutRules.Left-1e-6f || viewport.X>BoardViewportLayoutRules.Right+1e-6f ||
            viewport.Y<BoardViewportLayoutRules.Bottom-1e-6f || viewport.Y>BoardViewportLayoutRules.Top+1e-6f)
            throw new Exception("Enlarged board intrudes into reserved viewport regions.");
    }
    frames++;
}
Reject(()=>BoardViewportLayoutRules.Fit(0f,0f,0f,1f,1f));
Reject(()=>BoardViewportLayoutRules.Fit(0f,1f,0f,1f,0f));
Reject(()=>BoardViewportLayoutRules.Fit(float.NaN,1f,0f,1f,1f));
Reject(()=>BoardViewportLayoutRules.Fit(-float.MaxValue,float.MaxValue,0f,1f,1f));
string ReadSource(string name)
{
    using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new Exception("Missing board-label source contract.");
    using var reader=new StreamReader(stream);
    return reader.ReadToEnd();
}
string labelSource=ReadSource("NativeBoardLabelSource.cs"),boardSource=ReadSource("NativeCanonicalBoardSource.cs");
if (!labelSource.Contains("_role==BoardLabelRole.Rent") ||
    !labelSource.Contains("FitBoardSorting();") ||
    !labelSource.Contains("BoardLabelLayoutRules.ReadabilityScale(") ||
    !labelSource.Contains("_tmp.textInfo?.lineCount") ||
    !boardSource.Contains("BoardLabelLayoutRules.RentOuterFraction,BoardLabelRole.Rent)") ||
    !boardSource.Contains("cell._rentView==null?") ||
    !boardSource.Contains("if (!ModState.IsSpecialMapActive) { Restore(); return; }"))
    throw new Exception("Readable board text must stay restricted to native views on the special map.");
if (!labelSource.Contains("Transform.GetInstanceID()==_transformId") ||
    !labelSource.Contains("_renderer.GetInstanceID()==_rendererId") ||
    !labelSource.Contains("Transform.parent?.Pointer==_parent?.Pointer") ||
    !labelSource.Contains("if (_renderer.sortingLayerID==_assignedLayer) _renderer.sortingLayerID=_nativeLayer;") ||
    !labelSource.Contains("if (_renderer.sortingOrder==_assignedOrder) _renderer.sortingOrder=_nativeOrder;"))
    throw new Exception("Native renderer cleanup must be generation- and ownership-guarded.");
if (labelSource.Contains("RentForegroundDepth") || labelSource.Contains("camera.nearClipPlane") ||
    labelSource.Contains("short.MaxValue") || labelSource.Contains(".font.material=") ||
    labelSource.Contains(".DisplayByText(") || labelSource.Contains(".SetText("))
    throw new Exception("Board text must not pull ahead of pawns or mutate native amounts/shared font assets.");
if(!labelSource.Contains("if (_role!=BoardLabelRole.Rent || !IsOwned) return;") ||
    !labelSource.Contains("new Material(source)") || !labelSource.Contains("SameMaterial(_tmp.fontSharedMaterial,owned)") ||
    !labelSource.Contains("SameMaterial(_renderer.sharedMaterial,owned)") ||
    !labelSource.Contains("_tmp.GetInstanceID()==_tmpId") ||
    !labelSource.Contains("_rentMaterial.mainTexture?.Pointer!=source.mainTexture.Pointer") ||
    !labelSource.Contains("if(!pending)") || !labelSource.Contains("PendingMaterialCleanup") ||
    !labelSource.Contains("SortingLayer.GetLayerValueFromID(foreground)>=SortingLayer.GetLayerValueFromID(characters)"))
    throw new Exception("Rent clones require per-glyph ownership, preserved atlas and retriable cleanup below Characters.");
Console.WriteLine($"PASS: {checks:N0} separated/enlarged rent/name footprints; {readabilityChecks:N0} pixel readability projections; 40 deeper tiles preserve the outer frame; {depthChecks:N0} native depth projections; matched TMP shader/fallback and material ownership contracts; {frames:N0} camera frames, both windings and 4:3/16:9/21:9 aspects.");

static float Cross(Vector2 a,Vector2 b)=>a.X*b.Y-a.Y*b.X;
static void Reject(Action action)
{
    try { action(); } catch(ArgumentException) { return; }
    throw new Exception("Invalid footprint was accepted.");
}
