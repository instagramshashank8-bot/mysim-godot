from pathlib import Path
p=Path('src/DriveController.cs');s=p.read_text()
s=s.replace('float revHold;','PedalController pedals = new PedalController();')
a=s.index('  bool wantReverse =');b=s.index('\n  var startPos',a)
s=s[:a]+'''  Player.input = pedals.Update(thr, brk, steer, Input.IsKeyPressed(Key.Space), Vec.Dot(Player.velocity, Player.Forward), (float)delta, !AutoDrive && !AiTest && Player.human);
'''+s[b:]
s=s.replace('new Vector3(1.96f, 1.0f, 4.06f)', 'new Vector3(CarSize.X, Mathf.Clamp(CarSize.Y, 0.6f, 1.5f), CarSize.Z)')
s=s.replace('Contact.MarkGood(Player, gr.near); if (Contact.Contain', 'if (Contact.Contain')
s=s.replace('gr = Contact.Ground(space, w, refY, 0.6f); }\n  var hit', 'gr = Contact.Ground(space, w, refY, 0.6f); } Contact.MarkGood(Player, gr.near && Mathf.Abs(Player.lateral) <= 30f);\n  var hit')
s=s.replace('if (Race.paused) return; bool escWas = false;', 'if (Race.paused) { UpdateAudio(-1f); return; }')
# Measure actual render deltas independently of fixed physics ticks, including when paused.
pos=s.index(' public override void _PhysicsProcess')
s=s[:pos]+''' ulong lastHeartbeat; double worstRenderMs;
 public override void _Process(double delta) {
  worstRenderMs = System.Math.Max(worstRenderMs, delta * 1000);
  if (System.Environment.GetEnvironmentVariable("MYSIM_FPS") == null) return;
  ulong now = Time.GetTicksMsec(); if (now - lastHeartbeat < 2000) return;
  GD.Print("HEARTBEAT wallMs=", now, " paused=", Race?.paused, " sim=", Race?.clock, " fps=", Engine.GetFramesPerSecond(), " worstRenderMs=", worstRenderMs.ToString("0.0"));
  lastHeartbeat = now; worstRenderMs = 0;
 }
'''+s[pos:]
p.write_text(s)
p=Path('src/Contact.cs');s=p.read_text().replace('static readonly System.Collections.Generic.Dictionary<Vehicle, Vec> lastGood = new System.Collections.Generic.Dictionary<Vehicle, Vec>();','sealed class GroundHistory { public Vec Position; }\n static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Vehicle, GroundHistory> lastGood = new();')
s=s.replace('if (groundNear) lastGood[v] = v.position;', 'if (groundNear) lastGood.GetOrCreateValue(v).Position = v.position;')
s=s.replace('lastGood.TryGetValue(v, out var g0) ? g0 : start', 'lastGood.TryGetValue(v, out var g0) ? g0.Position : start')
p.write_text(s)
