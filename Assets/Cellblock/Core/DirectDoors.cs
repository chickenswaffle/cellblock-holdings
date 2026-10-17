using System;
using System.Collections.Generic;
using System.Linq;
namespace Cellblock.Core
{
    public sealed partial class World
    {
        static Plan CopyDoorPlan(Plan source)
        {
            if(source==null)return new Plan{Name="New doorway"};
            var p=new Plan{Name=source.Name,RelocatesDining=source.RelocatesDining,Dining=source.Dining};
            p.RemoveWalls.AddRange(source.RemoveWalls);p.RemoveFixtures.AddRange(source.RemoveFixtures);p.UpgradeIds.AddRange(source.UpgradeIds);
            p.Walls.AddRange(source.Walls.Select(w=>w.Copy()));p.Fixtures.AddRange(source.Fixtures.Select(f=>f.Copy()));
            p.HomeMoves.AddRange(source.HomeMoves.Select(h=>new HomeMove{Resident=h.Resident,Home=h.Home}));return p;
        }
        // A click can select either a planned boundary or an installed wall. Shared geometry is
        // resolved to the installed owner before quoting, so prefab deduplication retains the cut.
        public Plan DoorAt(Plan draft,Wall selected,Point click)
        {
            if(selected==null){Notice="Click a wall where you want the door.";return null;}
            int draftIndex=draft==null?-1:draft.Walls.IndexOf(selected);
            Wall source=Data.Walls.FirstOrDefault(w=>w==selected&&(draft==null||!draft.RemoveWalls.Contains(w.Id)));
            if(draftIndex>=0)
            {
                Point on=Geometry.OnLength(selected,Geometry.NearestFraction(selected,click));
                source=Data.Walls.Where(w=>!draft.RemoveWalls.Contains(w.Id)).FirstOrDefault(w=>Geometry.Distance(Geometry.OnLength(w,Geometry.NearestFraction(w,on)),on)<.12f);
                if(source!=null)
                {
                    float position=Geometry.NearestFraction(source,on);
                    if(!SharedDoorBoundary(draft,source,position))
                    {Notice="Move the doorway away from this junction so its full width borders the new structure.";return null;}
                }

            }
            Wall target=source??(draftIndex>=0?selected:null);
            if(target==null){Notice="That wall is no longer in this plan.";return null;}
            float fraction=Geometry.NearestFraction(target,click);float length=Geometry.Length(target);
            if(target.Door>=0&&Math.Abs(target.Door-fraction)*length<(target.DoorWidth+1.6f)/2+.4f){Notice="There is already a door here. Choose a solid section of wall.";return null;}
            Wall[] parts=DoorParts(target,fraction);if(parts==null)return null;
            Plan result=CopyDoorPlan(draft);
            if(source!=null){result.RemoveWalls.Add(source.Id);result.Walls.InsertRange(0,parts);}
            else{result.Walls.RemoveAt(draftIndex);result.Walls.InsertRange(draftIndex,parts);}
            Point center=Geometry.OnLength(target,fraction),before=Geometry.OnLength(target,Math.Max(0,fraction-.02f)),after=Geometry.OnLength(target,Math.Min(1,fraction+.02f));
            Point tangent=after-before;tangent=tangent*(1/Math.Max(.001f,tangent.Length));Point normal=new Point(-tangent.Y,tangent.X);
            var furniture=Data.Fixtures.Where(f=>!result.RemoveFixtures.Contains(f.Id)).Concat(result.Fixtures).ToList();var clear=new Navigation(new Wall[0],furniture);
            foreach(float offset in new[]{-.62f,-.3f,0,.3f,.62f})if(!clear.Clear(center+tangent*offset+normal*.65f,center+tangent*offset-normal*.65f))
            {Notice="Leave the doorway clear on both sides. Move the nearby furnishing or choose another position.";return null;}
            Notice="Door added at your click. Review the total, then approve the project.";return result;
        }
        // Distance to a boundary is 1-Lipschitz along arc length. Samples <=4 cm apart
        // with a 2 cm tolerance margin certify continuous coverage within 12 cm,
        // including gaps between samples, across the full doorway and its jambs.
        static bool SharedDoorBoundary(Plan draft,Wall source,float center)
        {
            float length=Geometry.Length(source),start=center*length-.9f,end=center*length+.9f;
            if(start<0||end>length)return false;
            var edges=draft.Walls.SelectMany(w=>Geometry.Segments(w,false)).ToList();
            int steps=(int)Math.Ceiling((end-start)/.04f);
            for(int i=0;i<=steps;i++)
            {
                Point point=Geometry.OnLength(source,(start+(end-start)*i/steps)/length);
                if(!edges.Any(edge=>Geometry.DistanceToSegment(point,edge.A,edge.B)<.10f))return false;
            }
            return true;
        }
        Wall[] DoorParts(Wall source,float door)
        {
            var replacement=source.Copy();replacement.Id="";replacement.Door=door;replacement.DoorWidth=1.6f;string error=Geometry.Validate(replacement);
            if(error!=""){Notice=error;return null;}if(source.Door<0)return new[]{replacement};
            float cut=(source.Door+door)/2;Wall first=source.Copy(),second=source.Copy();first.Id=second.Id="";
            if(!source.Curved){first.B=second.A=source.At(cut);}
            else
            {
                var samples=Geometry.Sample(source);float remaining=Geometry.Length(source)*cut,t=0;
                for(int i=1;i<samples.Count;i++){float span=Geometry.Distance(samples[i-1],samples[i]);if(remaining<=span){t=(i-1+remaining/Math.Max(.001f,span))/(samples.Count-1);break;}remaining-=span;}
                Point left=source.A+(source.Control-source.A)*t,right=source.Control+(source.B-source.Control)*t;
                first.Control=left;second.Control=right;first.B=second.A=left+(right-left)*t;
            }
            Point oldCenter=Geometry.OnLength(source,source.Door),newCenter=Geometry.OnLength(source,door);
            first.Door=Geometry.NearestFraction(first,source.Door<cut?oldCenter:newCenter);first.DoorWidth=source.Door<cut?source.DoorWidth:1.6f;
            second.Door=Geometry.NearestFraction(second,source.Door>cut?oldCenter:newCenter);second.DoorWidth=source.Door>cut?source.DoorWidth:1.6f;
            foreach(Wall part in new[]{first,second})if((error=Geometry.Validate(part))!=""){Notice=error;return null;}
            return new[]{first,second};
        }
    }
}
