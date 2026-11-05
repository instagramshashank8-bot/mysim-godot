using Godot;
namespace MySim {
// World-aligned weather follows translation only: looking around must not rotate rainfall.
public partial class RainVolume : Node3D {
 public int Amount;Node3D observer;float height;
 public override void _Ready(){
  observer=GetParent<Node3D>();TopLevel=true;var profile=Conditions.RainVisual;float V(string key)=>profile.GetProperty(key).GetSingle();height=V("spawnHeight");var wind=profile.GetProperty("wind");var direction=new Vector3(wind[0].GetSingle(),-V("fallSpeedMin"),wind[2].GetSingle()).Normalized();
  // One controlled rain-readability candidate: profile selector (MYSIM_RAIN_VOLUME_PROFILE). The authored drops are
  // 4 mm x 120 mm BOXES at opacity 0.22 - sub-pixel at driving distances, which is the measured "unreadable rain"
  // cause at this layer. "readable" widens/lengthens the drop box and raises opacity so individual drops cross the
  // pixel threshold at 10-40 m; particle simulation (amount/fall/wind) stays the flow's. Default unset = "base" =
  // exact current behaviour. The billboard-shader readability fix exists separately in RainVisibilityCandidate.
  string profileName=System.Environment.GetEnvironmentVariable("MYSIM_RAIN_VOLUME_PROFILE")??"base";
  float dropW=V("dropWidth"),dropL=V("dropLength"),vis=V("opacity"),lifeAdd=0f;
  switch(profileName){
   case "readable": dropW=Mathf.Max(dropW,0.012f);dropL=Mathf.Max(dropL,0.30f);vis=Mathf.Clamp(vis*1.36f,0f,.5f);lifeAdd=.3f;break;
   case "base":break;
   default:GD.Print("RAIN_VOLUME unknown profile: ",profileName," (base|readable)");break;
  }
  var process=new ParticleProcessMaterial {EmissionShape=ParticleProcessMaterial.EmissionShapeEnum.Box,EmissionBoxExtents=new Vector3(V("spawnHalfWidth"),.2f,V("spawnHalfWidth")),Direction=direction,Spread=2,InitialVelocityMin=V("fallSpeedMin"),InitialVelocityMax=V("fallSpeedMax"),Gravity=new Vector3(0,-6,0)};
  var tint=profile.GetProperty("tint");var material=new StandardMaterial3D {AlbedoColor=new Color(tint[0].GetSingle(),tint[1].GetSingle(),tint[2].GetSingle(),vis),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,MetallicSpecular=.1f,Roughness=.7f};
  var mesh=new BoxMesh {Size=new Vector3(dropW,dropL,dropW),Material=material};
  AddChild(new GpuParticles3D {Amount=Amount,Lifetime=V("lifetime")+lifeAdd,ProcessMaterial=process,DrawPass1=mesh,LocalCoords=false,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,VisibilityAabb=new Aabb(new Vector3(-40,-30,-40),new Vector3(80,60,80))});Follow();GD.Print("RAIN_VOLUME_READY profile=",Conditions.Wet," particles=",Amount," dropProfile=",profileName," drop=",(dropW*1000).ToString("0.0"),"x",(dropL*1000).ToString("0"),"mm opacity=",vis.ToString("0.00")," worldAligned=true wallMs=",Time.GetTicksMsec());
 }
 void Follow(){GlobalTransform=new Transform3D(Basis.Identity,observer.GlobalPosition+Vector3.Up*height);}
 public override void _Process(double delta){if(IsInstanceValid(observer))Follow();}
}
}
