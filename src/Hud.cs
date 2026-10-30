using Godot;
using MySimulator.Rebuild;
namespace MySim {
// Minimal in-game HUD: speed km/h, gear, rpm, lap/target, lap time, tyre wear. Real UI nodes (not a camera render).
public partial class Hud : CanvasLayer {
 public bool Paused; public Race Race; public Vehicle Player; Label speed, info, gear;PitServiceDisplay pitDisplay;
 public override void _Ready() {
  var root = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore }; AddChild(root);
  pitDisplay=new PitServiceDisplay();root.AddChild(pitDisplay);
  speed = Make(root, 48, new Vector2(-300, -140), true); gear = Make(root, 26, new Vector2(-300, -210), true); info = Make(root, 18, new Vector2(24, 20), false); mini = new MiniMap { H = this, Size = new Vector2(190, 150) }; mini.AnchorLeft = touchMode ? .5f : 0; mini.AnchorTop = 1; mini.AnchorRight = touchMode ? .5f : 0; mini.AnchorBottom = 1; mini.OffsetLeft = touchMode ? -100 : 20; mini.OffsetTop = -170; mini.OffsetRight = touchMode ? 90 : 210; mini.OffsetBottom = -20; root.AddChild(mini);
 }
 static bool touchMode => DisplayServer.IsTouchscreenAvailable() || Settings.Get("screen_controls", true) || System.Environment.GetEnvironmentVariable("MYSIM_TOUCH") != null;
 static Label Make(Control p, int size, Vector2 off, bool bottomRight) {
  var l = new Label(); l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", Colors.White); l.AddThemeColorOverride("font_outline_color", Colors.Black); l.AddThemeConstantOverride("outline_size", 3);
  if (bottomRight && touchMode) { l.AnchorLeft = l.AnchorRight = 1; l.AnchorTop = l.AnchorBottom = 0; l.OffsetLeft = off.X; l.OffsetTop = 24 + (off.Y < -180 ? 70 : 0); l.OffsetRight = -24; l.OffsetBottom = l.OffsetTop + 70; l.HorizontalAlignment = HorizontalAlignment.Right; } else if (bottomRight) { l.AnchorLeft = l.AnchorRight = 1; l.AnchorTop = l.AnchorBottom = 1; l.OffsetLeft = off.X; l.OffsetTop = off.Y; l.OffsetRight = -24; l.OffsetBottom = off.Y + 70; l.HorizontalAlignment = HorizontalAlignment.Right; }
  else { l.Position = off; }
  if(!bottomRight){var panel=new Panel {Position=off-new Vector2(10,6),Size=new Vector2(860,62),MouseFilter=Control.MouseFilterEnum.Ignore};panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color(.02f,.03f,.045f,.9f),CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6});p.AddChild(panel);}
  p.AddChild(l); return l;
 }
partial class MiniMap : Control {
  public Hud H; Vector2[] pts; Vector2 mn, mx;
  public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
  void Build() { var c = H.Race.track; int n = c.points.Length; pts = new Vector2[n + 1]; mn = new Vector2(1e9f, 1e9f); mx = new Vector2(-1e9f, -1e9f); for (int i = 0; i < n; i++) { var p = c.points[i]; pts[i] = new Vector2(p.x, -p.z); mn = new Vector2(Mathf.Min(mn.X, pts[i].X), Mathf.Min(mn.Y, pts[i].Y)); mx = new Vector2(Mathf.Max(mx.X, pts[i].X), Mathf.Max(mx.Y, pts[i].Y)); } pts[n] = pts[0]; }
  Vector2 Map(Vector2 w) { float pad = 10f, sw = Size.X - 2 * pad, sh = Size.Y - 2 * pad; float sc = Mathf.Min(sw / Mathf.Max(1f, mx.X - mn.X), sh / Mathf.Max(1f, mx.Y - mn.Y)); var ctr = (mn + mx) * 0.5f; return new Vector2(Size.X * 0.5f, Size.Y * 0.5f) + (w - ctr) * sc; }
  public override void _Process(double delta) { if (H.Race != null && pts == null) Build(); QueueRedraw(); }
  public override void _Draw() {
   if (pts == null || H.Player == null) return; DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.35f));
   var m = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) m[i] = Map(pts[i]); DrawPolyline(m, new Color(1, 1, 1, 0.75f), 3f, true);
   foreach (var v in H.Race.cars) { if (v == H.Player) continue; DrawCircle(Map(new Vector2(v.position.x, -v.position.z)), 3.5f, new Color(0.95f, 0.75f, 0.2f)); }
   DrawCircle(Map(new Vector2(H.Player.position.x, -H.Player.position.z)), 5f, new Color(0.2f, 0.9f, 0.35f));
  }
 }
 MiniMap mini;
 public override void _Process(double delta) {
  if (Player == null) return;pitDisplay.Player=Player;
  if (Paused) { info.Text = "PAUSED  (Esc / Tab to resume)"; return; }
  speed.Text = ((int)(Player.speed * 3.6f)) + " km/h"; gear.Text = "GEAR " + (Player.gear < 0 ? "R" : Player.gear.ToString());
  int rank = Race.Standings().IndexOf(Player) + 1;
  string pos = Race.cars.Count > 1 ? "POS " + rank + "/" + Race.cars.Count + "   " : "";
  info.Text = pos + (Player.finished ? "FINISHED " + Race.targetLaps + " laps" : "LAP " + System.Math.Min(Player.completedLaps + 1, Race.targetLaps) + "/" + Race.targetLaps) + "   LAP TIME " + Player.lapElapsed.ToString("0.0") + "s   BEST " + (Player.bestLap > 0 ? Player.bestLap.ToString("0.000") + "s" : "-- (no valid lap)") + "\nTYRES " + (int)(100 - Player.Wear * 100) + "%   PIT " + (Player.servicing ? PitServiceTimeline.Stage(PitServiceTimeline.Duration-Player.serviceRemaining) + " " + Player.serviceRemaining.ToString("0.0") + "s" : Player.pitActive ? "IN LANE" : Player.pitRequested ? "REQUESTED ("+OS.GetKeycodeString(ControlBindings.Get(5))+")" : (Race.track.PitValid ? "press "+OS.GetKeycodeString(ControlBindings.Get(5)) : "no pit lane")) + "   FUEL " + (int)Player.fuel + " L   DAMAGE " + (int)(Player.damage * 100) + "%";
 }
}}




