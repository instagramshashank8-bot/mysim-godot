using Godot;
using System.Collections.Generic;
namespace MySim {
// Controlled isolation of the observed night fence/foliage speckle. Never changes source assets.
// Each mode changes one cause only; a matching baseline is required before choosing a fix.
public static class TrackCutoutDiagnostic {
 static readonly System.Text.RegularExpressions.Regex FenceRepairTextureRegex=new System.Text.RegularExpressions.Regex("^mat_fence_(wire_04|small_wire_01|metal_catch_01)\\.png$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 static readonly Dictionary<string,Texture2D> FenceRepairCache=new Dictionary<string,Texture2D>();
 static string CutoutFamilyFor(string texturePath){
  if(string.IsNullOrEmpty(texturePath))return null;
  string p=texturePath.ToLowerInvariant();string f=System.IO.Path.GetFileName(p);
  if(f.StartsWith("mat_fence_",System.StringComparison.Ordinal))return "fence";
  if(f.StartsWith("mat_canvas",System.StringComparison.Ordinal)||f.StartsWith("mat_metal_rail",System.StringComparison.Ordinal)||f.Contains("scafolding")||p.Contains("/crowd/")||(p.Contains("/overlay/")&&f.StartsWith("overlay_wall",System.StringComparison.Ordinal)))return "stands";
  if(p.Contains("/org/"))return "foliage";
  return null;
 }
 static int[] AlphaHistogram(byte[] data){var hist=new int[256];for(int i=3;i<data.Length;i+=4)hist[data[i]]++;return hist;}
 static double CdfAt(int[] hist,int value,long texels){long seen=0;for(int v=0;v<value;v++)seen+=hist[v];return (seen+hist[value]*0.5)/texels;}
 static int QuantileFromCdf(double[] cdf,double p){for(int v=0;v<256;v++)if(cdf[v]>=p)return v;return 255;}
 // Coverage-preserving mip rebuild for exactly the three reference fence wire textures.
 // Generated box-average mips drive thin wire alpha below any fixed scissor threshold, producing the
 // observed black/white speckle. Each rebuilt level keeps the level-0 alpha CDF (identical keep fraction
 // for any threshold) while box-filtering RGB, so distant wire coverage stays spatially coherent instead
 // of collapsing into speckle. Binding-time transform only: source PNG/import/geometry/UVs untouched,
 // RGB channels untouched, transparency/threshold/AA mode/cull/filter unchanged.
 static Texture2D FenceCoverageRepairMips(Texture2D texture,List<object> evidence){
  string path=texture?.ResourcePath??"";string name=System.IO.Path.GetFileName(path);
  if(!FenceRepairTextureRegex.IsMatch(name))return null;
  if(FenceRepairCache.TryGetValue(path,out Texture2D cached))return cached;
  using var imported=texture.GetImage();
  int w=imported?.GetWidth()??0,h=imported?.GetHeight()??0;
  if(w<2||w!=h||(w&(w-1))!=0){GD.Print("FENCE_MIPREPAIR_SKIPPED texture=",name," reason=non_pot_square size=",w,"x",h);return null;}
  if(imported.IsCompressed())imported.Convert(Image.Format.Rgba8);
  if(imported.GetFormat()!=Image.Format.Rgba8){GD.Print("FENCE_MIPREPAIR_SKIPPED texture=",name," reason=format_",imported.GetFormat().ToString());return null;}
  byte[] all=imported.GetData();long texels=(long)w*h;
  if(all==null||all.Length<texels*4){GD.Print("FENCE_MIPREPAIR_SKIPPED texture=",name," reason=data_size_",all?.Length??0);return null;}
  var level0=new byte[texels*4];System.Array.Copy(all,0,level0,0,texels*4);
  var hist0=AlphaHistogram(level0);var cdf0=new double[256];for(int v=0;v<256;v++)cdf0[v]=CdfAt(hist0,v,texels);
  int kept05_0=0,kept035_0=0;for(int i=3;i<level0.Length;i+=4){if(level0[i]>=127)kept05_0++;if(level0[i]>=89)kept035_0++;}
  using var scratch=Image.CreateFromData(w,h,false,Image.Format.Rgba8,level0);
  using var outStream=new System.IO.MemoryStream();outStream.Write(level0,0,level0.Length);
  var levelRows=new List<object>();int cw=w;int levelCount=1;bool anomaly=false;
  while(cw>1){
   scratch.Resize(cw/2,cw/2,Image.Interpolation.Bilinear);cw/=2;levelCount++;
   byte[] data=scratch.GetData();long ltexels=(long)cw*cw;
   var hist=AlphaHistogram(data);var lut=new int[256];
   {long seen=0;for(int v=0;v<256;v++){lut[v]=QuantileFromCdf(cdf0,(seen+hist[v]*0.5)/ltexels);seen+=hist[v];}}
   for(int i=3;i<data.Length;i+=4)data[i]=(byte)lut[data[i]];
   int kept05=0,kept035=0;for(int i=3;i<data.Length;i+=4){if(data[i]>=127)kept05++;if(data[i]>=89)kept035++;}
   // Per-level keep fractions quantize at 1/texels granularity (a 4-texel level cannot represent 22% except as 0/0.25/0.5/1),
   // so the tolerance floor 2/sqrt(texels) covers binning noise while still catching real drift (e.g. a mis-wired CDF
   // producing grossly wrong coverage). Verified against run 41843204651: levels >=256 texels deviated <=0.0122-0.0235;
   // only <=64-texel levels exceeded 0.02, all consistent with quantization, none with a broken transform.
   double tolerance=System.Math.Max(0.02,2.0/System.Math.Sqrt((double)ltexels));
   if(System.Math.Abs((double)kept05_0/texels-(double)kept05/ltexels)>tolerance)anomaly=true;
   outStream.Write(data,0,data.Length);
   levelRows.Add(new{level=levelCount-1,width=cw,texels=ltexels,keptAt05=(double)kept05/ltexels,keptAt035=(double)kept035/ltexels});
  }
  using var composed=Image.CreateFromData(w,h,true,Image.Format.Rgba8,outStream.ToArray());
  if(composed==null||!composed.HasMipmaps()){GD.PrintErr("FENCE_MIPREPAIR_COMPOSE_FAILED texture=",name);return null;}
  var repaired=ImageTexture.CreateFromImage(composed);
  FenceRepairCache[path]=repaired;
  evidence.Add(new{texture=path,name,width=w,height=h,texels,levelCount,level0KeptAt05=(double)kept05_0/texels,level0KeptAt035=(double)kept035_0/texels,levels=levelRows,anomaly,scope="Binding-time alpha-CDF-preserving mip rebuild; RGB/UV/geometry untouched; source import never written"});
  GD.Print("FENCE_MIPREPAIR texture=",name," levels=",levelCount," kept05=",((double)kept05_0/texels).ToString("0.000")," kept035=",((double)kept035_0/texels).ToString("0.000")," anomaly=",anomaly);
  if(anomaly)GD.PrintErr("FENCE_MIPREPAIR_ANOMALY texture=",name," per-level keep fraction deviates from level 0 by more than 0.02");
  return repaired;
 }
 public static void Apply(Node3D track){
  string mode=System.Environment.GetEnvironmentVariable("MYSIM_TRACK_CUTOUT_DIAGNOSTIC");
  if(string.IsNullOrEmpty(mode))return;
  if(mode!="baseline"&&mode!="coverage_off"&&mode!="shadows_off"&&mode!="unshaded"&&mode!="specular_off"&&mode!="opaque_metal_candidate"&&mode!="filtered_metal_candidate"&&mode!="identity"&&mode!="fence_blend_candidate"&&mode!="fence_a2c_candidate"&&mode!="family_isolate_fence"&&mode!="family_isolate_stands"&&mode!="family_isolate_foliage"&&mode!="fence_miprepair_candidate")throw new System.ArgumentException("Unknown track cutout diagnostic: "+mode);
  var rows=new List<object>();int isolatedCount=0,mipRepairedCount=0;var mipRepairEvidence=new List<object>();
  if(mode=="filtered_metal_candidate"&&System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string evidence){
   System.IO.Directory.CreateDirectory(evidence);
   var texture=GD.Load<Texture2D>("res://assets/RealRacing/RecoveredNativeTextures/common/mat/metal/mat_metal_scafolding_d.png");
   using var original=texture.GetImage();using var alpha=Image.CreateEmpty(original.GetWidth(),original.GetHeight(),false,Image.Format.Rgba8);
   var histogram=new int[256];
   for(int y=0;y<original.GetHeight();y++)for(int x=0;x<original.GetWidth();x++){
    float a=original.GetPixel(x,y).A;histogram[Mathf.Clamp(Mathf.RoundToInt(a*255),0,255)]++;alpha.SetPixel(x,y,new Color(a,a,a,1));
   }
   alpha.SavePng(System.IO.Path.Combine(evidence,"grandstand-scaffold-d-source-alpha.png"));
   System.IO.File.WriteAllText(System.IO.Path.Combine(evidence,"grandstand-scaffold-d-source-alpha.json"),System.Text.Json.JsonSerializer.Serialize(new{width=original.GetWidth(),height=original.GetHeight(),histogram,scope="Actual imported source alpha channel; no source mutation or inferred authoring semantics"}));
  }
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null||!mesh.IsVisibleInTree())continue;
   bool cutout=false;
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
    if(mesh.GetActiveMaterial(surface) is not StandardMaterial3D source||source.Transparency!=BaseMaterial3D.TransparencyEnum.AlphaScissor)continue;
    cutout=true;var material=source;
    string textureName=System.IO.Path.GetFileName(source.AlbedoTexture?.ResourcePath??"");bool opaqueMetal=mode=="opaque_metal_candidate"&&(textureName=="mat_fence_metal_catch_01.png"||textureName=="mat_metal_scafolding_g.png");
    bool filteredMetal=mode=="filtered_metal_candidate"&&textureName=="mat_metal_scafolding_d.png";
    bool blendFence=mode=="fence_blend_candidate"&&(textureName=="mat_fence_wire_04.png"||textureName=="mat_fence_small_wire_01.png"||textureName=="mat_fence_metal_catch_01.png");
    bool a2cFence=mode=="fence_a2c_candidate"&&(textureName=="mat_fence_wire_04.png"||textureName=="mat_fence_small_wire_01.png"||textureName=="mat_fence_metal_catch_01.png");
    string cutoutFamily=CutoutFamilyFor(source.AlbedoTexture?.ResourcePath);
    bool familyIsolate=cutoutFamily!=null&&mode=="family_isolate_"+cutoutFamily;
    bool mipRepairFence=mode=="fence_miprepair_candidate"&&FenceRepairTextureRegex.IsMatch(textureName);
    bool mipRepaired=false;
    if(mode=="identity"||mode=="coverage_off"||mode=="unshaded"||mode=="specular_off"||opaqueMetal||filteredMetal||blendFence||a2cFence||familyIsolate||mipRepairFence){
     material=(StandardMaterial3D)source.Duplicate();
     if(mode=="identity"){string path=source.AlbedoTexture?.ResourcePath??"";material.Transparency=BaseMaterial3D.TransparencyEnum.Disabled;material.AlbedoTexture=null;material.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;material.AlbedoColor=path.Contains("/crowd/")?Colors.Red:path.Contains("/org/")?Colors.Green:path.Contains("/fence/")?Colors.Blue:path.Contains("scafolding")?Colors.Yellow:Colors.Magenta;}
     if(opaqueMetal)material.Transparency=BaseMaterial3D.TransparencyEnum.Disabled;
     // Retain filtered fractional source alpha instead of binary thresholding mip levels.
     // Exact reference-track wire textures only: preserve holes, geometry, UVs and source colour.
     if(filteredMetal)material.Transparency=BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass;
     // True fractional blending: unlike depth prepass, no binary alpha depth write.
     // Diagnostic only; inspect ordering and holes before any production adoption.
     if(blendFence){material.Transparency=BaseMaterial3D.TransparencyEnum.Alpha;material.AlphaAntialiasingMode=BaseMaterial3D.AlphaAntiAliasing.Off;}
     // Alpha-to-coverage keeps scissor depth writes (no order/holes): per-sample coverage dither over 4x MSAA replaces hard mip-threshold flicker. NOT alpha hashing.
     if(a2cFence){material.Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor;material.AlphaAntialiasingMode=BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverageAndToOne;}
     // Family isolation: every cutout surface of one family renders zero fragments (alpha 0 under scissor discards all, shadows included); all other scene content untouched. Attribution evidence only.
     if(familyIsolate){material.AlbedoTexture=null;material.AlbedoColor=new Color(1f,1f,1f,0f);isolatedCount++;}
     // One fix candidate: bind the alpha-coverage-preserving rebuilt mip chain on the three named fence textures only.
     if(mipRepairFence&&source.AlbedoTexture!=null){var repaired=FenceCoverageRepairMips(source.AlbedoTexture,mipRepairEvidence);if(repaired!=null){material.AlbedoTexture=repaired;mipRepaired=true;mipRepairedCount++;}}
     if(mode=="coverage_off")material.AlphaAntialiasingMode=BaseMaterial3D.AlphaAntiAliasing.Off;
     if(mode=="specular_off"){material.Metallic=0;material.MetallicSpecular=0;material.ClearcoatEnabled=false;}
     if(mode=="unshaded")material.ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded;
     mesh.SetSurfaceOverrideMaterial(surface,material);
    }
    rows.Add(new{renderer=mesh.GetPath().ToString(),surface,worldOrigin=new[]{mesh.GlobalPosition.X,mesh.GlobalPosition.Y,mesh.GlobalPosition.Z},boundsSize=new[]{mesh.GetAabb().Size.X,mesh.GetAabb().Size.Y,mesh.GetAabb().Size.Z},texture=source.AlbedoTexture?.ResourcePath,sourceTransparency=source.Transparency.ToString(),actualTransparency=material.Transparency.ToString(),opaqueMetal,sourceAlphaAA=source.AlphaAntialiasingMode.ToString(),actualAlphaAA=material.AlphaAntialiasingMode.ToString(),threshold=material.AlphaScissorThreshold,edge=material.AlphaAntialiasingEdge,filter=material.TextureFilter.ToString(),cull=material.CullMode.ToString(),uvScale=new[]{material.Uv1Scale.X,material.Uv1Scale.Y},uvOffset=new[]{material.Uv1Offset.X,material.Uv1Offset.Y},sourceShadow=mesh.CastShadow.ToString(),actualShadow=mode=="shadows_off"?GeometryInstance3D.ShadowCastingSetting.Off.ToString():mesh.CastShadow.ToString(),sourceMetallic=source.Metallic,actualMetallic=material.Metallic,sourceSpecular=source.MetallicSpecular,actualSpecular=material.MetallicSpecular,actualClearcoat=material.ClearcoatEnabled,emissionEnabled=material.EmissionEnabled,shading=material.ShadingMode.ToString(),cutoutFamily,familyIsolated=familyIsolate,mipRepaired});
   }
   if(cutout&&mode=="shadows_off")mesh.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
  }
  GD.Print("TRACK_CUTOUT_DIAGNOSTIC mode=",mode," surfaces=",rows.Count," isolated=",isolatedCount," mipRepaired=",mipRepairedCount);
  if(System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string dir){System.IO.Directory.CreateDirectory(dir);string name=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"cutout-"+name+"-"+mode+".json"),System.Text.Json.JsonSerializer.Serialize(new{mode,layer=track.Name.ToString(),surfaces=rows,isolatedCount,mipRepairedCount,mipRepair=mipRepairEvidence.Count>0?mipRepairEvidence:null,scope="One-variable diagnostic; no geometry/UV/source-texture/collision changes; not a promoted rendering fix"}));}
 }
}
}
