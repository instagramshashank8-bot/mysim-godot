using Godot;
using MySimulator.Rebuild;
namespace MySim {
// Compound markings are separate geometry; source tyres, rims, textures and UVs stay intact.
public static class TyreAppearance {
 public static Color Colour(Compound compound)=>compound switch {Compound.Soft=>new Color(.9f,.06f,.04f),Compound.Medium=>new Color(1f,.78f,.04f),Compound.Hard=>new Color(.92f,.93f,.94f),_=>new Color(.08f,.38f,.95f)};
 public static void Apply(Node3D[] pivots,Compound compound){
  if(pivots==null)return;
  foreach(var pivot in pivots){
   var spin=pivot.GetNode<Node3D>("Spin");Node3D markings=spin.GetNodeOrNull<Node3D>("CompoundMarkings");
   if(markings==null){
    markings=new Node3D {Name="CompoundMarkings"};spin.AddChild(markings);
    foreach(var node in spin.GetChildren())if(node is MeshInstance3D tyre&&(tyre.Name.ToString().Contains("TYRE")||tyre.Name.ToString().Contains("TIRE"))){
     var bounds=tyre.Transform*tyre.GetAabb();var center=bounds.GetCenter();float radius=Mathf.Max(bounds.Size.Y,bounds.Size.Z)*.5f;
     if(radius<.15f||radius>.6f)continue;
     foreach(float side in new[]{-1f,1f}){
      var ring=new MeshInstance3D {Name=side<0?"InnerSidewall":"OuterSidewall",Mesh=new TorusMesh {InnerRadius=radius*.805f,OuterRadius=radius*.83f,Rings=96,RingSegments=8},Position=center+Vector3.Right*(side*(bounds.Size.X*.5f+.0015f)),Rotation=new Vector3(0,0,Mathf.Pi*.5f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
      markings.AddChild(ring);
     }
     break;
    }
   }
   foreach(var child in markings.GetChildren())if(child is MeshInstance3D ring)ring.MaterialOverride=new StandardMaterial3D {AlbedoColor=Colour(compound),Roughness=.85f,MetallicSpecular=.1f};
   pivot.SetMeta("displayed_compound",(int)compound);
  }
 }
}
}
