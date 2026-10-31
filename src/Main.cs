using Godot;
using MySimulator.Rebuild;
namespace MySim {
// Game shell: menu (track/car/laps) -> race -> results -> restart/menu. Env MYSIM_TRACK skips the menu for automated tests.
public partial class Main : Node3D {
 static readonly float ColorContrast = float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_CONTRAST") ?? "1.14", System.Globalization.CultureInfo.InvariantCulture), ColorSat = float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SAT") ?? "1.1", System.Globalization.CultureInfo.InvariantCulture); Node3D world; CanvasLayer ui; DriveController drive; int lastTrack, lastCar, lastLaps = 3; Button menuStart; bool menuGoWas; float menuAge; bool resultsShown, escWas; float finShown, resultsArmed;
 public override void _Ready() {
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_SUSPENSION_CHECKS")!=null){AddChild(new NativeSuspensionScenario());return;}
  if (System.Environment.GetEnvironmentVariable("MYSIM_RUNTIME_CHECKS") != null) { AddChild(new RuntimeChecks()); return; }
  if (System.Environment.GetEnvironmentVariable("MYSIM_FLOWCHECKS") != null) AddChild(new RaceFlowScenario { GetDrive=()=>drive, ResultsVisible=()=>resultsShown, MenuVisible=()=>ui!=null&&drive==null&&!resultsShown, Restart=()=>StartRace(lastTrack,lastCar,lastLaps), Menu=ShowMenu });
  var win = GetWindow(); win.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems; win.ContentScaleSize = new Vector2I(1280, 720); win.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
  var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Sky, AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.62f, 0.66f, 0.72f), AmbientLightEnergy = 1.0f, TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1.0f, TonemapWhite = 6f, FogEnabled = true, FogLightColor = new Color(0.63f, 0.70f, 0.76f), FogDensity = 0.00025f, FogSkyAffect = 0.4f, GlowEnabled = Settings.Get("bloom",false) && System.Environment.GetEnvironmentVariable("MYSIM_NOGLOW") == null, GlowIntensity = 0.5f, GlowStrength = 0.9f, GlowBloom = 0.04f, GlowHdrThreshold = 1.1f, AdjustmentEnabled = true, AdjustmentContrast = ColorContrast, AdjustmentSaturation = ColorSat };
  var skyMat = new ProceduralSkyMaterial(); env.Sky = new Sky { SkyMaterial = skyMat }; AddChild(new WorldEnvironment { Environment = env });
  var sunLight = new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), LightEnergy = 1.7f, ShadowEnabled = true,
   DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits, DirectionalShadowMaxDistance = 150, DirectionalShadowBlendSplits = true, ShadowBias = 0.03f, ShadowNormalBias = float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SHADOW_NORMAL_BIAS")??"1.0",System.Globalization.CultureInfo.InvariantCulture), ShadowBlur = 1.0f }; if (System.Environment.GetEnvironmentVariable("MYSIM_NOSHADOW") != null) sunLight.ShadowEnabled = false; AddChild(sunLight); Conditions.Apply(env, sunLight, skyMat);
  if (System.Environment.GetEnvironmentVariable("MYSIM_LODAUDIT") != null) { foreach (int lci in new[] { 8, 7, 15 }) { string dir = "res://assets/RealRacing/Source/" + CarCatalog.Folders[lci] + "/"; var tally = new System.Collections.Generic.SortedDictionary<string, long[]>(); foreach (var part in DirAccess.GetFilesAt(dir)) { if (!part.EndsWith(".fbx")) continue; var inst = GD.Load<PackedScene>(dir + part).Instantiate<Node3D>(); foreach (var n in inst.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; string nm = mi.Name.ToString(); string key = nm.StartsWith("LOD_") ? nm.Substring(0, 5) : "other:" + nm.Split('_')[0]; long verts = 0; for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) verts += ((Vector3[])mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex]).Length; if (!tally.ContainsKey(key)) tally[key] = new long[2]; tally[key][0]++; tally[key][1] += verts; } inst.QueueFree(); } foreach (var kv in tally) GD.Print("LODAUDIT car=", lci, " group=", kv.Key, " meshes=", kv.Value[0], " vertices=", kv.Value[1]); } GetTree().Quit(); return; }
  if (System.Environment.GetEnvironmentVariable("MYSIM_AUDITCARS") != null) { for (int ci = 0; ci < CarCatalog.Count; ci++) { var cn = CarLoader.Load(CarCatalog.Folders[ci]); AddChild(cn); Aabb bb = new Aabb(); bool first = true; foreach (var n in cn.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; if (!mi.Visible) continue; var b2 = mi.GlobalTransform * mi.GetAabb(); if (first) { bb = b2; first = false; } else bb = bb.Merge(b2); } if (ci == int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_AUDITMESH") ?? "-1")) foreach (var n in cn.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; if (!mi.Visible) continue; var b3 = mi.GlobalTransform * mi.GetAabb(); GD.Print("AUDITMESH ", mi.Name, " c=", (b3.Position + b3.Size / 2).ToString("0.0"), " s=", b3.Size.ToString("0.0")); }
   GD.Print("CARAUDIT ", ci, " ", CarCatalog.Folders[ci], " L=", bb.Size.Z.ToString("0.00"), " W=", bb.Size.X.ToString("0.00"), " H=", bb.Size.Y.ToString("0.00"), " wheelbase=", RecoveredGeometry.Wheelbase[ci].ToString("0.00"), " mass=", RecoveredVehicleProfiles.Mass[ci].ToString("0")); cn.QueueFree(); } GetTree().Quit(); return; }
  var cv = System.Environment.GetEnvironmentVariable("MYSIM_CARVIEW"); if (cv != null) { CarView(int.Parse(cv)); return; }
  Settings.ApplyAudio(); lastTrack = Settings.Get("track", 0); lastCar = Settings.Get("car", 0); lastLaps = Settings.Get("laps", 3);
  var bakeT = System.Environment.GetEnvironmentVariable("MYSIM_BAKECOL"); if (bakeT != null) { BakeCollision(int.Parse(bakeT)); return; }
  var crashT = System.Environment.GetEnvironmentVariable("MYSIM_CRASHMAP"); if (crashT != null) { CrashWorld(int.Parse(crashT)); return; }
  if(System.Environment.GetEnvironmentVariable("MYSIM_TOUCHCHECKS")!=null){AddChild(new TouchControlsScenario());return;}
  if(System.Environment.GetEnvironmentVariable("MYSIM_NAVCHECKS")!=null){AddChild(new NavigationScenario());return;}
  if(System.Environment.GetEnvironmentVariable("MYSIM_SETTINGSCHECKS")!=null){AddChild(new SettingsScenario());return;}
  if(System.Environment.GetEnvironmentVariable("MYSIM_CONTROLCHECKS")!=null){AddChild(new ControlsScenario());return;}
  var t = System.Environment.GetEnvironmentVariable("MYSIM_TRACK");
  if (t != null) StartRace(int.Parse(t), int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_CAR") ?? "8"), int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_LAPS") ?? "3")); else { ShowMenu(); var ms = System.Environment.GetEnvironmentVariable("MYSIM_MENUSHOT"); if (ms != null) MenuShot(ms); }
 }
 async void MenuShot(string path) { for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); GetViewport().GetTexture().GetImage().SavePng(path); GetTree().Quit(); }
 void Ui(string n) { var st = GD.Load<AudioStream>("res://assets/Resources/RecoveredGUI/Audio/" + n + ".wav"); if (st == null) return; var pl = new AudioStreamPlayer { Stream = st, VolumeDb = -6f }; AddChild(pl); pl.Finished += () => pl.QueueFree(); pl.Play(); }
 void FitCarView(Camera3D camera,Node3D car,Vector3? targetPoint=null,string subject="car") {
   var points=new System.Collections.Generic.List<Vector3>();
   foreach(var n in car.FindChildren("*","MeshInstance3D",true,false)){var mesh=(MeshInstance3D)n;if(!mesh.IsVisibleInTree()||mesh.Mesh==null)continue;var bounds=mesh.GetAabb();for(int corner=0;corner<8;corner++)points.Add(mesh.GlobalTransform*bounds.GetEndpoint(corner));}
   if(points.Count==0)throw new System.InvalidOperationException("Car review has no visible geometry");
   var size=GetViewport().GetVisibleRect().Size;var target=targetPoint??new Vector3(0,.6f,0);
   for(int step=0;step<60;step++){bool fits=true;foreach(var point in points){var screen=camera.UnprojectPosition(point);if(camera.IsPositionBehind(point)||screen.X<size.X*.05f||screen.X>size.X*.95f||screen.Y<size.Y*.05f||screen.Y>size.Y*.95f){fits=false;break;}}if(fits){GD.Print("PASS complete visible ",subject," framing, corners=",points.Count);return;}camera.Position=target+(camera.Position-target)*1.08f;camera.LookAt(target);}
   throw new System.InvalidOperationException("Could not frame all visible car geometry");
  }
  async void CarView(int ci) {
  var floor = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(60, 60) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.32f) } }; AddChild(floor);
  var car = CarLoader.Load(CarCatalog.Folders[ci]); AddChild(car); if (System.Environment.GetEnvironmentVariable("MYSIM_UVAUDIT") != null) { foreach (var n in car.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; string nm = mi.Name.ToString(); if (!(nm.StartsWith("LOD_A_BODY") || nm.StartsWith("LOD_A_DOOR_LEFT_mm") || nm.StartsWith("LOD_A_HOOD_mm") || nm.StartsWith("LOD_A_CHASSIS"))) continue; for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) { var arr = mi.Mesh.SurfaceGetArrays(s); var u0 = (Vector2[])arr[(int)Mesh.ArrayType.TexUV]; var u1 = (Vector2[])arr[(int)Mesh.ArrayType.TexUV2]; string D(Vector2[] u) { if (u == null || u.Length == 0) return "none"; float x0 = 9e9f, y0 = 9e9f, x1 = -9e9f, y1 = -9e9f; foreach (var v in u) { x0 = Mathf.Min(x0, v.X); y0 = Mathf.Min(y0, v.Y); x1 = Mathf.Max(x1, v.X); y1 = Mathf.Max(y1, v.Y); } return "[" + x0.ToString("0.00") + ".." + x1.ToString("0.00") + ", " + y0.ToString("0.00") + ".." + y1.ToString("0.00") + "]"; } GD.Print("UVAUDIT ", nm, " s", s, " UV0=", D(u0), " UV1=", D(u1)); } } GetTree().Quit(); return; }
  CarLoader.ApplyExact(car, ci); var viewWheels=CarLoader.AttachWheels(car, ci);if(int.TryParse(System.Environment.GetEnvironmentVariable("MYSIM_COMPOUND"),out int compound))TyreAppearance.Apply(viewWheels,(Compound)Mathf.Clamp(compound,0,3)); if (System.Environment.GetEnvironmentVariable("MYSIM_UNSHADED") != null) CarLoader.ForceUnshaded(car); car.Position = new Vector3(0, 0.0f, 0);
  if(System.Environment.GetEnvironmentVariable("MYSIM_REBUILT_BODY_PAINT")=="1")RecoveredBodyPaint.Apply(car,ci);
  if(System.Environment.GetEnvironmentVariable("MYSIM_NEUTRAL_CAR_MATERIAL")=="1"){var neutral=new StandardMaterial3D {AlbedoColor=new Color(.42f,.45f,.48f),Roughness=.65f,CullMode=BaseMaterial3D.CullModeEnum.Disabled};foreach(var n in car.FindChildren("*","MeshInstance3D",true,false)){var mesh=(MeshInstance3D)n;if(mesh.IsVisibleInTree())mesh.MaterialOverride=neutral;}}
  if(System.Environment.GetEnvironmentVariable("MYSIM_CAR_ASSEMBLY_AUDIT") is string assemblyPath)CarAssemblyAudit.Write(car,ci,assemblyPath);
  var cam = new Camera3D { Fov = 40, Current = true, Position = new Vector3(5.2f, 1.8f, 5.6f) }; var cp = System.Environment.GetEnvironmentVariable("MYSIM_CAMPOS"); if (cp != null) { var q = cp.Split(','); cam.Position = new Vector3(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(q[2], System.Globalization.CultureInfo.InvariantCulture)); } AddChild(cam); cam.LookAt(new Vector3(0, 0.6f, 0));
  if(int.TryParse(System.Environment.GetEnvironmentVariable("MYSIM_WHEEL_CLOSEUP"),out int wheelIndex)){
   var wheel=viewWheels[Mathf.Clamp(wheelIndex,0,3)];var target=wheel.GlobalPosition;float side=Mathf.Sign(target.X-car.GlobalPosition.X);
   cam.Position=target+new Vector3(side*1.05f,.16f,.25f);cam.Fov=38;cam.LookAt(target);FitCarView(cam,wheel,target,"wheel");
  }else if(System.Environment.GetEnvironmentVariable("MYSIM_FITVIEW")=="1")FitCarView(cam,car);
  for (int i = 0; i < 25; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
  GetViewport().GetTexture().GetImage().SavePng(System.Environment.GetEnvironmentVariable("MYSIM_SHOT") ?? "carview.png"); GetTree().Quit();
 }
 CanvasLayer pauseLayer;
 void PauseUi() {
  bool paused = drive != null && drive.Race != null && drive.Race.paused && !resultsShown;
  if (paused && pauseLayer == null) { pauseLayer = new CanvasLayer { Layer = 20 }; AddChild(pauseLayer); var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), AnchorRight = 1, AnchorBottom = 1 }; pauseLayer.AddChild(dim);
   var box = new VBoxContainer { Position = new Vector2(540, 250), CustomMinimumSize = new Vector2(200, 0) }; pauseLayer.AddChild(box);
   var t = new Label { Text = "PAUSED" }; t.AddThemeFontSizeOverride("font_size", 44); box.AddChild(t);
   var rs = new Button { Text = "RESUME (Esc / Tab)" }; rs.Pressed += () => { Ui("click"); drive.Race.paused = false; }; box.AddChild(rs);
   var rt = new Button { Text = "RESTART" }; rt.Pressed += () => { Ui("click"); RemovePause(); StartRace(lastTrack, lastCar, lastLaps); }; box.AddChild(rt);
   var mn = new Button { Text = "MAIN MENU" }; mn.Pressed += () => { Ui("back"); RemovePause(); ShowMenu(); }; box.AddChild(mn); rs.GrabFocus(); }
  else if (!paused && pauseLayer != null) RemovePause();
 }
 void RemovePause() { pauseLayer?.QueueFree(); pauseLayer = null; }
 async void BakeCollision(int trackIdx) {
  var tl = Json.ParseString(FileAccess.GetFileAsString("res://assets/RealRacing/TrackList.json")).AsGodotDictionary()["tracks"].AsGodotArray()[trackIdx].AsGodotDictionary();
  string mp = tl["map"].AsString(), scn = tl["scene"].AsString();
  using var source=ResourceLoader.Load<PackedScene>("res://assets/RealRacing/Source/"+mp+"/"+scn,cacheMode:ResourceLoader.CacheMode.IgnoreDeep);
  var track=source.Instantiate<Node3D>(); track.Scale = new Vector3(100f, 100f, 100f); AddChild(track);
  // The bake stores positions only and reads with BackfaceCollision=true; visual normal/winding rewrites are unnecessary.
  string outp = System.Environment.GetEnvironmentVariable("MYSIM_BAKEOUT") ?? ("user://col_" + mp + ".bin");
  int tris = CollisionBake.Write(track, outp); GD.Print("BAKECOL map=", mp, " tris=", tris, " out=", outp);
  track.Free();track=null;source.Dispose();
  // Finish the initiating Ready frame and release temporary managed mesh wrappers before engine teardown.
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  System.GC.Collect();System.GC.WaitForPendingFinalizers();System.GC.Collect();GetTree().Quit();
 }
 async void CrashWorld(int trackIdx) {
  string colDir = System.Environment.GetEnvironmentVariable("MYSIM_COLDIR");
  if (colDir != null) { // collision-only world (CI): drive surface text + baked barrier triangles, no models/textures needed
   var tl0 = Json.ParseString(FileAccess.GetFileAsString("res://data/TrackList.json")).AsGodotDictionary()["tracks"].AsGodotArray()[trackIdx].AsGodotDictionary();
   string mp0 = tl0["map"].AsString(), lay0 = tl0["layout"].AsString();
   string ds0 = "res://data/surfaces/" + mp0 + "_" + lay0 + ".txt"; if (!FileAccess.FileExists(ds0)) ds0 = "res://data/surfaces/" + mp0 + "_processed.txt";
   AddChild(DriveSurfaceLoader.Load(ds0)); AddChild(CollisionBake.Read(colDir + "/col_" + mp0 + ".bin"));
   for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
   int pts0 = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_CRASHPTS") ?? "40"), shard0 = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SHARD") ?? "0"), shards0 = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SHARDS") ?? "1");
   CrashBatch.Run(GetWorld3D().DirectSpaceState, trackIdx, pts0, shard0, shards0, System.Environment.GetEnvironmentVariable("MYSIM_CRASHOUT") ?? ("crash_" + trackIdx + "_" + shard0 + ".jsonl")); GetTree().Quit(); return;
  }
  var tl = Json.ParseString(FileAccess.GetFileAsString("res://assets/RealRacing/TrackList.json")).AsGodotDictionary()["tracks"].AsGodotArray()[trackIdx].AsGodotDictionary();
  string mp = tl["map"].AsString(), lay = tl["layout"].AsString(), scn = tl["scene"].AsString();
  var track = GD.Load<PackedScene>("res://assets/RealRacing/Source/" + mp + "/" + scn).Instantiate<Node3D>(); track.Scale = new Vector3(100f, 100f, 100f); AddChild(track);
  WindingFixer.Fix(track, out int wc); GroundFixer.Fix(track); WallBuilder.Build(track);
  string ds = "res://assets/Resources/" + mp + "_" + lay + ".txt"; AddChild(DriveSurfaceLoader.Load(FileAccess.FileExists(ds) ? ds : "res://assets/Resources/" + mp + "_processed.txt"));
  for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
  int pts = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_CRASHPTS") ?? "40"), shard = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SHARD") ?? "0"), shards = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SHARDS") ?? "1");
  string outp = System.Environment.GetEnvironmentVariable("MYSIM_CRASHOUT") ?? ("user://crash_" + trackIdx + "_" + shard + ".jsonl");
  CrashBatch.Run(GetWorld3D().DirectSpaceState, trackIdx, pts, shard, shards, outp); GetTree().Quit();
 }
 void ClearUi() { ui?.QueueFree(); ui = null; }
 RaceSeries activeSeries;
 void ShowMenu() {
  activeSeries=null;EndRace();ClearUi();menuStart=null;resultsShown=false;ui=new CanvasLayer();AddChild(ui);
  RaceSeries savedSeries=null;try{string saved=Settings.Get("series_resume","");if(!string.IsNullOrEmpty(saved))savedSeries=RaceSeries.Restore(saved);}catch(System.Exception e){GD.PrintErr("SERIES_RESTORE_FAILED ",e.Message);}
  var modes=new ModeMenu {ResumeSeries=savedSeries!=null&&!savedSeries.Complete?()=>StartSeriesSession(savedSeries):null,QuickRace=ShowRaceSetup,Series=()=>{var setup=new SeriesSetupPanel();setup.StartSeries+=StartSeriesSession;AddChild(setup);},CreateRoom=()=>AddChild(new LanRoomPanel {Hosting=true}),JoinRoom=()=>AddChild(new LanRoomPanel {Hosting=false})};ui.AddChild(modes);
 }
 void ShowRaceSetup() {
  EndRace(); ClearUi(); ui = new CanvasLayer(); AddChild(ui); resultsShown = false; menuAge = 0;
  var bg = new ColorRect { Color = new Color(0.04f, 0.05f, 0.07f), AnchorRight = 1, AnchorBottom = 1 }; ui.AddChild(bg);
  var title = new Label { Text = "MYSIM", Position = new Vector2(48, 24) }; title.AddThemeFontSizeOverride("font_size", 56); ui.AddChild(title);
  var tl = Json.ParseString(FileAccess.GetFileAsString("res://assets/RealRacing/TrackList.json")).AsGodotDictionary()["tracks"].AsGodotArray();
  var tracks = new ItemList { Position = new Vector2(48, 110), Size = new Vector2(520, 480) }; foreach (var x in tl) tracks.AddItem(x.AsGodotDictionary()["name"].AsString()); tracks.Select(lastTrack); ui.AddChild(tracks);
  var cars = new ItemList { Position = new Vector2(600, 110), Size = new Vector2(420, 480) }; foreach (var n in CarCatalog.Names) cars.AddItem(n); cars.Select(lastCar); ui.AddChild(cars);
  var lapsLbl = new Label { Text = "LAPS", Position = new Vector2(1060, 110) }; ui.AddChild(lapsLbl);
  var laps = new SpinBox { MinValue = 1, MaxValue = 20, Value = lastLaps, Position = new Vector2(1060, 140) }; ui.AddChild(laps);
  var infoLbl = new Label { Position = new Vector2(1060, 290), Size = new Vector2(200, 200), AutowrapMode = TextServer.AutowrapMode.WordSmart }; ui.AddChild(infoLbl);
  var prev = new TextureRect { Position = new Vector2(1060, 500), Size = new Vector2(200, 112), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered }; ui.AddChild(prev);
  System.Action refreshInfo = () => { var ts0 = tracks.GetSelectedItems(); var cs0 = cars.GetSelectedItems(); int ti = ts0.Length > 0 ? ts0[0] : 0, ci2 = cs0.Length > 0 ? cs0[0] : 0; var circ = new Circuit(ti); prev.Texture = GD.Load<Texture2D>("res://assets/previews/t" + ti + ".png"); infoLbl.Text = "TRACK\n" + SourceRoutes.Names[ti] + "\nLength " + (circ.length / 1000f).ToString("0.00") + " km\nWidth " + circ.width.ToString("0") + " m\n\nCAR\n" + CarCatalog.Names[ci2] + "\nMass " + RecoveredVehicleProfiles.Mass[ci2].ToString("0") + " kg\nTop speed " + (RecoveredVehicleProfiles.Speed[ci2] * 1.609f).ToString("0") + " km/h\n0-60 mph " + RecoveredVehicleProfiles.Acceleration[ci2].ToString("0.0") + " s\nWheelbase " + RecoveredGeometry.Wheelbase[ci2].ToString("0.00") + " m"; };
  tracks.ItemSelected += i => { Ui("toggle"); refreshInfo(); }; cars.ItemSelected += i => { Ui("toggle"); refreshInfo(); }; refreshInfo();
  var hint = new Label { Text = "Pick track and car, then START.  Drive: WASD/arrows, Space handbrake, P pit, C camera, hold brake when stopped = reverse, Esc pause", Position = new Vector2(48, 650) }; ui.AddChild(hint);
  var start = new Button { Text = "START RACE", Position = new Vector2(1060, 200), Size = new Vector2(180, 56) };
  start.Pressed += () => { Ui("swoosh"); var ts = tracks.GetSelectedItems(); var cs = cars.GetSelectedItems(); StartRace(ts.Length > 0 ? ts[0] : 0, cs.Length > 0 ? cs[0] : 0, (int)laps.Value); };
  var options=new Button {Text="SESSION OPTIONS",Position=new Vector2(1040,590),Size=new Vector2(200,36)};options.Pressed+=()=>AddChild(new RaceSettingsPanel());ui.AddChild(options);
  var back=new Button {Text="BACK",Position=new Vector2(48,600),Size=new Vector2(180,36)};back.Pressed+=ShowMenu;ui.AddChild(back);
  tracks.GrabFocus(); menuStart = start;
  var controls=new Button {Text="CONTROLS / KEY BINDINGS",Position=new Vector2(600,590),Size=new Vector2(420,36)};controls.Pressed+=()=>AddChild(new ControlsPanel());ui.AddChild(controls);
  ui.AddChild(start); var raceTheme=MySimTheme.Build();foreach(var child in ui.GetChildren())if(child is Control control)control.Theme=raceTheme; tracks.ItemActivated += i => start.EmitSignal(BaseButton.SignalName.Pressed); cars.ItemActivated += i => start.EmitSignal(BaseButton.SignalName.Pressed);
 }
 async void DebugRoad(Node3D track) {
  foreach (var n in track.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; mi.CreateTrimeshCollision(); foreach (var ch in mi.GetChildren()) if (ch is StaticBody3D sb) { sb.CollisionLayer = 8; sb.CollisionMask = 0; sb.SetMeta("mesh", mi.GetPath().ToString()); foreach (var cs in sb.GetChildren()) if (cs is CollisionShape3D c3 && c3.Shape is ConcavePolygonShape3D cp) cp.BackfaceCollision = true; } }
  await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
  var space = GetWorld3D().DirectSpaceState; foreach (var p in new[] { new Vector3(-266.6f, 0, -266.1f), new Vector3(-247.1f, 0, -291.4f), new Vector3(-30f, 0, -460f) }) { var q = PhysicsRayQueryParameters3D.Create(new Vector3(p.X, 100, p.Z), new Vector3(p.X, -100, p.Z)); q.CollisionMask = 8; q.HitBackFaces = true; var r = space.IntersectRay(q); if (r.Count == 0) { GD.Print("ROADRAY none at ", p); continue; } var body = (Node)r["collider"]; var mi = GetNode<MeshInstance3D>(body.GetMeta("mesh").AsString()); string mats = ""; for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) mats += (mi.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? "null") + "|"; GD.Print("ROADRAY ", p, " hit y=", ((Vector3)r["position"]).Y.ToString("0.00"), " mesh=", mi.Name, " mats=", mats, " active=", mi.GetActiveMaterial(0) is StandardMaterial3D sm ? (sm.AlbedoTexture != null ? sm.AlbedoTexture.ResourcePath : "color") : "none"); }
 }
 void EndRace() { world?.QueueFree(); world = null; drive = null; }
 bool loadingRace;
 async System.Threading.Tasks.Task<PackedScene> LoadSceneAsync(string path) {
  var error=ResourceLoader.LoadThreadedRequest(path,"PackedScene",true);
  if(error!=Error.Ok) throw new System.Exception("Cannot load "+path+": "+error);
  while(ResourceLoader.LoadThreadedGetStatus(path)==ResourceLoader.ThreadLoadStatus.InProgress) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  var scene=ResourceLoader.LoadThreadedGet(path) as PackedScene;
  return scene ?? throw new System.Exception("Scene load failed: "+path);
 }
 async void StartRace(int trackIdx, int carIdx, int laps) {
  if(loadingRace)return;loadingRace=true;
  try {

  EndRace(); ClearUi(); lastTrack = trackIdx; lastCar = carIdx; lastLaps = laps; resultsShown = false; finShown = 0; Settings.Set("track", trackIdx); Settings.Set("car", carIdx); Settings.Set("laps", laps);
  menuStart=null;
  ui=new CanvasLayer { Layer=30 };AddChild(ui);
  ui.AddChild(new ColorRect { Color=new Color(.025f,.035f,.05f),AnchorRight=1,AnchorBottom=1 });
  var loading=new Label { Text="LOADING CIRCUIT",Position=new Vector2(48,300) };loading.AddThemeFontSizeOverride("font_size",32);ui.AddChild(loading);
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  world = new Node3D { Name = "World" }; AddChild(world);
  var tl = Json.ParseString(FileAccess.GetFileAsString("res://assets/RealRacing/TrackList.json")).AsGodotDictionary()["tracks"].AsGodotArray()[trackIdx].AsGodotDictionary();
  string mp = tl["map"].AsString(), lay = tl["layout"].AsString(), scn = tl["scene"].AsString();
  var trackScene=await LoadSceneAsync("res://assets/RealRacing/Source/" + mp + "/" + scn); var track=trackScene.Instantiate<Node3D>(); track.Scale = new Vector3(100f, 100f, 100f); world.AddChild(track);
  var layers = new System.Collections.Generic.List<Node3D> { track }; foreach (var vp in tl["variationPaths"].AsGodotArray()) { string rp = "res://" + vp.AsString().Replace("Assets/RealRacing/Source/", "assets/RealRacing/Source/"); if (EmptyVariations.Paths.Contains(rp)) { GD.Print("VARIATION empty source layer ", rp); continue; } if (!ResourceLoader.Exists(rp)) { GD.Print("VARIATION missing ", rp); continue; } var vps = await LoadSceneAsync(rp); if (vps == null) { GD.Print("VARIATION import failed ", rp); continue; } var vn = vps.Instantiate<Node3D>(); vn.Scale = new Vector3(100f, 100f, 100f); world.AddChild(vn); layers.Add(vn); GD.Print("VARIATION loaded ", rp); }
  string sceneAudit=System.Environment.GetEnvironmentVariable("MYSIM_TRACK_SCENE_AUDIT");if(sceneAudit!=null){TrackSceneAudit.Write(layers,trackIdx,sceneAudit);CleanExit.Request(this);return;}
  loading.Text="PREPARING CAR";await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  string carDir="res://assets/RealRacing/Source/"+CarCatalog.Folders[carIdx]+"/";
  foreach(var part in Assets.Files(carDir))if(part.EndsWith(".fbx"))await LoadSceneAsync(carDir+part);
  var car = CarLoader.Load(CarCatalog.Folders[carIdx]); world.AddChild(car); CarLoader.ApplyExact(car, carIdx); var wheelPivots = CarLoader.AttachWheels(car, carIdx);
  loading.Text="PREPARING TRACK SURFACES";await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  int baseVar = (int)tl["baseTrackIndex"]; foreach (var L in layers) { WindingFixer.Fix(L, out int wchk); GroundFixer.Fix(L); WallBuilder.Build(L); UnityMaterialBinder.Apply(L, baseVar, out int utot); TrackMaterialBinder.Apply(L, "res://assets/" + mp + "/"); }
  SkyDome.Add(world, mp);
  string ds = "res://assets/Resources/" + mp + "_" + lay + ".txt"; world.AddChild(DriveSurfaceLoader.Load(FileAccess.FileExists(ds) ? ds : "res://assets/Resources/" + mp + "_processed.txt"));
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_TRACK_TRIAL") is string nativeTrial){world.AddChild(new NativeTrackTrial {TrackIndex=trackIdx,CarIndex=carIdx,Car=car,Wheels=wheelPivots,Destination=nativeTrial});ClearUi();return;}
  if (System.Environment.GetEnvironmentVariable("MYSIM_DEBUGROAD") != null) DebugRoad(track);
  var cam = new Camera3D { Fov = 46, Current = true, Far = 4000 }; world.AddChild(cam);
  var carSize = CarLoader.MeasureSize(car); GD.Print("CARSIZE ", carIdx, " ", carSize.ToString("0.00")); drive = new DriveController { CarIndex = carIdx, CarSize = carSize, Car = car, WheelPivots = wheelPivots, TrackIndex = trackIdx, Laps = laps, Cam = cam, AutoDrive = System.Environment.GetEnvironmentVariable("MYSIM_AUTODRIVE") != null, AiTest = System.Environment.GetEnvironmentVariable("MYSIM_AI") != null, AutoSeconds = float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_SECONDS") ?? "8") };
  var hud = new Hud(); drive.Hud = hud; world.AddChild(drive); if (System.Environment.GetEnvironmentVariable("MYSIM_INPUTCHECKS") != null) world.AddChild(new DrivingScenario { Drive=drive }); world.AddChild(hud); world.AddChild(new TouchControls { Drive=drive });
  ClearUi();GD.Print("RACE_READY track=",trackIdx," car=",carIdx);
  } catch(System.Exception e) {
   GD.PrintErr("RACE_LOAD_FAILED ",e);EndRace();ClearUi();ui=new CanvasLayer();AddChild(ui);
   var errorLabel=new Label { Text="Could not load this race. See game log for details.",Position=new Vector2(48,300) };ui.AddChild(errorLabel);
   var back=new Button { Text="BACK TO MENU",Position=new Vector2(48,360) };back.Pressed+=ShowMenu;ui.AddChild(back);
  } finally { loadingRace=false; }
 }
 public override void _Process(double delta) {
  PauseUi();
  if (drive != null && !resultsShown && drive.Race != null && drive.Player != null && drive.Player.finished) { finShown += (float)GetProcessDeltaTime(); if (drive.Race.cars.Count <= 1 || drive.Race.AllFinished || finShown > 12f) ShowResults(); }
  if (ui != null && menuStart != null && IsInstanceValid(menuStart) && !resultsShown && (drive == null || !IsInstanceValid(drive) || drive.GetParent() == null)) { var pd0 = Input.GetConnectedJoypads(); bool go = Input.IsKeyPressed(Key.Enter) || (pd0.Count > 0 && Input.IsJoyButtonPressed(pd0[0], JoyButton.Start)); if (go && !menuGoWas && menuAge > 0.4f && GetTree().Root.FindChildren("*","ControlsPanel",true,false).Count==0 && GetTree().Root.FindChildren("*","RaceSettingsPanel",true,false).Count==0 && GetTree().Root.FindChildren("*","SeriesSetupPanel",true,false).Count==0 && GetTree().Root.FindChildren("*","LanRoomPanel",true,false).Count==0) menuStart.EmitSignal(BaseButton.SignalName.Pressed); menuGoWas = go; menuAge += (float)GetProcessDeltaTime(); }
  { bool padA = false, padB = false; var pads = Input.GetConnectedJoypads(); if (pads.Count > 0) { padA = Input.IsJoyButtonPressed(pads[0], JoyButton.A); padB = Input.IsJoyButtonPressed(pads[0], JoyButton.B); }
    bool escNow = Input.IsKeyPressed(Key.Escape) || padB;
    if (resultsShown && resultsArmed > 0.6f) { if (Input.IsKeyPressed(Key.Enter) || padA) { ContinueResult(); } else if (escNow && !escWas) { ShowMenu(); } }
    if (resultsShown) resultsArmed += (float)GetProcessDeltaTime(); else resultsArmed = 0; escWas = escNow; }
 }
 void StartSeriesSession(RaceSeries series){activeSeries=series;Settings.Set("series_resume",series.Save());StartRace(series.CurrentTrack,series.Car,series.Laps);}
 void ContinueResult(){if(activeSeries==null){StartRace(lastTrack,lastCar,lastLaps);return;}if(activeSeries.Complete){ShowMenu();return;}int place=drive.Race.Standings().IndexOf(drive.Player)+1;activeSeries.FinishRound(place);Settings.Set("series_resume",activeSeries.Complete?"":activeSeries.Save());if(activeSeries.Complete){ShowMenu();return;}StartRace(activeSeries.CurrentTrack,activeSeries.Car,activeSeries.Laps);}
 void ShowResults() {
  resultsShown = true; var layer = new CanvasLayer(); world.AddChild(layer);
  var p = drive.Player; var panel = new ColorRect { Color = new Color(0, 0, 0, 0.7f), AnchorRight = 1, AnchorBottom = 1 }; layer.AddChild(panel);
  var l = new Label { Position = new Vector2(420, 90) }; l.AddThemeFontSizeOverride("font_size", 30);
  var order = drive.Race.Standings();
  var resultActions=new VBoxContainer {AnchorLeft=1,AnchorRight=1,AnchorTop=1,AnchorBottom=1,OffsetLeft=-290,OffsetRight=-30,OffsetTop=-150,OffsetBottom=-30,Theme=MySimTheme.Build()};layer.AddChild(resultActions);var proceed=new Button {Text=activeSeries==null?"RACE AGAIN":activeSeries.Round+1==activeSeries.Tracks.Length?"FINISH SERIES":"NEXT ROUND",CustomMinimumSize=new Vector2(240,50)};proceed.Pressed+=ContinueResult;resultActions.AddChild(proceed);var home=new Button {Text="MAIN MENU",CustomMinimumSize=new Vector2(240,50)};home.Pressed+=ShowMenu;resultActions.AddChild(home);

  string board = ""; for (int i = 0; i < order.Count; i++) { var o = order[i]; string who = o == p ? "YOU" : o.driver; float gapM = (drive.Race.DistanceScore(p) - drive.Race.DistanceScore(o)) * drive.Race.track.length; string tm = o.finished ? o.finishTime.ToString("0.0") + " s" : (o == p ? "" : "+" + (gapM > 0 ? gapM : 0).ToString("0") + " m"); board += (i + 1) + ".  " + who.PadRight(8) + " " + tm + "\n"; }
  int place = order.IndexOf(p) + 1;
  if(activeSeries!=null){var summary=new Label {Text="SERIES / ROUND "+(activeSeries.Round+1)+" OF "+activeSeries.Tracks.Length+"\nPrevious finishes: "+string.Join(", ",activeSeries.Places),Position=new Vector2(60,100)};layer.AddChild(summary);}

  l.Text = "RESULTS   P" + place + " of " + order.Count + "\n\n" + (order.Count > 1 ? board + "\n" : "") + "TIME  " + p.finishTime.ToString("0.00") + " s\nBEST LAP  " + p.bestLap.ToString("0.00") + " s\nPENALTY  " + p.penalty.ToString("0.0") + " s\nDAMAGE  " + (int)(p.damage * 100) + "%" + (activeSeries==null?"\n\nENTER race again / ESC menu":"\n\nENTER next round / ESC menu"); layer.AddChild(l);
 }
}}

































