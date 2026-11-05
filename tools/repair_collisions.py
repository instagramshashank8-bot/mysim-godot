from pathlib import Path
import json
rows=json.load(open('outputs/repair-20261029/geometry.json'))
text='namespace MySimulator.Rebuild {\n // Bounds measured from the assembled recovered meshes by RuntimeChecks (2026-10-29).\n public static class VehicleDimensions {\n'
for name,key in [('Width','width'),('Height','height'),('Length','length')]:text+='  public static readonly float[] '+name+' = {'+','.join(f'{r[key]:.5f}f' for r in rows)+'};\n'
text+=' }\n}\n';Path('src/Logic/VehicleDimensions.cs').write_text(text)
p=Path('src/Logic/Simulation.cs');s=p.read_text();a=s.index('        public void Collisions()');b=s.index('        public List<Vehicle> Standings()',a)
s=s[:a]+'''        public void Collisions()
        {
            for(int i=0;i<cars.Count;i++)for(int j=i+1;j<cars.Count;j++)
            {
                Vehicle a=cars[i],b=cars[j];if(a.externalPhysics||b.externalPhysics||a.retired||b.retired)continue;
                int ac=Math.Max(0,Math.Min(CarCatalog.Count-1,a.carModel)),bc=Math.Max(0,Math.Min(CarCatalog.Count-1,b.carModel));
                Vec delta=b.position-a.position;
                float al=VehicleDimensions.Length[ac]*.5f,aw=VehicleDimensions.Width[ac]*.5f;
                float bl=VehicleDimensions.Length[bc]*.5f,bw=VehicleDimensions.Width[bc]*.5f;
                if(delta.Magnitude>al+aw+bl+bw)continue;
                Vec af=a.Forward,ar=a.Right,bf=b.Forward,br=b.Right,normal=new Vec(1,0);float overlap=float.MaxValue;bool separated=false;
                // Separating-axis test of both oriented footprints; no circular contacts outside a car's body.
                foreach(Vec axis in new[]{af,ar,bf,br}) {
                    float ra=al*Math.Abs(Vec.Dot(af,axis))+aw*Math.Abs(Vec.Dot(ar,axis));
                    float rb=bl*Math.Abs(Vec.Dot(bf,axis))+bw*Math.Abs(Vec.Dot(br,axis));
                    float d=Vec.Dot(delta,axis),depth=ra+rb-Math.Abs(d);
                    if(depth<=0){separated=true;break;}
                    if(depth<overlap){overlap=depth;normal=axis*(d<0?-1:1);}
                }
                if(separated)continue;
                float invA=1/RecoveredVehicleProfiles.Mass[ac],invB=1/RecoveredVehicleProfiles.Mass[bc],invSum=invA+invB;
                a.position-=normal*(overlap*invA/invSum);b.position+=normal*(overlap*invB/invSum);
                float approach=Vec.Dot(a.velocity-b.velocity,normal);
                if(approach>0) {
                    float impulse=approach*1.1f/invSum;
                    a.velocity-=normal*(impulse*invA);b.velocity+=normal*(impulse*invB);
                    float damage=MathUtil.Clamp(approach*.005f,0,.1f);
                    a.damage=MathUtil.Clamp(a.damage+damage,0,1);b.damage=MathUtil.Clamp(b.damage+damage,0,1);
                }
                a.speed=a.velocity.Magnitude;b.speed=b.velocity.Magnitude;
                // Contact correction is already resolved; do not apply the generic external-position penalty again.
                a.lastIntegrated=a.position;b.lastIntegrated=b.position;
            }
        }
'''+s[b:];p.write_text(s)
p=Path('src/CrashBatch.cs');s=p.read_text().replace('new Vector3(1.96f, 1.0f, 4.06f)','new Vector3(VehicleDimensions.Width[carIdx], Mathf.Clamp(VehicleDimensions.Height[carIdx], .6f, 1.5f), VehicleDimensions.Length[carIdx])').replace('Contact.MarkGood(v, g.near);','Contact.MarkGood(v, g.near && Mathf.Abs(v.lateral) <= 30f);');p.write_text(s)
