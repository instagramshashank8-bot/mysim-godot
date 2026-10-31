using Godot;
using System;
using System.Collections.Generic;
using MySimulator.Rebuild;
namespace MySim {
public partial class RuntimeChecks : Node3D {
 int count; void Check(bool ok,string reason) { if(!ok) throw new Exception(reason); count++; }
 public override async void _Ready() {
  try {
   GeneratedWheelGeometryChecks.Run(this);
   var windingRoot=new Node3D();AddChild(windingRoot);var plane=new PlaneMesh();var good=new MeshInstance3D {Mesh=plane};windingRoot.AddChild(good);Check(WindingFixer.Fix(windingRoot,out _)==0,"healthy Godot clockwise plane was reversed");
   var planeArrays=plane.SurfaceGetArrays(0);var indices=(int[])planeArrays[(int)Mesh.ArrayType.Index];for(int i=0;i<indices.Length;i+=3){int tmp=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=tmp;}planeArrays[(int)Mesh.ArrayType.Index]=indices;var reversed=new ArrayMesh();reversed.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,planeArrays);good.Mesh=reversed;
   Check(WindingFixer.Fix(windingRoot,out _)==1,"reversed plane was not repaired");Check(WindingFixer.Fix(windingRoot,out _)==0,"winding repair is not idempotent");windingRoot.QueueFree();
   var wall=new StaticBody3D { CollisionLayer=2, CollisionMask=0 };
   wall.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new Vector3(30,4,.2f) }, Position=new Vector3(0,1,0) }); AddChild(wall);
   var beam=new StaticBody3D {CollisionLayer=2,CollisionMask=0};beam.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(3,.2f,.3f)},Position=new Vector3(-20,1.8f,-6)});AddChild(beam);
   var corner=new StaticBody3D {CollisionLayer=2,CollisionMask=0};corner.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(8,3,.2f)},Position=new Vector3(36,1,-6)});corner.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(.2f,3,8)},Position=new Vector3(38,1,-8)});AddChild(corner);
   var floor=new StaticBody3D { CollisionLayer=1, CollisionMask=0 };
   floor.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new Vector3(100,.2f,100) }, Position=new Vector3(0,-.1f,0) });AddChild(floor);
   var deck=new StaticBody3D {CollisionLayer=2,CollisionMask=0};deck.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(8,.2f,8)},Position=new Vector3(70,-.1f,-35)});deck.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(.2f,2,8)},Position=new Vector3(73.5f,1,-35)});AddChild(deck);
   var elevated=new StaticBody3D {CollisionLayer=1,CollisionMask=0};elevated.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(8,.2f,8)},Position=new Vector3(80,24.9f,35)});AddChild(elevated);
   var objects=new Node3D();AddChild(objects);
   var building=new MeshInstance3D {Name="arc_brick_test",Mesh=new BoxMesh {Size=new Vector3(3,3,3)},Position=new Vector3(20,1.5f,-6)};objects.AddChild(building);
   var road=new MeshInstance3D {Name="gnd_concrete_test",Mesh=new PlaneMesh()};objects.AddChild(road);
   var decal=new MeshInstance3D {Name="overlay_wall_scuff",Mesh=new PlaneMesh()};objects.AddChild(decal);
   var concaveRoof=new MeshInstance3D {Name="arc_concave_roof_test",Mesh=new BoxMesh {Size=new Vector3(8,.2f,8)},Position=new Vector3(120,2,-35)};objects.AddChild(concaveRoof);
   var bridgeFaces=new List<Vector3>();foreach(var spec in new[]{(size:new Vector3(8,.2f,8),at:new Vector3(0,-.1f,0)),(size:new Vector3(.2f,2,8),at:new Vector3(3.5f,1,0)),(size:new Vector3(8,.2f,8),at:new Vector3(0,2,0))})foreach(var point in new BoxMesh {Size=spec.size}.GetFaces())bridgeFaces.Add(point+spec.at);
   var bridgeMesh=new ArrayMesh();var bridgeArrays=new Godot.Collections.Array();bridgeArrays.Resize((int)Mesh.ArrayType.Max);bridgeArrays[(int)Mesh.ArrayType.Vertex]=bridgeFaces.ToArray();bridgeMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,bridgeArrays);var authoredBridge=new MeshInstance3D {Name="arc_authored_bridge_test",Mesh=bridgeMesh,Position=new Vector3(160,0,-35)};objects.AddChild(authoredBridge);
   var bridgeWalls=WallBuilder.WallFaces(authoredBridge,out var bridgeSupport);Check(bridgeWalls.Length+bridgeSupport.Length==bridgeFaces.Count,"bridge role partition discarded original triangles");Check(bridgeSupport.Length==18,"bridge upward faces are not support");
   Check(WallBuilder.IsSolid(building),"architecture lacks a solid role");Check(!WallBuilder.IsSolid(road),"road became a phantom wall");Check(!WallBuilder.IsSolid(decal),"decal became a phantom wall");Check(WallBuilder.Build(objects)==3,"physical role coverage wrong");
   for(int i=0;i<3;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
   var space=GetWorld3D().DirectSpaceState;
   foreach(float length in new[]{4.06f,5.55f}) foreach(float angle in new[]{0f,.35f,-.35f}) {
    var v=new Vehicle { heading=angle, position=new Vec(0,8),velocity=new Vec(0,80),speed=80 };
    float before=v.speed;Check(Contact.Walls(space,v,new Vec(0,-8),0,new Vector3(2.2f,1,length)),"swept wall missed");
    Check(v.position.z < -length*.4f,"car tunneled through wall");Check(v.speed<=before+.001f,"wall added energy");
   }
   foreach(float speed in new[]{0f,10f,-10f}) {
    var v=new Vehicle { position=new Vec(0,-.5f),velocity=new Vec(0,speed),speed=Math.Abs(speed) };
    Check(Contact.Walls(space,v,new Vec(0,-.5f),0,new Vector3(2,1,4)),"initial overlap ignored");Check(v.position.z < -2f,"initial overlap not cleared");Check(v.speed<=Math.Abs(speed)+.01f,"overlap added energy");
   }
   var offsetHit=new Vehicle {position=new Vec(0,8),velocity=new Vec(0,80),speed=80};Check(Contact.Walls(space,offsetHit,new Vec(0,-8),0,new Vector3(2,1,4),new Vector3(0,.5f,1)),"offset body missed wall");Check(offsetHit.position.z < -3f,"visual nose crossed wall despite collision");
   var buildingHit=new Vehicle {position=new Vec(20,0),velocity=new Vec(0,80),speed=80};Check(Contact.Walls(space,buildingHit,new Vec(20,-14),0,new Vector3(2,1,4)),"drive-through architecture");Check(buildingHit.position.z < -9f,"car entered building footprint");
   var tall=new Vehicle {position=new Vec(-20,0),velocity=new Vec(0,30),speed=30};Check(Contact.Walls(space,tall,new Vec(-20,-14),0,Contact.BodySize(new Vector3(2,1.85f,4))),"tall car roof clipped overhead beam");Check(tall.position.z<-8,"tall car passed through overhead obstruction");
   var low=new Vehicle {position=new Vec(-20,0),velocity=new Vec(0,30),speed=30};Check(!Contact.Walls(space,low,new Vec(-20,-14),0,Contact.BodySize(new Vector3(2,1.3f,4))),"short car blocked by clear overhead beam");
   var wedged=new Vehicle {position=new Vec(37.5f,-6.5f),velocity=new Vec(),speed=0};Check(Contact.Walls(space,wedged,wedged.position,0,new Vector3(2,1,4)),"corner overlap ignored");
   var clearance=new PhysicsShapeQueryParameters3D {Shape=new BoxShape3D {Size=new Vector3(2,1,4)},Transform=new Transform3D(Basis.Identity,new Vector3(wedged.position.x,.5f,wedged.position.z)),CollisionMask=2,Margin=.01f};Check(space.IntersectShape(clearance).Count==0,"initial overlap remains against second corner wall");Check((wedged.lastIntegrated-wedged.position).Magnitude<.001f,"stationary corner recovery leaves stale integration pose");
   var g=Contact.Ground(space,new Vector3(4,0,-4),0);Check(g.near&&Math.Abs(g.y)<.01f,"ground height incorrect");
   var supported=new Vehicle {position=new Vec(70,-35)};Check(!Contact.Walls(space,supported,supported.position,0,new Vector3(2,1,4)),"walkable bridge support became a side-wall collision");var parapet=new Vehicle {position=new Vec(80,-35),velocity=new Vec(40,0),speed=40};Check(Contact.Walls(space,parapet,new Vec(70,-35),0,new Vector3(2,1,4)),"bridge parapet was ignored with its deck");Check(parapet.position.x<72.5f,"car passed through bridge parapet");
   using var roofProbeShape=new BoxShape3D {Size=new Vector3(2,2.16f,4)};using var roofProbe=new PhysicsShapeQueryParameters3D {Shape=roofProbeShape,Transform=new Transform3D(Basis.Identity,new Vector3(120,1.12f,-35)),CollisionMask=2,Margin=.01f};GD.Print("CONCAVE_ROOF overlap=",space.IntersectShape(roofProbe).Count," rest=",space.GetRestInfo(roofProbe).Count," contacts=",space.CollideShape(roofProbe).Count);
   var embeddedRoof=new Vehicle {position=new Vec(120,-35)};Check(Contact.Walls(space,embeddedRoof,embeddedRoof.position,0,new Vector3(2,2.2f,4)),"actual concave imported roof overlap was ignored");
   var enteringRoof=new Vehicle {position=new Vec(120,-35),velocity=new Vec(0,30),speed=30};Check(Contact.Walls(space,enteringRoof,new Vec(120,-45),0,new Vector3(2,2.2f,4)),"actual concave imported roof sweep was ignored");Check(enteringRoof.position.z<-40,"car crossed actual concave roof edge");
   var deckGround=Contact.Ground(space,new Vector3(160,0,-35),0);Check(deckGround.near&&Math.Abs(deckGround.y)<.01f,"bridge floor query chose the overhead roof");var deckCar=new Vehicle {position=new Vec(160,-35)};Check(!Contact.Walls(space,deckCar,deckCar.position,0,new Vector3(2,1,4)),"bridge support became a side impact");
   var bridgeRail=new Vehicle {position=new Vec(170,-35),velocity=new Vec(40,0),speed=40};Check(Contact.Walls(space,bridgeRail,new Vec(160,-35),0,new Vector3(2,1,4)),"partitioned bridge parapet became permeable");Check(bridgeRail.position.x<162.5f,"car crossed bridge parapet");var bridgeTall=new Vehicle {position=new Vec(160,-35)};Check(Contact.Walls(space,bridgeTall,bridgeTall.position,0,new Vector3(2,2.2f,4)),"partitioned bridge underside became permeable");
   string bridgeBin=ProjectSettings.GlobalizePath("user://bridge-collision-regression.bin");CollisionBake.Write(objects,bridgeBin);var bakedBridge=CollisionBake.Read(bridgeBin);Check(bakedBridge.GetNodeOrNull<StaticBody3D>("BakedBridgeSupport")?.CollisionLayer==1,"baked bridge lost support layer");bakedBridge.Free();
   var seedGround=Contact.Ground(space,new Vector3(80,0,35),0,float.MaxValue);Check(seedGround.found&&!seedGround.clamped&&Math.Abs(seedGround.y-25)<.01f,"initial road height was clamped toward world zero");var seeded=Contact.Ground(space,new Vector3(80,seedGround.y,35),seedGround.y);Check(seeded.near&&!seeded.clamped,"spawn did not begin on the actual driving surface");
   var contained=new Vehicle {position=new Vec(0,-4),velocity=new Vec(10,0),speed=10,lateral=0}; Contact.MarkGood(contained,true);
   contained.position=new Vec(40,-4);contained.lateral=40;Check(Contact.Contain(contained,new Vec(39,-4),true,30),"containment missed");Check(contained.position.x==0,"containment did not restore last valid point");
   Check((contained.lastIntegrated-contained.position).Magnitude<.001f,"containment leaves stale integration pose");
   var testCircuit=new Circuit(0);var stale=new Vehicle {position=testCircuit.Point(.2f),lateral=40};Check(!Contact.Contain(stale,stale.position,true,30f,testCircuit),"stale pre-contact lateral coordinate caused false containment");Check(Math.Abs(stale.lateral)<.01f,"containment did not project corrected position");
   if (System.Environment.GetEnvironmentVariable("MYSIM_CONTACT_ONLY") != null) { GD.Print("PASS contact checks ",count," assertions"); GetTree().Quit(); return; }
   var rows=new List<object>();
   for(int ci=0;ci<CarCatalog.Count;ci++) {
    var car=CarLoader.Load(CarCatalog.Folders[ci]);AddChild(car);CarLoader.ApplyExact(car,ci);var wheels=CarLoader.AttachWheels(car,ci);
    if(ci==6){ShaderMaterial hood=null,other=null;foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){var mesh=(MeshInstance3D)node;if(mesh.Name=="LOD_A_HOOD_mm_misc")hood=mesh.GetActiveMaterial(0) as ShaderMaterial;else if(mesh.IsVisibleInTree()&&mesh.Name.ToString().EndsWith("_mm_misc")&&mesh.GetActiveMaterial(0) is ShaderMaterial candidate&&candidate.HasMeta("source_material_guid")&&candidate.GetMeta("source_material_guid").AsString()=="73c5434059a2bbe3b97e4f9115bfeba3")other=candidate;}Check(hood!=null&&hood.GetShaderParameter("uv_scale").AsVector2()==new Vector2(1,-1),"reviewed bonnet UV missing");Check(other!=null&&hood!=other,"bonnet repair mutated shared misc material");var otherScale=other.GetShaderParameter("uv_scale");Check(otherScale.VariantType==Variant.Type.Nil||otherScale.AsVector2()==new Vector2(-1,-1),"bonnet repair changed other misc panels");}
    var dims=CarLoader.MeasureSize(car);var wr=new List<object>();var partAudit=new List<object>();
    for(int wi=0;wi<4;wi++) {
     var spin=wheels[wi].GetNode<Node3D>("Spin"); var bound=new Aabb();bool first=true;
     foreach(var node in spin.GetChildren()) if(node is MeshInstance3D mesh && mesh.Visible) {
      var ab=mesh.Transform*mesh.GetAabb();bound=first?ab:bound.Merge(ab);first=false;
      var sc=mesh.Transform.Basis.Scale;Check(float.IsFinite(sc.X)&&Math.Abs(sc.X)>0,"invalid wheel transform");
     }
     GD.Print("WHEEL_GEOMETRY car=",ci," wheel=",wi," center=",bound.GetCenter()," size=",bound.Size);
     foreach(var child in spin.GetChildren())if(child is MeshInstance3D part&&part.Visible){var bounds=part.Transform*part.GetAabb();var materials=new List<object>();for(int si=0;si<part.Mesh.GetSurfaceCount();si++){var material=part.GetActiveMaterial(si);materials.Add(new {surface=si,type=material?.GetType().Name,texture=(material as StandardMaterial3D)?.AlbedoTexture?.ResourcePath});}partAudit.Add(new {wheel=wi,name=part.Name.ToString(),center=bounds.GetCenter().ToString(),size=bounds.Size.ToString(),materials});}
     wr.Add(new { wheel=wi, x=bound.Size.X,y=bound.Size.Y,z=bound.Size.Z,center=bound.GetCenter().ToString() });
     Check(Math.Abs(bound.GetCenter().X)<.08f,"wheel assembly offset from hub car "+ci);Check(!first,"missing wheel geometry car "+ci);Check(bound.Size.Y>.3f&&bound.Size.Y<1.5f,"wheel diameter invalid car "+ci);
     Check(Math.Abs(bound.Size.Y-bound.Size.Z)<.18f,"wheel radial bounds not circular car "+ci+" wheel "+wi+" bounds="+bound);
    }
    rows.Add(new {car=ci,name=CarCatalog.Names[ci],width=dims.X,height=dims.Y,length=dims.Z,wheels=wr,parts=partAudit,visibleMaterials=CarMaterialAudit.Describe(car)});car.Free();
   }
   System.IO.File.WriteAllText(System.Environment.GetEnvironmentVariable("MYSIM_GEOMETRYOUT")??"outputs/repair-20261029/geometry.json",System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
   GD.Print("PASS runtime contact and geometry ",count," assertions");GetTree().Quit();
  } catch(Exception e) { GD.PrintErr("FAIL runtime checks ",e);GetTree().Quit(1); }
 }
}}
