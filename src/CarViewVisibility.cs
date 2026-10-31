using Godot;
using MySimulator.Rebuild;
namespace MySim {
public static class CarViewVisibility {
 public static bool WheelsVisible(int car,bool cockpit)=>!cockpit||CarCatalog.RaceClass(car)==1;
 public static void Apply(Node3D car,Node3D[] wheels,int index,bool cockpit){var nodes=car.FindChildren("*","MeshInstance3D",true,false);foreach(var node in nodes){var mesh=(MeshInstance3D)node;if(!mesh.HasMeta("grp"))continue;string group=mesh.GetMeta("grp").AsString();mesh.Visible=!mesh.HasMeta("wheel_hidden")&&(group=="int"?cockpit:WheelsVisible(index,cockpit));}nodes.Clear();SetWheels(wheels,index,cockpit);}
 // Pit service updates whole pivot visibility each tick, so camera policy must be applied after service.
 public static void SetWheels(Node3D[] wheels,int index,bool cockpit){if(wheels==null)return;foreach(var wheel in wheels)wheel.Visible=WheelsVisible(index,cockpit);}
}
}
