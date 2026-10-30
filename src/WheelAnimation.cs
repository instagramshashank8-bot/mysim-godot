using Godot;
namespace MySim {
public static class WheelAnimation {
 // Preserve the side-dependent authored assembly orientation. Spin about world +X for forward +Z rolling.
 public static void Update(Node3D[] pivots,float distance,float steering){if(pivots==null)return;for(int i=0;i<pivots.Length;i++){
  var pivot=pivots[i];if(!pivot.HasMeta("steer_base_yaw"))throw new System.InvalidOperationException("Wheel assembly lacks authored base orientation");
  float baseYaw=pivot.GetMeta("steer_base_yaw").AsSingle(),radius=pivot.HasMeta("radius")?pivot.GetMeta("radius").AsSingle():.34f;
  var spin=pivot.GetNode<Node3D>("Spin");spin.Rotation=new Vector3(distance*Mathf.Cos(baseYaw)/radius,0,0);
  pivot.Rotation=new Vector3(0,baseYaw+(i<2?steering:0),0);
 }}
}
}
