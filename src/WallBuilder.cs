using Godot;
using System.Text.RegularExpressions;
namespace MySim {
// One collision-role policy shared by live scenes and collision baking.
public static class WallBuilder {
 static readonly Regex Barrier=new("armco|barrier|wall|fence|guardrail|bollard|tyrewall|tirewall",RegexOptions.IgnoreCase);
 static readonly Regex Structure=new("^(arc_|prop_|veh_)|building|bridge|pillar|grandstand|garage|pitwall",RegexOptions.IgnoreCase);
 static readonly Regex Decoration=new("overlay|decal|skid|dust|shadow|sky|cloud|glow|crowd|spectator|^org_|tree|bush|grass|leaf",RegexOptions.IgnoreCase);
 public static bool IsSolid(string name)=>Barrier.IsMatch(name);
 public static bool IsSolid(MeshInstance3D mesh) {
  string name=mesh.Name.ToString();
  if(!mesh.IsVisibleInTree()||mesh.Mesh==null||Decoration.IsMatch(name))return false;
  if(name.Contains("flag",System.StringComparison.OrdinalIgnoreCase)&&!name.Contains("pole",System.StringComparison.OrdinalIgnoreCase))return false;
  if(Barrier.IsMatch(name))return true;
  if(name.StartsWith("gnd_",System.StringComparison.OrdinalIgnoreCase)||name.StartsWith("road",System.StringComparison.OrdinalIgnoreCase))return false;
  if(Structure.IsMatch(name))return true;
  for(int i=0;i<mesh.Mesh.GetSurfaceCount();i++)if(Barrier.IsMatch(mesh.Mesh.SurfaceGetMaterial(i)?.ResourceName??""))return true;
  return false;
 }
 // Keep every source triangle: upward bridge deck faces support the car on layer1;
 // sides and downward soffits remain walls on layer2. Never exclude the whole bridge RID.
 public static Vector3[] WallFaces(MeshInstance3D mesh,out Vector3[] support) {
  var walls=new System.Collections.Generic.List<Vector3>();var floor=new System.Collections.Generic.List<Vector3>();
  bool bridge=mesh.Name.ToString().Contains("bridge",System.StringComparison.OrdinalIgnoreCase);
  var xf=mesh.GlobalTransform;
  var faces=mesh.Mesh.GetFaces();
  for(int i=0;i+2<faces.Length;i+=3){
   var a=xf*faces[i];var b=xf*faces[i+1];var c=xf*faces[i+2];
   // Godot front faces are clockwise, so the geometric cross points inward.
   var outward=-(b-a).Cross(c-a).Normalized();var target=bridge&&outward.Y>.98f?floor:walls;
   target.Add(faces[i]);target.Add(faces[i+1]);target.Add(faces[i+2]);
  }
  support=floor.ToArray();return walls.ToArray();
 }
 internal static void AddFaces(MeshInstance3D mesh,Vector3[] faces,uint layer,string name){
  if(faces.Length==0)return;
  bool metres=System.Environment.GetEnvironmentVariable("MYSIM_METRE_WALL_COLLIDERS")!="0";var data=faces;
  if(metres){data=new Vector3[faces.Length];for(int i=0;i<faces.Length;i++)data[i]=mesh.GlobalBasis*faces[i];}
  var shape=new ConcavePolygonShape3D {Data=data,BackfaceCollision=true};
  var body=new StaticBody3D {Name=name,CollisionLayer=layer,CollisionMask=0};
  // Bake only the basis into collision vertices, keeping translation separate for precision.
  // This preserves every source triangle and its world pose while testing solver-unit conditioning.
  if(metres){body.TopLevel=true;body.Transform=new Transform3D(Basis.Identity,mesh.GlobalPosition);}
  body.AddChild(new CollisionShape3D {Shape=shape});mesh.AddChild(body);
 }
 public static int Build(Node3D track) {
  int count=0,supportTriangles=0;
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)) {
   var mesh=(MeshInstance3D)node;if(!IsSolid(mesh))continue;
   var walls=WallFaces(mesh,out var support);AddFaces(mesh,walls,2,mesh.Name+"_col");AddFaces(mesh,support,1,mesh.Name+"_support");count++;supportTriangles+=support.Length/3;
  }
  GD.Print("WALLS physical meshes=",count," bridge support triangles=",supportTriangles);return count;
 }
 // Collision-overlay evidence mode hook: one flag (MYSIM_COLLISION_OVERLAY=1) plus optional value envs
 // (MYSIM_COLLISION_OVERLAY_AUDIT=path, MYSIM_COLLIDER_TRIM=path). Attach only; the overlay builds
 // deferred on its first physics tick so meshes added later in the same frame (e.g. the drive surface)
 // are included. Attaching nothing when the flag is unset = default rendering unchanged.
 public static void AttachCollisionOverlay(Node3D layer){
  if(System.Environment.GetEnvironmentVariable("MYSIM_COLLISION_OVERLAY")!="1")return;
  layer.AddChild(new CollisionOverlay {Name="CollisionOverlay"});
 }
}
// Collision-overlay evidence node: renders every static collider as a transparent colour-coded shape OVER the
// visible scene (no depth test, so buried/offset colliders are directly visible against their visual counterpart),
// and writes a JSON audit of every collider: body name/path (WallBuilder names bodies "<mesh>_col"/"_support", so
// the name IS the visible-source mapping), shape type, collision layer/mask, world AABB, matched visible mesh path,
// collider-to-mesh centre offset (catches displaced/over-scaled colliders = the invisible-wall class), unmatched
// (counterpartless) colliders, drive-surface width deltas vs the nearest visible road mesh, and any per-name
// collider trims applied from the MYSIM_COLLIDER_TRIM evidence file (CollisionLayer/Mask zeroed per named body
// only; never blanket; fully reversible; audited with the supplied evidence reference).
public partial class CollisionOverlay : Node3D {
 bool built;
 public override void _PhysicsProcess(double delta){if(built)return;built=true;Build();}
 static Color LayerColor(uint layer){
  bool floor=(layer&1u)!=0u,walls=(layer&2u)!=0u;
  if(floor&&walls)return new Color(0.95f,0.9f,0.2f,0.32f);
  if(walls)return new Color(0.95f,0.25f,0.2f,0.32f);
  if(floor)return new Color(0.3f,0.95f,0.4f,0.32f);
  return new Color(0.7f,0.4f,0.95f,0.32f);
 }
 static Aabb WorldAabb(Aabb local,Transform3D transform){
  var corners=new Vector3[8];int i=0;
  for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)corners[i++]=transform*new Vector3(x==0?local.Position.X:local.End.X,y==0?local.Position.Y:local.End.Y,z==0?local.Position.Z:local.End.Z);
  var result=new Aabb(corners[0],Vector3.Zero);for(int c=1;c<8;c++)result=result.Expand(corners[c]);
  return result;
 }
 static MeshInstance3D ShapeVisual(Shape3D shape,Color color){
  Mesh mesh=null;
  switch(shape){
   case BoxShape3D b:mesh=new BoxMesh{Size=b.Size};break;
   case CylinderShape3D c:mesh=new CylinderMesh{Height=c.Height,TopRadius=c.Radius,BottomRadius=c.Radius,RadialSegments=12};break;
   case SphereShape3D s:mesh=new SphereMesh{Radius=s.Radius,Height=s.Radius*2f,RadialSegments=10,Rings=5};break;
   case CapsuleShape3D cap:mesh=new CapsuleMesh{Radius=cap.Radius,Height=cap.Height};break;
   case WorldBoundaryShape3D:mesh=new QuadMesh{Size=new Vector2(60f,60f)};break;
   case ConcavePolygonShape3D cv:{
    if(cv.Data==null||cv.Data.Length<9)break;
    var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
    arrays[(int)Mesh.ArrayType.Vertex]=cv.Data;
    var am=new ArrayMesh();am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);mesh=am;break;
   }
   default:return null;
  }
  var material=new StandardMaterial3D{AlbedoColor=color,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=true,RenderPriority=10,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
  return new MeshInstance3D{Mesh=mesh,MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
 }
 static MeshInstance3D FindCounterpart(Node start){
  Node node=start;
  for(int depth=0;node!=null&&depth<3;depth++,node=node.GetParent()){
   if(node is MeshInstance3D mesh)return mesh;
   foreach(var child in node.GetChildren())if(child is MeshInstance3D sub&&sub.Mesh!=null)return sub;
  }
  return null;
 }
 void Build(){
  Node layer=GetParent();Node root=layer!=null&&layer.GetParent()!=null?layer.GetParent():layer;
  var rows=new System.Collections.Generic.List<object>();var unmatched=new System.Collections.Generic.List<object>();
  var driveDeltas=new System.Collections.Generic.List<object>();var trims=new System.Collections.Generic.List<object>();
  int drawn=0;
  if(root!=null)foreach(var node in root.FindChildren("*","CollisionShape3D",true,false)){
   var shapeNode=(CollisionShape3D)node;var shape=shapeNode.Shape;if(shape==null)continue;
   if(shapeNode.GetParent() is not StaticBody3D body)continue;
   var aabb=WorldAabb(shape.GetDebugMesh().GetAabb(),shapeNode.GlobalTransform);
   string counterpart=null;Vector3? counterpartSize=null;float[] offset=null;
   if(body.Name=="DriveSurface"){
    MeshInstance3D nearest=null;float best=float.MaxValue;
    foreach(var candidate in root.FindChildren("*","MeshInstance3D",true,false)){
     var mesh=(MeshInstance3D)candidate;string name=mesh.Name.ToString().ToLowerInvariant();
     if(!name.StartsWith("gnd_",System.StringComparison.Ordinal)&&!name.StartsWith("road",System.StringComparison.Ordinal))continue;
     float d=WorldAabb(mesh.GetAabb(),mesh.GlobalTransform).GetCenter().DistanceTo(aabb.GetCenter());
     if(d<best){best=d;nearest=mesh;}
    }
    if(nearest!=null){var meshAabb=WorldAabb(nearest.GetAabb(),nearest.GlobalTransform);counterpart=nearest.GetPath().ToString();counterpartSize=new Vector3(meshAabb.Size.X,meshAabb.Size.Y,meshAabb.Size.Z);driveDeltas.Add(new{body=body.GetPath().ToString(),colliderXZ=new[]{aabb.Size.X,aabb.Size.Z},visualXZ=new[]{meshAabb.Size.X,meshAabb.Size.Z},centreDeltaXZ=new[]{aabb.GetCenter().X-meshAabb.GetCenter().X,aabb.GetCenter().Z-meshAabb.GetCenter().Z},distance=best});}
   }else{
    var visible=FindCounterpart(body);
    if(visible!=null){var meshAabb=WorldAabb(visible.GetAabb(),visible.GlobalTransform);counterpart=visible.GetPath().ToString();counterpartSize=new Vector3(meshAabb.Size.X,meshAabb.Size.Y,meshAabb.Size.Z);offset=new[]{aabb.GetCenter().X-meshAabb.GetCenter().X,aabb.GetCenter().Y-meshAabb.GetCenter().Y,aabb.GetCenter().Z-meshAabb.GetCenter().Z};}
    else unmatched.Add(new{body=body.GetPath().ToString(),shapeType=shape.GetType().Name,layer=body.CollisionLayer,aabb=new[]{aabb.Position.X,aabb.Position.Y,aabb.Position.Z,aabb.Size.X,aabb.Size.Y,aabb.Size.Z}});
   }
   var visual=ShapeVisual(shape,LayerColor(body.CollisionLayer));
   if(visual!=null){visual.Transform=layer is Node3D layerNode?layerNode.GlobalTransform.AffineInverse()*shapeNode.GlobalTransform:shapeNode.GlobalTransform;AddChild(visual);drawn++;}
   rows.Add(new{body=body.Name.ToString(),path=body.GetPath().ToString(),shapeType=shape.GetType().Name,layer=body.CollisionLayer,mask=body.CollisionMask,aabb=new[]{aabb.Position.X,aabb.Position.Y,aabb.Position.Z,aabb.Size.X,aabb.Size.Y,aabb.Size.Z},counterpart,counterpartSize,centreOffset=offset});
  }
  string trimPath=System.Environment.GetEnvironmentVariable("MYSIM_COLLIDER_TRIM");
  if(!string.IsNullOrEmpty(trimPath)&&System.IO.File.Exists(trimPath)){
   using var document=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(trimPath));
   if(document.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Array)throw new System.InvalidOperationException("Collider trim file must be a JSON array of {name,reason,evidence}");
   foreach(var entry in document.RootElement.EnumerateArray()){
    string name=entry.GetProperty("name").GetString(),reason=entry.GetProperty("reason").GetString(),evidence=entry.GetProperty("evidence").GetString();
    int disabled=0;
    foreach(var node in root.FindChildren("*","CollisionShape3D",true,false)){
     if(node.GetParent() is not StaticBody3D body||!body.Name.ToString().Equals(name,System.StringComparison.Ordinal))continue;
     body.CollisionLayer=0;body.CollisionMask=0;disabled++;
    }
    trims.Add(new{name,reason,evidence,disabledBodies=disabled,note=disabled>0?null:"no exact-name StaticBody found; nothing disabled"});
   }
  }
  string auditPath=System.Environment.GetEnvironmentVariable("MYSIM_COLLISION_OVERLAY_AUDIT");
  if(!string.IsNullOrEmpty(auditPath)){var folder=System.IO.Path.GetDirectoryName(auditPath);if(!string.IsNullOrEmpty(folder))System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllText(auditPath,System.Text.Json.JsonSerializer.Serialize(new{scanRoot=root?.GetPath().ToString(),colliders=rows.Count,drawn,unmatchedCount=unmatched.Count,trims,driveWidthDeltas=driveDeltas,unmatched,scope="Collision-overlay evidence: every static collider vs its visible counterpart. Offsets/oversizes are evidence for per-name trim review; nothing is removed except MYSIM_COLLIDER_TRIM named entries with supplied evidence; collision never blanket-disabled"}));}
  GD.Print("COLLISION_OVERLAY shapes=",rows.Count," drawn=",drawn," unmatched=",unmatched.Count," driveDeltas=",driveDeltas.Count," trims=",trims.Count);
 }
}
}
