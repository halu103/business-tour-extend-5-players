using System.Numerics;
using BusinessTourFiveRealms;

int checks=0;
foreach(int count in new[]{32,40})
foreach(int winding in new[]{-1,1})
foreach(float angle in new[]{-.37f,0f,.63f})
foreach(float radius in new[]{1f,4f,10f})
{
    var geometry=CanonicalBoardGeometry.Create(new Vector2(2f,-3f),radius,radius*.57f,count,winding);
    foreach(var cell in geometry.Cells)
    foreach(var band in new[]{(From:0f,To:1f),(From:0f,To:.52f),(From:.7f,To:1f)})
    {
        var quad=new[]{Rotate(Vector2.Lerp(cell.InnerStart,cell.OuterStart,band.From)),
            Rotate(Vector2.Lerp(cell.InnerStart,cell.OuterStart,band.To)),
            Rotate(Vector2.Lerp(cell.InnerEnd,cell.OuterEnd,band.To)),
            Rotate(Vector2.Lerp(cell.InnerEnd,cell.OuterEnd,band.From))};
        Vector2 center=(quad[0]+quad[1]+quad[2]+quad[3])*.25f;
        float shortest=Enumerable.Range(0,4).Min(i=>Vector2.Distance(quad[i],quad[(i+1)%4]));
        float padding=shortest*.04f;
        foreach(float width in new[]{.1f,1f,4f,12f})
        foreach(float height in new[]{.05f,.25f,1f})
        {
            float scale=BoardLabelLayoutRules.FitScale(quad,center,width,height,padding);
            if(!float.IsFinite(scale)||scale<=0f||scale>1f) throw new Exception("Invalid label scale.");
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
Console.WriteLine($"PASS: {checks:N0} label footprints within 32/40-cell pentagons, both windings, rotated camera planes, short/long labels and input guards.");

static float Cross(Vector2 a,Vector2 b)=>a.X*b.Y-a.Y*b.X;
static void Reject(Action action)
{
    try { action(); } catch(ArgumentException) { return; }
    throw new Exception("Invalid footprint was accepted.");
}
