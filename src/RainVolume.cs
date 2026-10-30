using Godot;
namespace MySim {
// World-aligned weather follows translation only: looking around must not rotate rainfall.
public partial class RainVolume : Node3D {
 public int Amount;Node3D observer;float height;
 public override void _Ready(){
  observer=GetParent<Node3D>();TopLevel=true;var profile=Conditions.RainVisual;float V(string key)=>profile.GetProperty(key).GetSingle();height=V("spawnHeight");var wind=profile.GetProperty("wind");var direction=new Vector3(wind[0].GetSingle(),-V("fallSpeedMin"),wind[2].GetSingle()).Normalized();
  var process=new ParticleProcessMaterial {EmissionShape=ParticleProcessMaterial.EmissionShapeEnum.Box,EmissionBoxExtents=new Vector3(V("spawnHalfWidth"),.2f,V("spawnHalfWidth")),Direction=direction,Spread=2,InitialVelocityMin=V("fallSpeedMin"),InitialVelocityMax=V("fallSpeedMax"),Gravity=new Vector3(0,-6,0)};
  var tint=profile.GetProperty("tint");var material=new StandardMaterial3D {AlbedoColor=new Color(tint[0].GetSingle(),tint[1].GetSingle(),tint[2].GetSingle(),V("opacity")),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,MetallicSpecular=.1f,Roughness=.7f};
  var mesh=new BoxMesh {Size=new Vector3(V("dropWidth"),V("dropLength"),V("dropWidth")),Material=material};
  AddChild(new GpuParticles3D {Amount=Amount,Lifetime=V("lifetime"),ProcessMaterial=process,DrawPass1=mesh,LocalCoords=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityAabb=new Aabb(new Vector3(-40,-30,-40),new Vector3(80,60,80))});Follow();
 }
 void Follow(){GlobalTransform=new Transform3D(Basis.Identity,observer.GlobalPosition+Vector3.Up*height);}
 public override void _Process(double delta){if(IsInstanceValid(observer))Follow();}
}
}
