using System;
using System.Collections.Generic;
using System.Globalization;
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
            // Actual41578470440 regression: five rear-bumper-only impacts were
            // assigned front damage because deceleration points forwards.
            var rearShares=ImpactDamageLocalization.Shares(new[]{"SourceBodyHull_LOD_A_REARBUMPER","SourceBodyHull_LOD_A_REARBUMPER"},0);
            Check(rearShares[1]==1&&rearShares[0]==0,"Rear bumper contact damaged the front or multiplied manifold points");
            var mixedShares=ImpactDamageLocalization.Shares(new[]{"SourceBodyHull_LOD_A_FRONTBUMPER","SourceBodyHull_LOD_A_REARBUMPER","SourceBodyHull_LOD_A_REARBUMPER"},0);
            float shareTotal=0;foreach(float share in mixedShares)shareTotal+=share;
            Check(mixedShares[0]==.5f&&mixedShares[1]==.5f&&shareTotal==1,"Mixed bumper event did not conserve total damage");
            Check(ImpactDamageLocalization.Shares(new[]{"Unknown"},4)[4]==1&&ImpactDamageLocalization.Shares(Array.Empty<string>(),6)[6]==1,"Unknown contact lost explicit fallback region");

            var seriesTracks=new[]{0,1,0};var series=new RaceSeries(seriesTracks,0,2);seriesTracks[0]=3;Check(series.CurrentTrack==0,"Series aliases mutable setup input");series.FinishRound(2);Check(series.CurrentTrack==1&&series.Places[0]==2,"Series did not advance/preserve result");var resumedSeries=RaceSeries.Restore(series.Save());Check(resumedSeries.Round==1&&resumedSeries.CurrentTrack==1&&resumedSeries.Car==0&&resumedSeries.Laps==2&&resumedSeries.Places[0]==2,"Series resume lost configuration/progress");series.FinishRound(1);Check(series.CurrentTrack==0,"Repeated track round lost");series.FinishRound(3);Check(series.Complete&&series.Places.Count==3,"Series completion incorrect");var retiredSeries=new RaceSeries(new[]{0,1},16,1);retiredSeries.FinishRetiredRound();var restoredRetiredSeries=RaceSeries.Restore(retiredSeries.Save());Check(restoredRetiredSeries.Round==1&&restoredRetiredSeries.Places[0]==0&&restoredRetiredSeries.CurrentTrack==1,"Series lost DNF or incorrectly classified it");bool extraRound=false;try{series.FinishRound(1);}catch(InvalidOperationException){extraRound=true;}Check(extraRound,"Completed series accepted another result");

            foreach(int rate in new[]{30,60,120,240}){
                var keys=new SteeringInput();for(int i=0;i<rate;i++)keys.Step(1,1f/rate,false);
                Check(Math.Abs(keys.Command-1)<.00001f,"Digital input lost full steering authority");
                var pad=new SteeringInput();Check(pad.Step(1,1f/rate,true)==keys.Command,"Device-dependent full-lock command");
                for(int i=0;i<rate;i++)keys.Step(0,1f/rate,false);
                Check(Math.Abs(keys.Command)<.00001f,"Released steering stayed held");
                for(int i=0;i<rate;i++)keys.Step(-1,1f/rate,false);
                Check(Math.Abs(keys.Command+1)<.00001f,"Opposite steering lost full authority");
            }
            for(int carIndex=0;carIndex<24;carIndex++)foreach(float intent in new[]{-1f,1f}){
                var turning=new Vehicle {carModel=carIndex,human=true,onDrivingSurface=true,velocity=new Vec(0,8),speed=8,input=new DriverInput(.1f,0,SteeringInput.ToSimulation(intent))};
                var flatRoad=new Circuit(0);for(int step=0;step<120;step++)Dynamics.Step(turning,1f/120f,0,flatRoad);
                Check(turning.position.x*intent<-.01f,"Human steering turns opposite the screen-right convention, car "+carIndex);
            }
            var weatherPlanner=new Vehicle();
            Check(Math.Abs(weatherPlanner.PlanningWeatherGrip(0)-1)<.00001f,"dry planner changed");
            float previousWeatherGrip=1;
            for(int wi=0;wi<=10;wi++){float grip=weatherPlanner.PlanningWeatherGrip(wi/10f);Check(float.IsFinite(grip)&&grip<=previousWeatherGrip+.00001f,"slick wet planning grip is nonfinite or rises with water");previousWeatherGrip=grip;}
            Check(Math.Abs(previousWeatherGrip-.38f)<.00001f,"slick planner ignores tyre weather loss");
            foreach(var planningTyre in weatherPlanner.tyres)planningTyre.Replace(Compound.Wet);
            Check(Math.Abs(weatherPlanner.PlanningWeatherGrip(1)-1)<.00001f,"wet tyres received an unvalidated planning bonus");
            weatherPlanner.tyres[0].Replace(Compound.Soft);
            Check(Math.Abs(weatherPlanner.PlanningWeatherGrip(1)-.38f)<.00001f,"planner ignored weakest mixed tyre");
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
            var tyreOnlyStop=new Vehicle {servicing=true,serviceRemaining=.001f,externalPhysics=true,damage=.5f};tyreOnlyStop.damageRegions[0]=.5f;tyreOnlyStop.Step(new Circuit(0),.01f,0,true,1,1);Check(tyreOnlyStop.damage==.5f&&tyreOnlyStop.damageRegions[0]==.5f,"Tyre-only stop silently repaired bodywork");
            var repairTrack=new Circuit(0);var repairStop=new Vehicle {externalPhysics=true,human=true,pitActive=true,position=repairTrack.PitBox,repairRequested=true,damage=.8f,nextCompound=Compound.Wet};repairStop.damageRegions[1]=.8f;repairStop.Step(repairTrack,.01f,0,true,1,1);Check(repairStop.servicing&&Math.Abs(repairStop.serviceDuration-PitServiceTimeline.Duration-PitServiceTimeline.RepairDuration)<.0001f&&repairStop.serviceRemaining>PitServiceTimeline.Duration,"Repair did not extend the actual service clock");repairStop.serviceRemaining=PitServiceTimeline.RepairDuration-.01f;repairStop.Step(repairTrack,.01f,0,true,2,1,true,false);Check(repairStop.tyresReplaced&&repairStop.tyres[0].compound==Compound.Wet&&repairStop.damage==.8f&&repairStop.ServiceStage=="REPAIR BODYWORK","Tyres/repair stages incorrectly combined");repairStop.serviceRemaining=.001f;repairStop.Step(repairTrack,.01f,0,true,3,1,true,false);Check(repairStop.servicing&&repairStop.bodyRepaired&&repairStop.damage==0&&repairStop.damageRegions[1]==0,"Completed repair or unsafe-release hold lost consistency");repairStop.Step(repairTrack,.01f,0,true,4,1,true,true);Check(!repairStop.servicing&&repairStop.pitDone,"Completed repair did not release safely");log.Add("PASS explicit pit repair duration, tyre-only damage preservation, staged completion and safe release");
            var classification=new Race(0,1,0){started=true,clock=130};var finisher=classification.Add("Finished",true,Compound.Medium);var dnf=classification.Add("Stopped",true,Compound.Medium);finisher.finished=true;finisher.finishTime=10;classification.Step(.01f);Check(dnf.retired&&classification.Classified&&classification.Standings()[0]==finisher,"Finish window did not classify non-finishers");
            var collision=Race();var a=collision.cars[0];var b=collision.cars[1];a.heading=0;b.heading=0;a.position=new Vec(0,0);b.position=new Vec(1,0);a.velocity=new Vec(10,0);b.velocity=new Vec();collision.Collisions();Check((b.position-a.position).Magnitude>=2,"Collision failed to separate vehicles");Check(a.damage>0&&b.damage>0,"Collision did not apply damage");log.Add("PASS collision separation and damage");
            a.carModel=3;a.hasBodyPose=true;a.bodyHeight=42.125f;a.bodyQx=0;a.bodyQy=.6f;a.bodyQz=0;a.bodyQw=.8f;string state=Wire.Snapshot(collision,9);int seq;var copy=Wire.ReadSnapshot(state.Split('|'),out seq);Check(copy.cars[0].carModel==3,"Car model snapshot mismatch");Check(seq==9&&copy.cars.Count==6,"Snapshot count changed");Check(Math.Abs(copy.cars[0].position.x-a.position.x)<.002f,"Snapshot pose changed");
            Check(copy.cars[0].hasBodyPose&&Math.Abs(copy.cars[0].bodyHeight-42.125f)<.002f&&Math.Abs(copy.cars[0].bodyQy-.6f)<.002f&&Math.Abs(copy.cars[0].bodyQw-.8f)<.002f,"Full native body pose lost in snapshot");
            a.damage=.4f;a.repairRequested=true;a.repairForService=true;a.serviceDuration=PitServiceTimeline.Duration+PitServiceTimeline.RepairDuration;a.damageRegions[1]=.4f;a.ersCharge=.35f;a.ersAuto=false;a.retirementReason="CRASH DAMAGE";var extended=Wire.ReadSnapshot(Wire.Snapshot(collision,11).Split('|'),out seq).cars[0];Check(extended.repairRequested&&extended.repairForService&&Math.Abs(extended.serviceDuration-a.serviceDuration)<.001f&&Math.Abs(extended.damageRegions[1]-.4f)<.001f&&Math.Abs(extended.ersCharge-.35f)<.001f&&!extended.ersAuto&&extended.retirementReason==a.retirementReason,"Service/damage/battery snapshot lost actual state");var legacyPacket=Wire.Snapshot(collision,12).Split('|');for(int packet=8;packet<legacyPacket.Length;packet++)legacyPacket[packet]=string.Join(",",legacyPacket[packet].Split(','),0,49);var legacyRace=Wire.ReadSnapshot(legacyPacket,out seq);Check(legacyRace.cars.Count==collision.cars.Count,"Legacy snapshot reading regressed");
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
            // Controlled continuous-driving validation scaffolding: the data files must parse,
            // every referenced profile/metric must exist, cross-references must resolve, and the
            // metric maths must be honest against synthetic telemetry. These checks run headless
            // on every push; the actual driving runs happen on CI against the native engine
            // (data/native_driving_validation_*.json, dispatched by the coordinator).
            var validationProfiles=DrivingValidation.ParseProfiles(System.IO.File.ReadAllText("data/native_driving_validation_profiles.json"));
            var validationProfileIds=new HashSet<string>();foreach(var vp in validationProfiles)validationProfileIds.Add(vp.Id);
            Check(validationProfileIds.IsSupersetOf(new[]{"input_direction","acceleration_0_60","braking_60_0","braking_60_wet","steering_rate_step","straight_stability","edge_excursion","impact_wall_recovery"}),"Driving validation profiles are missing core scenarios");
            foreach(var manifestPath in new[]{"data/native_driving_validation_core.json","data/native_driving_validation_wet.json","data/native_driving_validation_impact.json"}){
                var manifest=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(manifestPath)).RootElement;
                var seenCases=new HashSet<string>();
                foreach(var c in manifest.GetProperty("cases").EnumerateArray()){
                    int caseCar=c.GetProperty("car").GetInt32(),caseTrack=c.GetProperty("track").GetInt32();
                    Check(seenCases.Add(caseCar+"/"+caseTrack),"Validation manifest "+manifestPath+" repeats a car/track case; one case per car and track per manifest");
                    if(c.TryGetProperty("weather",out var weather))Check(weather.GetInt32()>=0&&weather.GetInt32()<=3,"Validation manifest "+manifestPath+" has an out-of-range weather profile");
                    if(c.TryGetProperty("compound",out var compound))Check(compound.GetInt32()>=0&&compound.GetInt32()<=3,"Validation manifest "+manifestPath+" has an out-of-range compound");
                    foreach(var id in c.GetProperty("drivingValidation").EnumerateArray())Check(validationProfileIds.Contains(id.GetString()),"Validation manifest "+manifestPath+" references unknown profile "+id.GetString());
                    string lastProfile=null;bool sawImpact=false;
                    foreach(var id0 in c.GetProperty("drivingValidation").EnumerateArray()){var profileName=id0.GetString();if(profileName=="impact_wall_recovery")sawImpact=true;lastProfile=profileName;}
                    Check(lastProfile=="impact_wall_recovery"||!sawImpact,"Impact profile must run last so earlier profiles keep undamaged state");
                }
            }
            var wetMatrix=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("data/native_wet_comparison_matrix.json")).RootElement;
            var matrixCells=new HashSet<string>();int matrixCellCount=0;
            foreach(var c in wetMatrix.GetProperty("cases").EnumerateArray()){
                matrixCellCount++;
                int caseCar=c.GetProperty("car").GetInt32(),caseTrack=c.GetProperty("track").GetInt32(),weather=c.GetProperty("weather").GetInt32(),compound=c.GetProperty("compound").GetInt32();
                Check(weather>=0&&weather<=3&&compound>=0&&compound<=3,"Wet comparison matrix has an out-of-range weather/compound cell");
                Check(weather!=2||compound!=1,"Wet comparison matrix must not re-run the completed wet-slick cell (run 41625514977)");
                Check(matrixCells.Add(caseCar+"/"+caseTrack+"/"+weather+"/"+compound),"Wet comparison matrix repeats a cell");
            }
            Check(matrixCellCount>=6,"Wet comparison matrix lost coverage");
            Check(!matrixCells.Contains("16/0/2/1")&&!matrixCells.Contains("0/0/2/1"),"Completed wet-slick cells must stay outside the executable case list");
            {
                var wallManifest=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("data/native_driving_validation_wall.json")).RootElement;
                var wallSeen=new HashSet<string>();string lastWall=null;
                foreach(var c in wallManifest.GetProperty("cases").EnumerateArray()){
                    int caseCar=c.GetProperty("car").GetInt32(),caseTrack=c.GetProperty("track").GetInt32();
                    Check(wallSeen.Add(caseCar+"/"+caseTrack),"Wall manifest repeats a car/track case");
                    foreach(var idW in c.GetProperty("drivingValidation").EnumerateArray()){var nm=idW.GetString();Check(validationProfileIds.Contains(nm),"Wall manifest references unknown profile "+nm);lastWall=nm;}
                }
                Check(lastWall=="impact_wall_recovery","Wall manifest must end with impact_wall_recovery (the DV impact-flag contract: DRIVING_VALIDATION_IMPACT must match the manifest)");
            }
            {
                // The wet/night matrix pair (the D0/N/W cells): the manifests must carry
                // explicit drivingValidation lists (ParseCaseProfiles fails closed without
                // them - the c677943 pair killed every matrix dispatch at boot), the dry
                // file stays on the dry default, and the wet file pins its weather contract
                // (requiresWetWeather: the tool's name-based wet assert cannot express the
                // wetnight_dry file's intent - "wetnight" contains "wet").
                var dryNight=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("data/native_wetnight_matrix_dry.json")).RootElement;
                var wetNight=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("data/native_wetnight_matrix_wet.json")).RootElement;
                foreach(var c in dryNight.GetProperty("cases").EnumerateArray()){
                    Check(c.TryGetProperty("drivingValidation",out var dvD)&&dvD.GetArrayLength()>0,"The wetnight DRY manifest lost its drivingValidation lists");
                    Check(!c.TryGetProperty("weather",out var wD)||wD.GetInt32()==0,"The wetnight DRY manifest must stay on the dry default");
                    foreach(var id in dvD.EnumerateArray())Check(validationProfileIds.Contains(id.GetString()),"The wetnight DRY manifest references unknown profile "+id.GetString());
                }
                Check(wetNight.TryGetProperty("requiresWetWeather",out var rww)&&rww.GetBoolean(),"The wetnight WET manifest must pin requiresWetWeather");
                foreach(var c in wetNight.GetProperty("cases").EnumerateArray()){
                    Check(c.GetProperty("weather").GetInt32()==2&&c.GetProperty("compound").GetInt32()==3,"The wetnight WET manifest must keep weather 2 + compound 3");
                    Check(c.TryGetProperty("drivingValidation",out var dvW)&&dvW.GetArrayLength()>0,"The wetnight WET manifest lost its drivingValidation lists");
                    foreach(var id in dvW.EnumerateArray())Check(validationProfileIds.Contains(id.GetString()),"The wetnight WET manifest references unknown profile "+id.GetString());
                }
            }
            {
                var forceProfile=new DrivingValidation.Profile{Id="force"};
                var fs=new List<DrivingValidation.Sample>();
                const float sdt=1f/240f;
                fs.Add(new DrivingValidation.Sample{phaseId="aim",t=0,longitudinalSpeed=10,lateralSpeed=0,nonSurfaceContact=false,loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                fs.Add(new DrivingValidation.Sample{phaseId="aim",t=sdt,longitudinalSpeed=7,lateralSpeed=1.5,nonSurfaceContact=true,loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                fs.Add(new DrivingValidation.Sample{phaseId="aim",t=2*sdt,longitudinalSpeed=6.5,lateralSpeed=1.2,nonSurfaceContact=true,loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                var forceRefs=new Dictionary<string,double>{{"vehicleMass",690}};
                var jump=DrivingValidation.ComputeMetric("impactTickPeakDeltaV","aim",fs,forceProfile,forceRefs);
                Check(jump.HasValue&&Math.Abs(jump.Value-Math.Sqrt(9+2.25))<0.01,"Per-tick velocity-jump metric is wrong on synthetic wall-contact data");
                var force=DrivingValidation.ComputeMetric("impactPeakForceEstimate","aim",fs,forceProfile,forceRefs);
                Check(force.HasValue&&Math.Abs(force.Value-690*Math.Sqrt(11.25)/sdt)<1.0,"Force estimate is wrong on synthetic wall-contact data");
                var dwell=DrivingValidation.ComputeMetric("impactContactSeconds","aim",fs,forceProfile,forceRefs);
                Check(dwell.HasValue&&Math.Abs(dwell.Value-2*sdt)<0.001,"Contact dwell is wrong on synthetic wall-contact data");
                bool noRef=false;try{DrivingValidation.ComputeMetric("impactImpulseEstimate","aim",fs,forceProfile,new Dictionary<string,double>());}catch(Exception){noRef=true;}
                Check(noRef,"Impact impulse estimate must fail closed without the vehicleMass reference");
                var fsBand=new List<DrivingValidation.Sample>();
                fsBand.Add(new DrivingValidation.Sample{phaseId="aim",t=0,speed=10,nonSurfaceContact=false,wheelBandContact=false,loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                fsBand.Add(new DrivingValidation.Sample{phaseId="aim",t=sdt,speed=10,nonSurfaceContact=true,wheelBandContact=true,loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                var band=DrivingValidation.ComputeMetric("wheelBandContactSamples","all",fsBand,forceProfile,forceRefs);
                Check(band.HasValue&&band.Value==1,"Wheel-band contact samples must count only the band rows on synthetic data");
            }
            var handlingTargets=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("data/native_handling_targets.json")).RootElement;
            int handlingTargetCount=0;
            foreach(var t in handlingTargets.GetProperty("targets").EnumerateArray()){
                handlingTargetCount++;
                Check(!string.IsNullOrEmpty(t.GetProperty("id").GetString())&&!string.IsNullOrEmpty(t.GetProperty("metric").GetString())&&!string.IsNullOrEmpty(t.GetProperty("profile").GetString()),"Handling target is missing id/metric/profile");
                Check(validationProfileIds.Contains(t.GetProperty("profile").GetString()),"Handling target references unknown validation profile");
            }
            Check(handlingTargetCount>=6,"Handling targets lost coverage");
            var synthetic=new DrivingValidation.Profile{Id="synthetic"};
            var syntheticSamples=new List<DrivingValidation.Sample>();
            for(int i=0;i<=300;i++){
                double t=i/100.0,v=Math.Max(0,27-9*t);
                syntheticSamples.Add(new DrivingValidation.Sample{phaseId="brake",t=t,speed=v,longitudinalSpeed=v,x=27*t-4.5*t*t,z=0,trackLateral=0,heading=0,yawRate=0,upY=1,groundedMask=15,loads=new double[4],springLengths=new double[4],lateralSlipAngle=new double[4],longitudinalSlip=new double[4]});
            }
            var syntheticDistance=DrivingValidation.ComputeMetric("pathLength","brake",syntheticSamples,synthetic,null);
            Check(syntheticDistance.HasValue&&Math.Abs(syntheticDistance.Value-40.5)<0.2,"Brake distance metric diverges from constant-deceleration kinematics");
            var syntheticEnd=DrivingValidation.ComputeMetric("endSpeed","brake",syntheticSamples,synthetic,null);
            Check(syntheticEnd.HasValue&&Math.Abs(syntheticEnd.Value)<0.01,"End-speed metric is wrong on a synthetic full stop");
            var syntheticAccel=DrivingValidation.ComputeMetric("longitudinalAccelMin","brake",syntheticSamples,synthetic,null);
            Check(syntheticAccel.HasValue&&Math.Abs(syntheticAccel.Value+9)<0.02,"Decel metric is wrong on constant deceleration");
            var syntheticReference=new Dictionary<string,double>{{"brakeStop60Metres",40.0}};
            var inBand=new DrivingValidation.Assertion{Id="in_band",Metric="pathLength:brake",Op="betweenReference",Reference="brakeStop60Metres",FactorMin=0.8,FactorMax=1.2};
            Check(DrivingValidation.Evaluate(inBand,syntheticSamples,synthetic,syntheticReference).Passed,"Reference-banded assertion rejected matching synthetic data");
            var outOfBand=new DrivingValidation.Assertion{Id="out_band",Metric="pathLength:brake",Op="betweenReference",Reference="brakeStop60Metres",FactorMin=0.9,FactorMax=0.95};
            Check(!DrivingValidation.Evaluate(outOfBand,syntheticSamples,synthetic,syntheticReference).Passed,"Out-of-band synthetic data passed a reference assertion");
            var missing=new DrivingValidation.Assertion{Id="missing",Metric="pathLength:empty_phase",Op="lt",Value=1,HasValue=true};
            Check(!DrivingValidation.Evaluate(missing,new List<DrivingValidation.Sample>(),synthetic,syntheticReference).Passed,"A metric over absent data must fail, not pass");
            bool rejectedConfig=false;try{DrivingValidation.ParseProfiles("{\"profiles\":[{\"id\":\"x\",\"phases\":[{\"id\":\"a\",\"forSeconds\":1}],\"assertions\":[{\"id\":\"b\",\"metric\":\"bogus_metric:a\",\"op\":\"lt\",\"value\":1}]}]}");}catch(Exception){rejectedConfig=true;}
            Check(rejectedConfig,"An unknown metric name passed profile parsing");
            rejectedConfig=false;try{DrivingValidation.ParseProfiles("{\"profiles\":[{\"id\":\"x\",\"phases\":[{\"id\":\"a\",\"forSeconds\":1}],\"assertions\":[{\"id\":\"b\",\"metric\":\"pathLength:ghost\",\"op\":\"lt\",\"value\":1}]}]}");}catch(Exception){rejectedConfig=true;}
            Check(rejectedConfig,"An assertion referencing a missing phase passed profile parsing");
            rejectedConfig=false;try{DrivingValidation.ParseProfiles("{\"profiles\":[{\"id\":\"x\",\"phases\":[{\"id\":\"a\"}],\"assertions\":[]}]}");}catch(Exception){rejectedConfig=true;}
            Check(rejectedConfig,"A phase without termination passed profile parsing");
            rejectedConfig=false;try{DrivingValidation.ParseCaseProfiles("{\"cases\":[{\"car\":1,\"track\":2}]}",1,2);}catch(Exception){rejectedConfig=true;}
            Check(rejectedConfig,"A case without drivingValidation profiles passed manifest parsing");
            // Scripted-command state machine: phases advance only when their own condition is met.
            var stateMachine=new DrivingValidation.Profile{Id="state_machine"};
            stateMachine.Phases.Add(new DrivingValidation.Phase{Id="accelerate",Throttle=1,UntilSpeedAtLeast=10});
            stateMachine.Phases.Add(new DrivingValidation.Phase{Id="brake",Brake=1,UntilSpeedBelow=1});
            var stateRun=new DrivingValidation.Run(stateMachine,new Dictionary<string,double>());
            stateRun.Start();Check(stateRun.Current.Throttle==1&&stateRun.Current.Brake==0,"Scenario did not start in its first phase");
            var march=new List<DrivingValidation.Sample>();
            for(int i=0;i<3000;i++){double t=i/100.0;double v=t<2?5*t:10-10*(t-2);if(v<0)v=0;
                stateRun.Step(new DrivingValidation.Sample{t=t,speed=v,longitudinalSpeed=v,x=0,z=0,trackLateral=0,heading=0,upY=1,groundedMask=15,loads=new double[4],springLengths=new double[4],lateralSlipAngle=new double[4],longitudinalSlip=new double[4]});
                if(stateRun.Completed)break;}
            Check(stateRun.Completed&&stateRun.Passed,"Scripted acceleration/brake state machine did not complete cleanly");
            Check(stateRun.Timeline.Count>=290,"State machine consumed phases without recording samples");
            // Turn-8 steering-feel regression guards: measured values before/after the smoothing fix
            // (attack 2.6/s -> 5.0/s). The human input chain (SteeringInput + the 3.2 rad/s actuator
            // shared with the native wheel-angle path) must keep correction-frequency authority:
            // a 1 Hz correction sine peaks >= 0.9 command (measured 1.0; 0.746 before the fix), a
            // 2 Hz sine >= 0.55 (measured 0.629; 0.407 before), full-lock front-wheel angle at
            // 30 m/s within 0.30 s of a keyboard step (measured ~0.19 s; ~0.55 s before), the
            // release decay within 0.30 s (measured 0.192), and perfect left/right symmetry.
            {
                const float dt=1f/240f;
                foreach(float hz in new[]{1f,2f}){
                    var keys=new SteeringInput();double peak=0;
                    for(int i=0;i<(int)(8/dt);i++){float t=i*dt;var c=keys.Step(MathF.Sin(2f*MathF.PI*hz*t),dt,false);if(t>4)peak=Math.Max(peak,Math.Abs((double)c));}
                    Check(peak>=(hz==1f?0.9:0.55),"Steering chain lost correction authority at "+hz+" Hz (peak "+peak.ToString("0.000",CultureInfo.InvariantCulture)+"); measured bands: 1Hz>=0.9, 2Hz>=0.55");
                }
                {
                    var keys=new SteeringInput();float angle0=0;float t95=-1;
                    for(int i=0;i<(int)(3/dt);i++){float t=i*dt;var c=keys.Step(1,dt,false);var target=Dynamics.SteeringTarget(c,30);angle0=target>angle0?MathF.Min(angle0+3.2f*dt,target):MathF.Max(angle0-3.2f*dt,target);if(t95<0&&MathF.Abs(angle0)>=0.95f*Dynamics.SteeringLock(30))t95=t;}
                    Check(t95>=0&&t95<=0.30,"Full-lock front-wheel angle too slow at 30 m/s (t95 "+t95.ToString("0.000",CultureInfo.InvariantCulture)+" s; guard 0.30 s, measured 0.19)");
                    var rel=new SteeringInput();for(int i=0;i<(int)(1/dt);i++)rel.Step(1,dt,false);float angle=Dynamics.SteeringTarget(1,30);float trel=-1;
                    for(int i=0;i<(int)(3/dt);i++){float t=i*dt;var c=rel.Step(0,dt,false);var target=Dynamics.SteeringTarget(c,30);angle=target>angle?MathF.Min(angle+3.2f*dt,target):MathF.Max(angle-3.2f*dt,target);if(trel<0&&MathF.Abs(angle)<=0.05f*Dynamics.SteeringTarget(1,30))trel=t;}
                    Check(trel>=0&&trel<=0.30,"Released steering decays too slowly at 30 m/s (t95 "+trel.ToString("0.000",CultureInfo.InvariantCulture)+" s; guard 0.30 s, measured 0.19)");
                }
            }
            log.Add("PASS steering-feel guards: correction authority, lock reach time, release decay and symmetry (turn-8 measured bands)");
            // Userpath chain state hygiene: profile-boundary resets must clear the smoothing/latch state
            // and the human reverse-pedal semantics (brake = the reverse pedal once latched) must engage.
            {
                var r1=new SteeringInput();r1.Step(1,1f/60,false);r1.Reset();Check(Math.Abs(r1.Command)<1e-6,"SteeringInput.Reset left a residual command");
                var p1=new PedalController();DriverInput d1=new DriverInput();bool latched=false;
                for(int i=0;i<40;i++){d1=p1.Update(0,1,0,false,0,1f/120,true);if(d1.reverse)latched=true;}
                Check(latched,"Brake-to-reverse latch never engaged holding the brake from standstill");
                p1.Reset();var d2=p1.Update(0,0,0,false,0,1f/120,true);
                Check(!d2.reverse&&p1.Reversing==false,"PedalController.Reset left the reverse latch armed");
            }
            // R-window recovery forensics: windows open on impact-counter rises and close when
            // motion (speed magnitude) holds at MotionSpeed for HoldSeconds; a profile without
            // the recordRecovery flag reports null metrics (fail-closed, never a passing zero).
            {
                var recSpec=new DrivingValidation.RecoverySpec{Enabled=true,MotionSpeed=1,HoldSeconds=0.5,MaxSeconds=10};
                var recProfile=new DrivingValidation.Profile{Id="rec",Recovery=recSpec};
                var rs=new List<DrivingValidation.Sample>();const float rdt=1f/240f;
                // t=0: 5 m/s; impact rise at t=rdt (speed 2); stalled at 0.2 until t=0.5; then a
                // reverse-out (speed 2.5, longitudinal -2.5) which holds past the 0.5 s hold.
                for(int i=0;i<1200;i++){double t=i*rdt;double sp=t<rdt?5:t<2*rdt?2:(t<0.5?0.2:2.5);
                    rs.Add(new DrivingValidation.Sample{t=t,speed=sp,longitudinalSpeed=t<0.5?sp:-sp,x=t<0.5?0:(t-0.5)*2.5,z=0,heading=0,upY=1,groundedMask=15,impactEvents=i==0?0:1,onDrivingSurface=i>=2,throttle=t>=0.5?1:0,phaseId="recover_fwd",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});}
                var windows=DrivingValidation.RecoveryWindows(rs,recSpec);
                Check(windows.Count==1,"Recovery windows did not open exactly on the impact rise (got "+windows.Count+")");
                Check(windows[0].Recovered&&windows[0].motionTime.HasValue&&Math.Abs(windows[0].motionTime.Value-(1.0-rdt))<2*rdt,"Recovery motion time is wrong (got "+(windows[0].motionTime?.ToString(CultureInfo.InvariantCulture)??"null")+")");
                Check(windows[0].maxReverseSpeed>1.5,"Recovery window missed the reverse-out phase");
                Check(windows[0].pathLength>1.0,"Recovery path length not measured");
                Check(windows[0].throttleFraction>0.4&&windows[0].throttleFraction<0.6&&windows[0].offTrackFraction<0.5,"Recovery input/surface fractions not recorded honestly");
                var maxMotion=DrivingValidation.ComputeMetric("recoveryMotionTimeMax","all",rs,recProfile,null);
                Check(maxMotion.HasValue&&Math.Abs(maxMotion.Value-(1.0-rdt))<2*rdt,"recoveryMotionTimeMax metric maths wrong against the same synthetic window");
                var stuckSpec=new DrivingValidation.RecoverySpec{Enabled=true,MotionSpeed=1,HoldSeconds=0.5,MaxSeconds=2};
                var stuck=new List<DrivingValidation.Sample>();
                for(int i=0;i<720;i++){double t=i*rdt;stuck.Add(new DrivingValidation.Sample{t=t,speed=0.2,impactEvents=i==0?0:1,onDrivingSurface=true,throttle=1,phaseId="recover_fwd",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});}
                var stuckWindows=DrivingValidation.RecoveryWindows(stuck,stuckSpec);
                Check(stuckWindows.Count==1&&!stuckWindows[0].Recovered&&stuckWindows[0].motionTime==null,"A stalled recovery must record an unresolved window (Recovered=false, motionTime null)");
                Check(Math.Abs(stuckWindows[0].endT-stuckWindows[0].startT-2)<2*rdt,"Unresolved recovery window not capped at maxSeconds");
                var offProfile=new DrivingValidation.Profile{Id="off"};
                Check(!DrivingValidation.ComputeMetric("recoveryWindowCount","all",rs,offProfile,null).HasValue,"Recovery metrics must be null without the recordRecovery flag (a silent zero could pass an assertion)");
                var clean=new List<DrivingValidation.Sample>();for(int i=0;i<240;i++)clean.Add(new DrivingValidation.Sample{t=i*rdt,speed=3,onDrivingSurface=true,phaseId="recover_fwd",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                var noImpact=DrivingValidation.ComputeMetric("recoveryWindowCount","all",clean,recProfile,null);
                Check(noImpact.HasValue&&noImpact.Value==0,"A flag-enabled profile with no impacts must report an observed zero window count");
            }
            // Contact-archetype force signatures: a guard-band contact (hub-mounted wheelguard/
            // caliper) and a body-panel contact must be classifiable per tick with per-archetype
            // per-tick velocity jumps; mixed ticks (both bands) feed both signatures and are
            // counted; a profile without the flag reports null (fail-closed, never zero).
            {
                var archProfile=new DrivingValidation.Profile{Id="arch",RecordContactArchetypes=true};
                var arche=new List<DrivingValidation.Sample>();const float adt=1f/240f;
                arche.Add(new DrivingValidation.Sample{t=0,speed=8,longitudinalSpeed=8,lateralSpeed=0,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                arche.Add(new DrivingValidation.Sample{t=adt,speed=5,longitudinalSpeed=4.6,lateralSpeed=-1.9,nonSurfaceContact=true,wheelBandContact=true,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                arche.Add(new DrivingValidation.Sample{t=2*adt,speed=4.9,longitudinalSpeed=4.6,lateralSpeed=-1.85,nonSurfaceContact=true,wheelBandContact=true,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                arche.Add(new DrivingValidation.Sample{t=3*adt,speed=4.8,longitudinalSpeed=4.6,lateralSpeed=-1.2,nonSurfaceContact=true,bodyPanelContact=true,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                arche.Add(new DrivingValidation.Sample{t=4*adt,speed=4.7,longitudinalSpeed=4.6,lateralSpeed=-1.1,nonSurfaceContact=true,bodyPanelContact=true,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                arche.Add(new DrivingValidation.Sample{t=5*adt,speed=4.6,longitudinalSpeed=4.5,lateralSpeed=-0.9,nonSurfaceContact=true,wheelBandContact=true,bodyPanelContact=true,phaseId="aim",loads=new double[4],springLengths=new double[4],longitudinalSlip=new double[4],lateralSlipAngle=new double[4]});
                var guardJump=DrivingValidation.ComputeMetric("wheelBandTickPeakDeltaV","all",arche,archProfile,null);
                Check(guardJump.HasValue&&Math.Abs(guardJump.Value-System.Math.Sqrt(3.4*3.4+1.9*1.9))<0.01,"Guard-band per-tick velocity jump is wrong on synthetic contact data");
                var panelJump=DrivingValidation.ComputeMetric("bodyPanelTickPeakDeltaV","all",arche,archProfile,null);
                Check(panelJump.HasValue&&Math.Abs(panelJump.Value-0.65)<0.01,"Body-panel per-tick velocity jump is wrong on synthetic contact data");
                var panelSamples=DrivingValidation.ComputeMetric("bodyPanelContactSamples","all",arche,archProfile,null);
                Check(panelSamples.HasValue&&panelSamples.Value==3,"Body-panel contact sample count is wrong (mixed tick must count toward the panel too)");
                var mixed=DrivingValidation.ComputeMetric("mixedContactSamples","all",arche,archProfile,null);
                Check(mixed.HasValue&&mixed.Value==1,"Mixed guard+panel contact ticks not counted");
                var bandSeconds=DrivingValidation.ComputeMetric("wheelBandContactSeconds","all",arche,archProfile,null);
                Check(bandSeconds.HasValue&&Math.Abs(bandSeconds.Value-3*adt)<1e-6,"Guard-band contact seconds computed wrongly");
                var offArch=new DrivingValidation.Profile{Id="archoff"};
                Check(!DrivingValidation.ComputeMetric("wheelBandTickPeakDeltaV","all",arche,offArch,null).HasValue&&!DrivingValidation.ComputeMetric("bodyPanelContactSamples","all",arche,offArch,null).HasValue,"Archetype metrics must be null without the recordContactArchetypes flag (a silent zero could pass an assertion)");
            }
            // LAN phase-2: the explicit START handshake (the adopted default) - the host sends
            // START carrying its session token and arms the real 5 s countdown; the client
            // gates its race entry on ITS exact token; wrong-token and pre-handshake STARTs
            // are ignored; a stale-protocol HELLO is refused (the version gate stays strict).
            {
                var channel=new TestChannel();
                var hostRace=new Race(0,3,0);hostRace.Add("HOST",true,Compound.Medium);hostRace.Add("GUEST",false,Compound.Medium);
                var host=new GameSession(hostRace);host.Host(channel,"HOST");
                host.Process("HELLO|"+Wire.Version+"|"+new string('a',32)+"|"+Wire.Name("GUEST")+"|1","1.2.3.4:9");
                Check(host.connected,"LAN host did not accept a valid HELLO");
                host.Process("HELLO|MSR5|"+new string('a',32)+"|"+Wire.Name("GUEST")+"|1","1.2.3.5:9");
                Check(!host.connected||host.peer=="1.2.3.4:9","A stale-protocol HELLO must be refused");
                host.StartRace();
                Check(channel.Sent.Contains("START|"+host.sessionToken),"Host did not send START carrying its session token");
                Check(host.race.countdown==5f&&host.startRequested,"Host start did not arm the 5 s countdown (the lobby's 9999 hold must end)");
                Check(!host.startReceived,"The host must not set the client-side startReceived flag");
                var client=new GameSession(new Race(0,3,0));client.Join(new TestChannel(),"1.2.3.4:9","GUEST");
                client.Process("START|"+client.sessionToken,"1.2.3.4:9");
                Check(!client.startReceived,"START before the handshake completed must be ignored");
                client.Process("ACCEPT|"+Wire.Version+"|"+client.nonce+"|"+new string('b',32),"1.2.3.4:9");
                Check(client.connected,"Client did not accept the host token");
                client.Process("START|wrongtoken","1.2.3.4:9");
                Check(!client.startReceived,"A START with a wrong token must be ignored");
                client.Process("START|"+client.sessionToken,"1.2.3.4:9");
                Check(client.startReceived,"Client did not gate its race entry on the START token");
                // Host-side adoption: the session re-points at the LIVE simulated race (the
                // remote car already added by the race scene from PeerName/PeerModel); the
                // client role can never adopt; a race without the remote car is rejected.
                Check(host.PeerName=="GUEST"&&host.PeerModel==1,"The host did not retain the peer's identity from HELLO");
                var liveRace=new Race(0,3,0);liveRace.Add("HOST",true,Compound.Medium);liveRace.Add("GUEST",true,Compound.Medium);
                host.AdoptRace(liveRace);
                Check(host.RaceAdopted&&host.race==liveRace,"AdoptRace did not re-point the session at the live race");
                bool clientRejected=false;try{client.AdoptRace(liveRace);}catch(InvalidOperationException){clientRejected=true;}
                Check(clientRejected,"A client session must never adopt a race");
                bool thinRejected=false;try{host.AdoptRace(new Race(0,3,0));}catch(ArgumentException){thinRejected=true;}
                if(!thinRejected){host.AdoptRace(liveRace);}
                Check(thinRejected,"A live race without the remote-driven car must be rejected");
            }
            // LAN phase-2: the client pipeline end to end (Snapshot -> ReadSnapshot ->
            // SnapshotApplier) with the persistence rule: one view per car id, updated in
            // place, never recreated per packet; new ids append exactly one view.
            {
                var views=new Dictionary<int,TestCarView>();int raceStates=0;
                var applier=new SnapshotApplier(new TestRaceView(id=>{var v=new TestCarView();views[id]=v;return v;},()=>raceStates++));
                var hostRace=new Race(0,3,0);hostRace.Add("HOST",true,Compound.Medium);hostRace.Add("GUEST",false,Compound.Medium);
                hostRace.cars[0].position=new Vec(10,20);
                applier.Apply(Wire.ReadSnapshot(Wire.Snapshot(hostRace,1).Split('|'),out _));
                applier.Apply(Wire.ReadSnapshot(Wire.Snapshot(hostRace,2).Split('|'),out _));
                Check(views.Count==2&&raceStates==2,"SnapshotApplier must create exactly one view per car and apply race state per snapshot");
                Check(views[0].PoseApplications==2&&views[1].PoseApplications==2,"Existing car views must update in place, never be recreated per packet");
                hostRace.Add("THIRD",false,Compound.Medium);
                applier.Apply(Wire.ReadSnapshot(Wire.Snapshot(hostRace,3).Split('|'),out _));
                Check(views.Count==3&&views[0].PoseApplications==3,"A new car id appends exactly one view; existing views keep updating");
                Check(applier.AppliedSnapshots==3&&applier.CarViews==3,"SnapshotApplier counters drifted");
                Check(applier.ViewFor(1)!=null&&applier.ViewFor(99)==null,"ViewFor must return the persistent view for a known id and null for an unknown one");
            }
            log.Add("PASS driving validation scaffolding: profiles, manifests, matrix, targets, metric maths, recovery windows and fail-closed parsing");
            log.Add("PASS "+count+" assertions");return string.Join("\n",log.ToArray());
        }
        // Test doubles for the LAN phase-2 checks: a recording channel, a counting car view
        // and a counting race view (no Godot types - the real adapters live in the host app).
        sealed class TestChannel:IGameChannel{
            public List<string> Sent=new List<string>();public string Status{get;set;}="test";
            public bool Receive(out string message,out string sender){message=null;sender=null;return false;}
            public void Send(string message,string address){Sent.Add(message);}
            public void Dispose(){}
        }
        sealed class TestCarView:ICarView{
            public int PoseApplications,StateApplications;
            public void EnsureCreated(int carModel,string driver){}
            public void ApplyPose(float x,float z,float heading,bool hasBodyPose,float bodyHeight,float bodyQx,float bodyQy,float bodyQz,float bodyQw){PoseApplications++;}
            public void ApplyState(float damage,float fuel,float speed,int completedLaps,bool finished,float finishTime,bool pitActive,bool servicing,float serviceRemaining){StateApplications++;}
        }
        sealed class TestRaceView:IRaceView{
            readonly Func<int,ICarView> factory;readonly Action onRaceState;
            public TestRaceView(Func<int,ICarView> factory,Action onRaceState){this.factory=factory;this.onRaceState=onRaceState;}
            public ICarView CreateCarView(int id)=>factory(id);
            public void ApplyRaceState(float clock,float countdown,bool started,int trackVariant,int targetLaps,float wetness)=>onRaceState();
        }
#if CLI_TEST
        public static void Main(){try{Console.WriteLine(Run());}catch(Exception e){Console.WriteLine("FAIL "+e);Environment.Exit(1);}}
#endif
    }
}
