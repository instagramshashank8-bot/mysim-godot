using System;
using System.Collections.Generic;
using System.Threading;
using MySimulator.Rebuild;
namespace MySimulator.Verification
{
    public static class CoreChecks
    {
        static int count;
        static void Check(bool yes,string reason){if(!yes)throw new Exception(reason);count++;}
        static Race Race(int variant=0){var r=new Race(variant,1,0);r.countdown=0;r.Add("Host",true,Compound.Medium);for(int i=1;i<6;i++)r.Add("Driver "+i,false,Compound.Medium);return r;}
        public static string Run()
        {
            count=0;var log=new List<string>();
            var seriesTracks=new[]{0,1,0};var series=new RaceSeries(seriesTracks,0,2);seriesTracks[0]=3;Check(series.CurrentTrack==0,"Series aliases mutable setup input");series.FinishRound(2);Check(series.CurrentTrack==1&&series.Places[0]==2,"Series did not advance/preserve result");var resumedSeries=RaceSeries.Restore(series.Save());Check(resumedSeries.Round==1&&resumedSeries.CurrentTrack==1&&resumedSeries.Car==0&&resumedSeries.Laps==2&&resumedSeries.Places[0]==2,"Series resume lost configuration/progress");series.FinishRound(1);Check(series.CurrentTrack==0,"Repeated track round lost");series.FinishRound(3);Check(series.Complete&&series.Places.Count==3,"Series completion incorrect");bool extraRound=false;try{series.FinishRound(1);}catch(InvalidOperationException){extraRound=true;}Check(extraRound,"Completed series accepted another result");

            var water=new SurfaceWeather(0);water.Step(100,.01f,0);Check(water.Water>0&&water.Water<1,"rain did not accumulate surface water");float accumulatedWater=water.Water;water.Step(100,0,.01f);Check(water.Water<accumulatedWater,"dry weather did not drain surface water");var whole=new SurfaceWeather(.3f);var split=new SurfaceWeather(.3f);whole.Step(10,.005f,.002f);for(int i=0;i<100;i++)split.Step(.1f,.005f,.002f);Check(Math.Abs(whole.Water-split.Water)<.0001f,"weather evolution depends on frame rate");
            for(int wheel=0;wheel<4;wheel++){
                Check(PitServiceTimeline.Loosen(wheel)<PitServiceTimeline.Remove(wheel)&&PitServiceTimeline.Remove(wheel)<PitServiceTimeline.Fit(wheel)&&PitServiceTimeline.Fit(wheel)<PitServiceTimeline.Secure(wheel),"pit wheel order invalid");
                Check(PitServiceTimeline.Secure(wheel)<PitServiceTimeline.LowerStart,"jack lowers before wheel secured");
                Check(PitServiceTimeline.WheelOut(-1,wheel)==0&&PitServiceTimeline.WheelOut(PitServiceTimeline.Duration,wheel)==0,"pit leaves wheel off its hub");
            }
            Check(PitServiceTimeline.Lift(-1)==0&&PitServiceTimeline.Lift(PitServiceTimeline.Duration)==0,"pit leaves car on jack");
            foreach(float mass in new[]{600f,2000f})foreach(float down in new[]{0f,20000f})foreach(float ax in new[]{-100f,0f,100f})foreach(float ay in new[]{-100f,0f,100f}){
                var loads=new float[4];Dynamics.NormalLoads(mass,2.8f,1.6f,.35f,down,ax,ay,loads);float sum=0;foreach(float load in loads){Check(float.IsFinite(load)&&load>=0,"negative/nonfinite tyre normal load");sum+=load;}Check(Math.Abs(sum-(mass*9.81f+down))<.02f,"load transfer invented vertical force");
                var mirror=new float[4];Dynamics.NormalLoads(mass,2.8f,1.6f,.35f,down,ax,-ay,mirror);Check(Math.Abs(loads[0]-mirror[1])<.01f&&Math.Abs(loads[2]-mirror[3])<.01f,"left-right load transfer is asymmetric");
            }
            foreach(float mass in new[]{600f,1200f,2000f})foreach(float service in new[]{0f,500f,15000f}){
                float front=Dynamics.WheelBrakeForce(service,mass,true,true),rear=Dynamics.WheelBrakeForce(service,mass,false,true);
                Check(Math.Abs(front-Dynamics.WheelBrakeForce(service,mass,true,false))<.001f,"handbrake applies front-wheel torque");
                Check(Math.Abs(2*(front+rear)-(service+mass*3))<.01f,"handbrake force counted twice");
                Check(Math.Abs(2*(Dynamics.WheelBrakeForce(service,mass,true,false)+Dynamics.WheelBrakeForce(service,mass,false,false))-service)<.01f,"service brake force is not conserved");
            }
            foreach(float speed in new[]{-20f,0f,8f,18f,40f,70f})foreach(float fraction in new[]{-1f,-.8f,-.3f,0f,.3f,.8f,1f}){
                float limit=Dynamics.SteeringLock(speed),requested=limit*fraction;float commanded=Dynamics.SteeringCommand(requested,speed);
                Check(Math.Abs(Dynamics.SteeringTarget(commanded,speed)-requested)<.00001f,"AI steering does not request the actual model wheel angle");
                float previous=MathUtil.Clamp((45f-Math.Max(Math.Abs(speed),.1f))/37f,0,1)*(.55f-.085f)+.085f;
                Check(Math.Abs(Dynamics.SteeringTarget(commanded,speed)-MathUtil.Clamp(commanded*330f/256f*previous,-previous,previous))<.00001f,"steering target refactor changed driver response");
            }
            foreach(int variant in new[]{0,1,2})
            {
                var r=new Race(variant,2,0);r.countdown=0;var v=r.Add("Autopilot",false,Compound.Medium);
                float worst=0;for(int i=0;i<40000&&!v.finished;i++){r.Advance(.02f);worst=Math.Max(worst,Math.Abs(v.lateral));Check(MathUtil.Finite(v.position.x)&&MathUtil.Finite(v.speed),"Non-finite dynamics");}
                Check(v.finished&&v.completedLaps==2,"AI failed to complete circuit "+variant);Check(v.bestLap>10,"Invalid lap timing");Check(worst<15,"AI departed circuit "+variant);
                log.Add("PASS two complete laps, circuit "+variant+", finish "+v.finishTime.ToString("0.00")+"s, max lateral "+worst.ToString("0.00")+"m");
            }
            foreach(float wetness in new[]{0f,.85f})
            {
                var field=new Race(0,1,wetness){countdown=0};for(int i=0;i<6;i++)field.Add("AI "+i,false,wetness>0?Compound.Wet:Compound.Medium);
                bool done=false;for(int i=0;i<12000&&!done;i++){field.Advance(.02f);done=true;foreach(var v in field.cars){done&=v.finished;Check(MathUtil.Finite(v.position.x),"Traffic dynamics invalid");}}
                Check(done,"Six-car traffic race failed, wetness="+wetness);log.Add("PASS complete six-car traffic race, wetness "+wetness);
            }
            var track=new Circuit(0);var manual=new Vehicle();manual.Place(track,0,0);manual.input=new DriverInput(1,0,0);
            for(int i=0;i<384;i++)manual.Step(track,MySimulator.Rebuild.Race.FixedDt,0,true,i*MySimulator.Rebuild.Race.FixedDt,1);
            Check(manual.speed>12,"Throttle did not accelerate vehicle");manual.input=new DriverInput(0,1,0);
            for(int i=0;i<840;i++)manual.Step(track,MySimulator.Rebuild.Race.FixedDt,0,true,8+i*MySimulator.Rebuild.Race.FixedDt,1);
            Check(manual.speed<.1f,"Brake did not stop vehicle");manual.input=new DriverInput(1,0,0){reverse=true};for(int i=0;i<240;i++)manual.Step(track,MySimulator.Rebuild.Race.FixedDt,0,true,20+i*MySimulator.Rebuild.Race.FixedDt,1);Check(Vec.Dot(manual.velocity,manual.Forward)<-2&&manual.gear==-1,"Reverse did not engage");log.Add("PASS acceleration, braking and reverse");
            var tyre=new Tyre{compound=Compound.Soft,temperature=88};float fresh=tyre.Grip(0);
            for(int i=0;i<15000;i++)tyre.Step(.02f,38,.3f,1,0);
            Check(tyre.wear>.25f,"Tyre did not wear");Check(tyre.Grip(0)<fresh,"Tyre wear did not reduce grip");Check(tyre.temperature>=20&&tyre.temperature<=170,"Tyre temperature unbounded");tyre.Replace(Compound.Wet);Check(tyre.wear==0&&tyre.compound==Compound.Wet,"Tyre replacement failed");
            var slick=new Tyre{compound=Compound.Medium,temperature=88};var wet=new Tyre{compound=Compound.Wet,temperature=65};Check(wet.Grip(1)>slick.Grip(1),"Wet compound has no wet advantage");log.Add("PASS tyre wear, grip, heat, compound and replacement");
            var lapper=new Vehicle();lapper.lapElapsed=55;lapper.RegisterProgress(.98f,.01f,55,1);Check(lapper.completedLaps==0,"Shortcut awarded a lap");lapper.RegisterProgress(.24f,.26f,10,1);lapper.RegisterProgress(.49f,.51f,20,1);lapper.RegisterProgress(.74f,.76f,30,1);lapper.RegisterProgress(.98f,.01f,55,1);Check(lapper.finished&&lapper.completedLaps==1,"Valid checkpoint lap rejected");log.Add("PASS ordered checkpoints and shortcut rejection");
            var pitRace=new Race(0,3,0);pitRace.countdown=0;var pit=pitRace.Add("Pit driver",false,Compound.Soft);pit.Place(pitRace.track,.875f,0);pit.pitRequested=true;pit.nextCompound=Compound.Hard;foreach(var t in pit.tyres)t.wear=.5f;bool serviceSeen=false,serviceDone=false;
            for(int i=0;i<9000;i++){pitRace.Step(.02f);serviceSeen|=pit.servicing;if(serviceSeen&&!pit.servicing&&pit.tyres[0].compound==Compound.Hard){serviceDone=true;if(!pit.pitActive)break;}}
            Check(serviceSeen&&serviceDone&&!pit.pitActive,"Pit assist did not enter, service and rejoin");Check(pit.Wear<.01f,"Service did not reset wear");log.Add("PASS complete assisted pit entry, timed stop, tyre change and circuit rejoin");
            var external=new Vehicle{externalPhysics=true,human=true};external.Place(track,.30f,20);external.speed=25;external.velocity=external.Forward*25;
            for(int i=0;i<400;i++)external.Step(track,.01f,0,true,i*.01f,3);
            Check(external.lapInvalid&&external.totalDistance>90,"External physics bypassed track limits/distance");
            var stopped=new Vehicle{pitActive=true,servicing=true,serviceRemaining=7.5f};stopped.position=track.PitBox;stopped.Step(track,1,0,false,0,3);Check(stopped.serviceRemaining==7.5f,"Service progressed before race start");
            var speeding=new Vehicle{externalPhysics=true,pitActive=true,human=true};speeding.position=track.PitBox;speeding.speed=20;
            for(int i=0;i<300;i++)speeding.Step(track,.01f,0,true,i*.01f,3);
            Check(speeding.pitSpeedPenaltyApplied&&speeding.penalty==0,"Pit speeding must be detected without a time penalty");
            var reset=new Vehicle{speed=9};reset.Place(track,.4f,0);reset.speed=9;var resetRace=new Race(0,3,0);resetRace.Reset(reset);Check(reset.penalty==0,"High speed reset accepted");reset.speed=0;resetRace.Reset(reset);Check(reset.penalty==0&&reset.lapInvalid,"Reset lap invalidation missing");
            var invalid=new Vehicle{lapInvalid=true,lapElapsed=50,nextGate=4};invalid.RegisterProgress(.98f,.01f,50,2);Check(invalid.completedLaps==1&&invalid.bestLap==0&&!invalid.lapInvalid,"Invalid lap entered best-lap records");
            log.Add("PASS shared external-physics rules, pit-speed penalty, service freeze, reset limits and invalid-lap records");
            var stacking=new Race(0,3,0){countdown=0};var first=stacking.Add("First",true,Compound.Soft);var second=stacking.Add("Second",true,Compound.Medium);first.position=second.position=stacking.track.PitBox;first.pitActive=second.pitActive=true;first.externalPhysics=second.externalPhysics=true;stacking.Step(.01f);Check(first.servicing&&!second.servicing,"Occupied pit box accepted a second service");
            first.serviceRemaining=.001f;second.position=first.position+first.Forward*3;stacking.Step(.01f);Check(first.servicing&&first.serviceRemaining==0,"Unsafe pit release accepted");second.position=first.position+first.Forward*10;stacking.Step(.01f);Check(!first.servicing&&first.tyres[0].wear==0,"Cleared pit release did not complete");log.Add("PASS occupied pit box, unsafe release hold and release after clearance");
            var classification=new Race(0,1,0){started=true,clock=130};var finisher=classification.Add("Finished",true,Compound.Medium);var dnf=classification.Add("Stopped",true,Compound.Medium);finisher.finished=true;finisher.finishTime=10;classification.Step(.01f);Check(dnf.retired&&classification.Classified&&classification.Standings()[0]==finisher,"Finish window did not classify non-finishers");
            var collision=Race();var a=collision.cars[0];var b=collision.cars[1];a.heading=0;b.heading=0;a.position=new Vec(0,0);b.position=new Vec(1,0);a.velocity=new Vec(10,0);b.velocity=new Vec();collision.Collisions();Check((b.position-a.position).Magnitude>=2,"Collision failed to separate vehicles");Check(a.damage>0&&b.damage>0,"Collision did not apply damage");log.Add("PASS collision separation and damage");
            a.carModel=3;a.hasBodyPose=true;a.bodyHeight=42.125f;a.bodyQx=0;a.bodyQy=.6f;a.bodyQz=0;a.bodyQw=.8f;string state=Wire.Snapshot(collision,9);int seq;var copy=Wire.ReadSnapshot(state.Split('|'),out seq);Check(copy.cars[0].carModel==3,"Car model snapshot mismatch");Check(seq==9&&copy.cars.Count==6,"Snapshot count changed");Check(Math.Abs(copy.cars[0].position.x-a.position.x)<.002f,"Snapshot pose changed");
            Check(copy.cars[0].hasBodyPose&&Math.Abs(copy.cars[0].bodyHeight-42.125f)<.002f&&Math.Abs(copy.cars[0].bodyQy-.6f)<.002f&&Math.Abs(copy.cars[0].bodyQw-.8f)<.002f,"Full native body pose lost in snapshot");
            a.bodyQw=3;bool poseRejected=false;try{Wire.ReadSnapshot(Wire.Snapshot(collision,10).Split('|'),out seq);}catch(FormatException){poseRejected=true;}Check(poseRejected,"Invalid body quaternion accepted");a.bodyQw=.8f;
            bool rejected=false;try{Wire.ReadSnapshot(state.Replace(",1000,",",NaN,").Split('|'),out seq);}catch(FormatException){rejected=true;}Check(rejected,"Non-finite packet accepted");
            var frames=new List<string>();var framer=new LineFramer();byte[] bytes=System.Text.Encoding.ASCII.GetBytes("FIRST\nSECOND\n");framer.Feed(bytes,3,s=>frames.Add(s));byte[] rest=new byte[bytes.Length-3];Array.Copy(bytes,3,rest,0,rest.Length);framer.Feed(rest,rest.Length,s=>frames.Add(s));Check(frames.Count==2&&frames[0]=="FIRST"&&frames[1]=="SECOND","Fragmented Bluetooth framing failed");log.Add("PASS snapshot roundtrip, malformed input rejection and Bluetooth stream framing");
            using(var server=new GameSession(Race()))using(var client=new GameSession(Race()))
            {
                client.localModel=5;var hostSocket=new LanChannel(true,0);server.Host(hostSocket,"Host");var clientSocket=new LanChannel(false);client.Join(clientSocket,"127.0.0.1:"+hostSocket.LocalPort,"Peer");
                for(int i=0;i<80&&!client.connected;i++){client.Tick(.03f);server.Tick(.03f);Thread.Sleep(2);}
                Check(server.connected&&client.connected,"LAN handshake failed");Check(server.race.cars[1].carModel==5,"Remote selected car not applied");Check(server.race.cars[1].driver=="Peer"&&server.race.cars[1].human,"Remote driver was not assigned");
                client.localInput=new DriverInput(.75f,0,.25f);client.pitRequest=true;client.compound=Compound.Hard;
                for(int i=0;i<30;i++){client.Tick(.03f);server.Tick(.03f);Thread.Sleep(2);}
                Check(Math.Abs(server.race.cars[1].input.throttle-.75f)<.01f,"LAN input not applied");Check(server.race.cars[1].pitRequested,"LAN pit request not applied");Check(client.lastSequence>0,"LAN snapshot not received");
                float before=server.race.cars[1].input.throttle;
                int previousInput=server.lastInputSequence;bool malformed=false;try{server.Process("INPUT|"+server.sessionToken+"|9999|NaN|0|0|0|0|0|0",server.peer);}catch(FormatException){malformed=true;}Check(malformed&&server.lastInputSequence==previousInput&&server.race.cars[1].input.throttle==before,"Malformed command partially changed authoritative state");server.Process("INPUT|WRONG|999|1|0|0|0|0|0|0",server.peer);Check(server.race.cars[1].input.throttle==before,"Unauthenticated input accepted");
                server.Process("INPUT|"+server.sessionToken+"|1|1|0|0|0|0|0|0",server.peer);Check(server.race.cars[1].input.throttle==before,"Stale input accepted");
                server.race=Race();server.race.countdown=9999;for(int i=0;i<10;i++){server.Tick(.1f);client.Tick(.1f);Thread.Sleep(2);}Check(!client.race.started&&client.race.countdown>100,"Rematch did not reset the peer lobby state");
                server.race.cars[1].speed=0;client.resetRequest=true;float resetPenalty=server.race.cars[1].penalty;
                for(int i=0;i<30;i++){client.Tick(.03f);server.Tick(.03f);Thread.Sleep(2);}Check(!client.resetRequest&&server.race.cars[1].penalty==resetPenalty,"Acknowledged reset failed or repeated its penalty");
                server.inputSilence=1;server.Tick(.01f);Check(server.race.cars[1].input.throttle==0&&server.race.cars[1].input.brake==1,"Stale remote throttle persisted");
                client.Dispose();server.Tick(.1f);Check(!server.connected,"Disconnect not processed");log.Add("PASS real UDP loopback handshake, remote input, pit request, snapshots, token/replay rejection and disconnect");
            }
            log.Add("PASS "+count+" assertions");return string.Join("\n",log.ToArray());
        }
#if CLI_TEST
        public static void Main(){try{Console.WriteLine(Run());}catch(Exception e){Console.WriteLine("FAIL "+e);Environment.Exit(1);}}
#endif
    }
}
