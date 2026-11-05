using Godot;
using System.Collections.Generic;
using System.Globalization;
namespace MySim {
// Recovered RR3 drive-surface collision mesh (OBJ text: v/f, 1-based). Becomes the only ground the car drives on.
public static class DriveSurfaceLoader {
 public static StaticBody3D Load(string resPath, uint layer = 1) {
  var verts = new List<Vector3>(); var tris = new List<Vector3>();
  using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
  while (!f.EofReached()) {
   string l = f.GetLine(); if (l.Length < 2) continue;
   if (l[0] == 'v' && l[1] == ' ') { var p = l.Substring(2).Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries); verts.Add(new Vector3(P(p[0]), P(p[1]), -P(p[2]))); }
   else if (l[0] == 'f' && l[1] == ' ') { var p = l.Substring(2).Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries); tris.Add(verts[int.Parse(p[0]) - 1]); tris.Add(verts[int.Parse(p[1]) - 1]); tris.Add(verts[int.Parse(p[2]) - 1]); }
  }
  var shape = new ConcavePolygonShape3D { BackfaceCollision = true }; shape.Data = NativeSpinDiagonalExperiment.ApplyCandidate(MeasuredInterlagosSeam.Apply(TrackSeamWelds.Apply(tris.ToArray(),resPath),resPath),resPath);
  var body = new StaticBody3D { Name = "DriveSurface", CollisionLayer = layer }; body.AddChild(new CollisionShape3D { Shape = shape });
  if(System.Environment.GetEnvironmentVariable("MYSIM_COPLANAR_ROAD_CANDIDATE")=="1"){
   if(layer!=1)throw new System.InvalidOperationException("Coplanar road candidate only applies to actual drive-surface layer1");
   var candidate=CoplanarRoadCollisionCandidate.Build(tris.ToArray());var generated=candidate.Body;
   generated.Name=body.Name;generated.CollisionLayer=body.CollisionLayer;generated.CollisionMask=body.CollisionMask;generated.PhysicsMaterialOverride=body.PhysicsMaterialOverride;generated.ConstantLinearVelocity=body.ConstantLinearVelocity;generated.ConstantAngularVelocity=body.ConstantAngularVelocity;
   if(candidate.SlabTriangles+candidate.RetainedTriangles!=tris.Count/3||candidate.MaximumPlaneError>.0001f)throw new System.Exception("Generated road topology/support bounds failed");
   string folder="outputs/cloud-game";System.IO.Directory.CreateDirectory(folder);string file=System.IO.Path.GetFileNameWithoutExtension(resPath);string path=folder+"/road-coplanar-"+file+".json";
   System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{source=resPath,enabled=true,sourceCollisionLayer=body.CollisionLayer,sourceCollisionMask=body.CollisionMask,materialPreserved=generated.PhysicsMaterialOverride==body.PhysicsMaterialOverride,sourceTriangleCount=tris.Count/3,topology=candidate.Evidence(),scope="Explicit candidate only; source vertices/files/visuals unchanged, original material/masks preserved. Below-surface slab boundary contacts and all driving/road-coverage gates remain required."}));
   GD.Print("COPLANAR_ROAD_CANDIDATE source=",resPath," slabs=",candidate.Slabs," mergedTriangles=",candidate.SlabTriangles," retained=",candidate.RetainedTriangles," seamEdges=",candidate.SlabToRetainedEdges+candidate.SlabToSlabEdges);
   body.Free();return generated;
  }
  GD.Print("DRIVESURFACE verts=", verts.Count, " tris=", tris.Count / 3);
  return body;
 }
 static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}}
