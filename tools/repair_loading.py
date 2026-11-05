from pathlib import Path
p=Path('src/Main.cs');s=p.read_text();s=s.replace(' void StartRace(int trackIdx, int carIdx, int laps) {',''' bool loadingRace;
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
''',1)
s=s.replace('  world = new Node3D { Name = "World" }; AddChild(world);','''  menuStart=null;
  ui=new CanvasLayer { Layer=30 };AddChild(ui);
  ui.AddChild(new ColorRect { Color=new Color(.025f,.035f,.05f),AnchorRight=1,AnchorBottom=1 });
  var loading=new Label { Text="LOADING CIRCUIT",Position=new Vector2(48,300) };loading.AddThemeFontSizeOverride("font_size",32);ui.AddChild(loading);
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  world = new Node3D { Name = "World" }; AddChild(world);''',1)
s=s.replace('var track = GD.Load<PackedScene>("res://assets/RealRacing/Source/" + mp + "/" + scn).Instantiate<Node3D>(); track.Scale = new Vector3(100f, 100f, 100f); world.AddChild(track);','var trackScene=await LoadSceneAsync("res://assets/RealRacing/Source/" + mp + "/" + scn); var track=trackScene.Instantiate<Node3D>(); track.Scale = new Vector3(100f, 100f, 100f); world.AddChild(track);',1)
s=s.replace('var vps = GD.Load<PackedScene>(rp);', 'var vps = await LoadSceneAsync(rp);',1)
s=s.replace('  var car = CarLoader.Load(CarCatalog.Folders[carIdx]); world.AddChild(car);','''  loading.Text="PREPARING CAR";await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  string carDir="res://assets/RealRacing/Source/"+CarCatalog.Folders[carIdx]+"/";
  foreach(var part in Assets.Files(carDir))if(part.EndsWith(".fbx"))await LoadSceneAsync(carDir+part);
  var car = CarLoader.Load(CarCatalog.Folders[carIdx]); world.AddChild(car);''',1)
s=s.replace('  int baseVar = (int)tl["baseTrackIndex"]; foreach (var L in layers)', '  loading.Text="PREPARING TRACK SURFACES";await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);\n  int baseVar = (int)tl["baseTrackIndex"]; foreach (var L in layers)',1)
s=s.replace('world.AddChild(new TouchControls());\n }\n public override void _Process', '''world.AddChild(new TouchControls());
  ClearUi();GD.Print("RACE_READY track=",trackIdx," car=",carIdx);
  } catch(System.Exception e) {
   GD.PrintErr("RACE_LOAD_FAILED ",e);EndRace();ClearUi();ui=new CanvasLayer();AddChild(ui);
   var errorLabel=new Label { Text="Could not load this race. See game log for details.",Position=new Vector2(48,300) };ui.AddChild(errorLabel);
   var back=new Button { Text="BACK TO MENU",Position=new Vector2(48,360) };back.Pressed+=ShowMenu;ui.AddChild(back);
  } finally { loadingRace=false; }
 }
 public override void _Process''',1)
p.write_text(s)
