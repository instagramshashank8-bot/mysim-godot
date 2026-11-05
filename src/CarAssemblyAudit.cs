using Godot;
using System;
using System.Collections.Generic;
namespace MySim {
// Read-only assembly evidence; never hides, moves, rewrites UVs or replaces a source part.
public static class CarAssemblyAudit {
 static object UVStats(Godot.Collections.Array arrays,Mesh.ArrayType channel){
  var value=arrays[(int)channel];if(value.VariantType==Variant.Type.Nil)return new {present=false,count=0};
  var points=value.AsVector2Array();int invalid=0;var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
  foreach(var point in points){if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)){invalid++;continue;}minimum=minimum.Min(point);maximum=maximum.Max(point);}
  return new {present=points.Length>0,count=points.Length,invalid,minimum=points.Length>invalid?new[]{minimum.X,minimum.Y}:null,maximum=points.Length>invalid?new[]{maximum.X,maximum.Y}:null};
 }
 class SurfaceTag{public string Name{get;set;}="";public int Surface{get;set;}public string Family{get;set;}="";public string Tag{get;set;}="";public object Report{get;set;}}
 // Mirrored-text detector: measures, per rendered text-bearing surface, whether the COMPOSED
 // mesh-UV + material UV transform mirrors or flips text as seen from the surface's outward side,
 // and whether left/right family members disagree (mirrored-instance signature). Read-only
 // geometry/UV arithmetic: changes nothing rendered; console tags are META-suffixed so they can
 // never be confused with pixel-verified results. Mirror test per primitive: sign(cross(screenDelta))
 // * sign(cross(textureDelta)); X-flip test: the screen image of the transformed UV X axis.
 static SurfaceTag TextTag(string name,int surface,Transform3D inCar,Vector3 reference,Vector3[] points,int[] indices,Vector2[] uv,Vector2 scale,Vector2 offset,bool textured){
  var tag=new SurfaceTag{Name=name,Surface=surface,Family=name.Replace("_LEFT","").Replace("_RIGHT","")};
  if(!textured){tag.Tag="no_texture";return tag;}
  if(uv==null||uv.Length==0){tag.Tag="no_uv";return tag;}
  if(points==null||points.Length==0){tag.Tag="no_geometry";return tag;}
  if(uv.Length!=points.Length)throw new InvalidOperationException("Mirrored-text tag needs matching position/UV counts: "+name);
  var centre=Vector3.Zero;foreach(var point in points)centre+=inCar*point;centre/=points.Length;
  var radial=centre-reference;if(radial.LengthSquared()<1e-10f)radial=new Vector3(0,1,0);else radial=radial.Normalized();
  double weightTotal=0,weightFlipped=0,weightXFlip=0;int evaluated=0,primitives=0;
  int limit=indices!=null&&indices.Length>0?indices.Length:points.Length;
  for(int t=0;t+2<limit;t+=3){
   int a=indices!=null&&indices.Length>0?indices[t]:t,b=indices!=null&&indices.Length>0?indices[t+1]:t+1,c=indices!=null&&indices.Length>0?indices[t+2]:t+2;
   var p0=inCar*points[a];var p1=inCar*points[b];var p2=inCar*points[c];
   var normal=(p1-p0).Cross(p2-p0);if(normal.LengthSquared()<1e-16f)continue;primitives++;
   var outward=normal.Normalized();if(outward.Dot(radial)<0)outward=-outward;
   var forward=-outward;
   var upHint=Mathf.Abs(forward.Y)>.9f?new Vector3(0,0,1):new Vector3(0,1,0);
   var right=forward.Cross(upHint);if(right.LengthSquared()<1e-8f)continue;right=right.Normalized();
   var upScreen=right.Cross(forward).Normalized();
   var q0=new Vector2(p0.Dot(right),p0.Dot(upScreen));var q1=new Vector2(p1.Dot(right),p1.Dot(upScreen));var q2=new Vector2(p2.Dot(right),p2.Dot(upScreen));
   var r0=uv[a]*scale+offset;var r1=uv[b]*scale+offset;var r2=uv[c]*scale+offset;
   var sA=q1-q0;var sB=q2-q0;var tA=r1-r0;var tB=r2-r0;
   float screenCross=sA.Cross(sB);float textureCross=tA.Cross(tB);
   if(Mathf.Abs(screenCross)<1e-12f||Mathf.Abs(textureCross)<1e-12f)continue;
   var uScreen=(tB.Y*sA-tA.Y*sB)/textureCross;float area=Mathf.Abs(screenCross);
   weightTotal+=area;if(screenCross*textureCross<0)weightFlipped+=area;if(uScreen.X<0)weightXFlip+=area;evaluated++;
  }
  double flipFraction=weightTotal>0?weightFlipped/weightTotal:0,xFraction=weightTotal>0?weightXFlip/weightTotal:0;
  if(evaluated==0){tag.Tag="degenerate_uv";tag.Report=new {verdict=tag.Tag,primitives,evaluated,scope="No evaluable UV primitive on this surface; no text-orientation evidence"};return tag;}
  string verdict=xFraction>.6?(flipFraction>.6?"mirrored_text_risk":"rotated_text_risk"):(flipFraction>.6?"flipped_text_risk":(flipFraction<.4&&xFraction<.4?"ok":"mixed_risk"));
  tag.Tag=verdict;
  tag.Report=new {verdict,primitives,evaluated,flipFraction=Math.Round(flipFraction,3),xFlipFraction=Math.Round(xFraction,3),uvScale=new[]{scale.X,scale.Y},uvOffset=new[]{offset.X,offset.Y},scope="Composite mesh-UV + material UV transform chirality seen from the surface outward side (upright viewer basis); numeric risk tag only, no texture/UV/material change; visible-text confirmation stays with paired renders"};
  return tag;
 }
 static bool IsRiskTag(string tag)=>tag=="mirrored_text_risk"||tag=="flipped_text_risk"||tag=="rotated_text_risk";
 public static void Write(Node3D car,int index,string path){
  RecoveredComponentMounts.VerifyRepeatApply(car,index);
  CarUvTopologyEvidence.Write(car,index,path);
  var rows=new List<object>();var tags=new List<SurfaceTag>();var children=car.FindChildren("*","MeshInstance3D",true,false);
  var carCentre=CarLoader.MeasureBounds(car).GetCenter();
  foreach(var child in children){var mesh=(MeshInstance3D)child;if(mesh.Mesh==null)continue;var inCar=car.GlobalTransform.AffineInverse()*mesh.GlobalTransform;var box=inCar*mesh.GetAabb();var materials=new List<object>();
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){using var arrays=mesh.Mesh.SurfaceGetArrays(surface);var material=mesh.GetActiveMaterial(surface);var shader=material as ShaderMaterial;var standard=material as StandardMaterial3D;var texture=shader!=null?shader.GetShaderParameter("base").As<Texture2D>():standard?.AlbedoTexture;
    // Effective UV transform: an unbound CarShader parameter means the authored default (-1,-1)/(1,1),
    // never (0,0) (getter ambiguity documented in FORMULA16_REAR_SOURCE_FOLLOWUP.json).
    var effectiveScale=new Vector2(-1,-1);var effectiveOffset=new Vector2(1,1);
    if(shader!=null){var storedScale=shader.GetShaderParameter("uv_scale");if(storedScale.VariantType!=Variant.Type.Nil)effectiveScale=storedScale.AsVector2();var storedOffset=shader.GetShaderParameter("uv_offset");if(storedOffset.VariantType!=Variant.Type.Nil)effectiveOffset=storedOffset.AsVector2();}
    else if(standard!=null){effectiveScale=new Vector2(standard.Uv1Scale.X,standard.Uv1Scale.Y);effectiveOffset=new Vector2(standard.Uv1Offset.X,standard.Uv1Offset.Y);}
    var pointArray=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();var indexArray=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.Nil?null:arrays[(int)Mesh.ArrayType.Index].AsInt32Array();var uvArray=arrays[(int)Mesh.ArrayType.TexUV].VariantType==Variant.Type.Nil?null:arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
    var tag=TextTag(mesh.Name.ToString(),surface,inCar,carCentre,pointArray,indexArray,uvArray,effectiveScale,effectiveOffset,texture!=null);tags.Add(tag);
    materials.Add(new {surface,uv0=UVStats(arrays,Mesh.ArrayType.TexUV),uv2=UVStats(arrays,Mesh.ArrayType.TexUV2),uvScale=shader?.GetShaderParameter("uv_scale").AsVector2().ToString(),uvOffset=shader?.GetShaderParameter("uv_offset").AsVector2().ToString(),textOrientation=tag.Report,source=material!=null&&material.HasMeta("source_material_guid")?material.GetMeta("source_material_guid").AsString():null,texture=texture?.ResourcePath,alphaMode=shader?.GetShaderParameter("alpha_mode").AsDouble(),materialType=material?.GetType().Name});}
   var blendShapes=new List<object>();if(mesh.Mesh is ArrayMesh am)for(int i=0;i<am.GetBlendShapeCount();i++)blendShapes.Add(new {name=am.GetBlendShapeName(i).ToString(),value=mesh.GetBlendShapeValue(i)});
   rows.Add(new {name=mesh.Name.ToString(),path=car.GetPathTo(mesh).ToString(),visible=mesh.IsVisibleInTree(),basisDeterminant=inCar.Basis.Determinant(),miscTwinHidden=mesh.HasMeta("misc_twin_hidden"),localPosition=new[]{mesh.Position.X,mesh.Position.Y,mesh.Position.Z},centre=new[]{box.GetCenter().X,box.GetCenter().Y,box.GetCenter().Z},size=new[]{box.Size.X,box.Size.Y,box.Size.Z},materials,blendShapes});
  }children.Clear();
  foreach(var tag in tags)if(IsRiskTag(tag.Tag))GD.Print("MIRRORED_TEXT_RISK ",tag.Name," s",tag.Surface," ",tag.Tag);
  var pairFamilies=new Dictionary<string,List<SurfaceTag>>();foreach(var tag in tags){if(!tag.Name.Contains("_LEFT")&&!tag.Name.Contains("_RIGHT"))continue;if(!pairFamilies.TryGetValue(tag.Family,out var members)){members=new List<SurfaceTag>();pairFamilies[tag.Family]=members;}members.Add(tag);}
  int pairAsymmetries=0;
  foreach(var pair in pairFamilies){
   SurfaceTag left=null,right=null;foreach(var member in pair.Value){if(member.Name.Contains("_LEFT"))left=member;else if(member.Name.Contains("_RIGHT"))right=member;}
   if(left==null||right==null)continue;
   if(IsRiskTag(left.Tag)!=IsRiskTag(right.Tag)){GD.Print("MIRRORED_TEXT_PAIR_RISK ",pair.Key," left=",left.Name,":",left.Tag," right=",right.Name,":",right.Tag);pairAsymmetries++;}
  }
  int mirroredLike=0;foreach(var tag in tags)if(IsRiskTag(tag.Tag))mirroredLike++;
  GD.Print("MIRRORED_TEXT_RISK_SUMMARY car=",index," surfaces=",tags.Count," orientationRisk=",mirroredLike," pairAsymmetries=",pairAsymmetries," numeric risk tags only; visual confirmation stays with paired renders");
  System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {car=index,scope="Read-only imported assembly and material evidence; not geometry acceptance",rows,mirroredTextTags=tags,visibleMaterials=CarMaterialAudit.Describe(car)},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));GD.Print("PASS read-only car assembly evidence ",rows.Count);
 }
}
}
