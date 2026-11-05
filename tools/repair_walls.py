from pathlib import Path
p=Path('src/Contact.cs');s=p.read_text();a=s.index(' public static bool Walls(');s=s[:a]+''' public static bool Walls(PhysicsDirectSpaceState3D space, Vehicle v, Vec start, float carY, Vector3 boxSize) {
  if (v.servicing || v.pitActive) return false;
  var motion = ToWorld(v.position - start, 0); var s3 = ToWorld(start, carY + boxSize.Y * .5f);
  var q = new PhysicsShapeQueryParameters3D { Shape = new BoxShape3D { Size=boxSize }, Transform = new Transform3D(new Basis(Vector3.Up,v.heading),s3), CollisionMask=2, Margin=.01f };
  bool touched=false;
  // CastMotion ignores shapes already overlapping at the start. Resolve those explicitly, even at rest.
  var rest=space.GetRestInfo(q);
  if(rest.Count>0) {
   var n=(Vector3)rest["normal"];n.Y=0;n=n.Normalized();var pts=space.CollideShape(q,16);float depth=0;
   for(int i=0;i+1<pts.Length;i+=2) depth=Mathf.Max(depth,Mathf.Abs((pts[i]-pts[i+1]).Dot(n)));
   if(n.LengthSquared()>.1f) {s3+=n*(depth+.02f);start=new Vec(s3.X,s3.Z);Respond(v,n);touched=true;}
  }
  float dist=motion.Length();if(dist<.0001f){v.position=start;return touched;}
  q.Transform=new Transform3D(q.Transform.Basis,s3);q.Motion=motion;
  var cast=space.CastMotion(q);float safe=cast[0],unsafeF=cast[1];
  if(safe>=1f){if(touched)v.position=start+new Vec(motion.X,motion.Z);v.lastIntegrated=v.position;return touched;}
  q.Transform=new Transform3D(q.Transform.Basis,s3+motion*unsafeF);q.Motion=Vector3.Zero;
  var info=space.GetRestInfo(q);Vector3 normal=info.Count>0?(Vector3)info["normal"]:-motion.Normalized();normal.Y=0;normal=normal.Normalized();if(normal.Dot(motion)>0)normal=-normal;
  var stop=s3+motion*(Mathf.Max(0,safe*dist-.02f)/dist);v.position=new Vec(stop.X,stop.Z);
  Respond(v,normal);return true;
 }
 static void Respond(Vehicle v,Vector3 normal) {
  Vec n=new Vec(normal.X,normal.Z);float into=Vec.Dot(v.velocity,n);
  if(into<0){v.velocity-=n*(into*1.1f);v.damage=Mathf.Min(1,v.damage+Mathf.Abs(into)*.002f);v.yawRate*=.5f;}
  // Preserve heading and tangential velocity instead of snapping the car toward an arbitrary wall tangent.
  v.speed=v.velocity.Magnitude;v.lastIntegrated=v.position;
 }
}}
''';p.write_text(s)
p=Path('src/RuntimeChecks.cs');s=p.read_text();idx=s.index('   var g=Contact.Ground');s=s[:idx]+'''   foreach(float speed in new[]{0f,10f,-10f}) {
    var v=new Vehicle { position=new Vec(0,-.5f),velocity=new Vec(0,speed),speed=Math.Abs(speed) };
    Check(Contact.Walls(space,v,new Vec(0,-.5f),0,new Vector3(2,1,4)),"initial overlap ignored");Check(v.position.z < -2f,"initial overlap not cleared");Check(v.speed<=Math.Abs(speed)+.01f,"overlap added energy");
   }
'''+s[idx:];p.write_text(s)
