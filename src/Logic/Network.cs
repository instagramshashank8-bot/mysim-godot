using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace MySimulator.Rebuild
{
    public interface IGameChannel:IDisposable
    {
        string Status {get;}
        bool Receive(out string message,out string sender);
        void Send(string message,string address);
    }
    public sealed class LanChannel:IGameChannel
    {
        public const int Port=44880;
        UdpClient socket;
        public string Status {get;private set;}
        public int LocalPort {get{return ((IPEndPoint)socket.Client.LocalEndPoint).Port;}}
        public LanChannel(bool host,int port=Port)
        {
            socket=new UdpClient(new IPEndPoint(IPAddress.Any,host?port:0));socket.EnableBroadcast=true;socket.Client.Blocking=false;Status=host?"Hosting on port "+LocalPort:"Ready";
        }
        public bool Receive(out string message,out string sender)
        {
            message=null;sender=null;if(socket==null||socket.Available==0)return false;
            try{IPEndPoint endpoint=new IPEndPoint(IPAddress.Any,0);byte[] data=socket.Receive(ref endpoint);if(data.Length>16000)return false;message=Encoding.UTF8.GetString(data);sender=endpoint.ToString();return true;}
            catch(SocketException e){if(e.SocketErrorCode!=SocketError.WouldBlock)Status=e.SocketErrorCode.ToString();return false;}
        }
        public void Send(string message,string address)
        {
            if(socket==null||string.IsNullOrEmpty(address)||message.Length>16000)return;
            int split=address.LastIndexOf(':');if(split<0)return;
            IPAddress ip;int port;if(!IPAddress.TryParse(address.Substring(0,split),out ip)||!int.TryParse(address.Substring(split+1),out port))return;
            try{byte[] data=Encoding.UTF8.GetBytes(message);socket.Send(data,data.Length,new IPEndPoint(ip,port));}
            catch(SocketException e){Status=e.SocketErrorCode.ToString();}
        }
        public void Discover(){Send("DISCOVER|"+Wire.Version,"255.255.255.255:"+Port);}
        public static string Addresses()
        {
            var result=new List<string>();foreach(var ip in Dns.GetHostAddresses(Dns.GetHostName()))if(ip.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(ip))result.Add(ip.ToString());return string.Join("  /  ",result.ToArray());
        }
        public void Dispose(){if(socket!=null){socket.Close();socket=null;}Status="Closed";}
    }
    public sealed class LineFramer
    {
        readonly StringBuilder pending=new StringBuilder();
        public const int MaxLine=16000;
        public void Feed(byte[] bytes,int count,Action<string> onLine)
        {
            for(int i=0;i<count;i++)
            {
                char c=(char)bytes[i];
                if(c=='\n'){if(pending.Length>0)onLine(pending.ToString());pending.Length=0;}
                else if(c!='\r'){if(pending.Length>=MaxLine){pending.Length=0;throw new FormatException("Bluetooth packet exceeds limit");}pending.Append(c);}
            }
        }
    }
    public static class Wire
    {
        public const string Version="MSR6";
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        public static string F(float f){return f.ToString("0.###",C);}
        public static float Float(string s){float f;if(!float.TryParse(s,NumberStyles.Float,C,out f)||!MathUtil.Finite(f))throw new FormatException("Invalid number");return f;}
        public static int Int(string s){return int.Parse(s,C);}
        public static string Name(string name){return Convert.ToBase64String(Encoding.UTF8.GetBytes((name??"Driver").Substring(0,Math.Min(24,(name??"Driver").Length))));}
        public static string Unname(string name){string result=Encoding.UTF8.GetString(Convert.FromBase64String(name));if(result.Length>24)throw new FormatException("Name too long");return result;}
        public static string Snapshot(Race race,int seq)
        {
            StringBuilder s=new StringBuilder("STATE|"+seq+"|"+F(race.clock)+"|"+F(race.countdown)+"|"+(race.started?1:0)+"|"+race.track.variant+"|"+race.targetLaps+"|"+F(race.wetness));
            foreach(Vehicle v in race.cars)
            {
                s.Append('|');s.Append(v.id+","+Name(v.driver)+","+F(v.position.x)+","+F(v.position.z)+","+F(v.velocity.x)+","+F(v.velocity.z)+","+F(v.heading)+","+F(v.speed)+","+F(v.steering)+","+F(v.rpm)+","+v.gear+","+v.completedLaps+","+v.nextGate+","+F(v.progress)+","+F(v.lateral)+","+F(v.lapElapsed)+","+F(v.lastLap)+","+F(v.bestLap)+","+F(v.damage)+","+F(v.fuel)+","+F(v.penalty)+","+F(v.finishTime)+","+(v.finished?1:0)+","+(v.pitRequested?1:0)+","+(v.pitActive?1:0)+","+(v.servicing?1:0)+","+F(v.serviceRemaining)+","+(int)v.nextCompound);
                foreach(Tyre t in v.tyres)s.Append(","+(int)t.compound+","+F(t.wear)+","+F(t.temperature));s.Append(","+v.carModel+","+(v.retired?1:0)+","+(v.physicalHandling||v.externalPhysics?1:0)+","+(v.hasBodyPose?1:0)+","+F(v.bodyHeight)+","+F(v.bodyQx)+","+F(v.bodyQy)+","+F(v.bodyQz)+","+F(v.bodyQw));s.Append(","+(v.repairRequested?1:0)+","+F(v.serviceDuration)+","+(v.repairForService?1:0));foreach(float region in v.damageRegions)s.Append(","+F(region));s.Append(","+Name(v.retirementReason)+","+F(v.ersCharge)+","+(v.ersAuto?1:0)+","+(v.ersOvertake?1:0)+","+(v.ersActive?1:0)+","+(v.tyresReplaced?1:0)+","+(v.bodyRepaired?1:0));
            }
            return s.ToString();
        }
        public static Race ReadSnapshot(string[] parts,out int sequence)
        {
            if(parts.Length<9||parts.Length>14)throw new FormatException("Invalid race packet");
            sequence=Int(parts[1]);int circuit=Int(parts[5]),laps=Int(parts[6]);if(circuit<0||circuit>=SourceRoutes.Names.Length||laps<1||laps>20)throw new FormatException("Invalid settings");
            Race race=new Race(circuit,laps,MathUtil.Clamp(Float(parts[7]),0,1)){clock=Float(parts[2]),countdown=Float(parts[3]),started=parts[4]=="1"};
            for(int i=8;i<parts.Length;i++)
            {
                string[] p=parts[i].Split(',');if(p.Length!=49&&p.Length!=74)throw new FormatException("Invalid car packet");int id=Int(p[0]);if(id!=i-8)throw new FormatException("Invalid car id");
                if(p[43]!="0"&&p[43]!="1")throw new FormatException("Invalid body pose state");
                if((p[42]!="0"&&p[42]!="1")||(p[41]!="0"&&p[41]!="1"))throw new FormatException("Invalid retirement state");int model=Int(p[40]);if(model<0||model>=CarCatalog.Count)throw new FormatException("Invalid car model");
                int nextCompound=Int(p[27]);if(nextCompound<0||nextCompound>3)throw new FormatException("Invalid next compound");
                var v=new Vehicle{id=id,carModel=model,retired=p[41]=="1",physicalHandling=p[42]=="1",driver=Unname(p[1]),position=new Vec(Float(p[2]),Float(p[3])),velocity=new Vec(Float(p[4]),Float(p[5])),heading=Float(p[6]),speed=Float(p[7]),steering=Float(p[8]),rpm=Float(p[9]),gear=Int(p[10]),completedLaps=Int(p[11]),nextGate=Int(p[12]),progress=Float(p[13]),lateral=Float(p[14]),lapElapsed=Float(p[15]),lastLap=Float(p[16]),bestLap=Float(p[17]),damage=Float(p[18]),fuel=Float(p[19]),penalty=Float(p[20]),finishTime=Float(p[21]),finished=p[22]=="1",pitRequested=p[23]=="1",pitActive=p[24]=="1",servicing=p[25]=="1",serviceRemaining=Float(p[26]),nextCompound=(Compound)Int(p[27])};
                for(int k=0;k<4;k++){int compound=Int(p[28+k*3]);if(compound<0||compound>3)throw new FormatException("Invalid compound");v.tyres[k].compound=(Compound)compound;v.tyres[k].wear=MathUtil.Clamp(Float(p[29+k*3]),0,1);v.tyres[k].temperature=MathUtil.Clamp(Float(p[30+k*3]),0,170);}
                v.hasBodyPose=p[43]=="1";v.bodyHeight=Float(p[44]);v.bodyQx=Float(p[45]);v.bodyQy=Float(p[46]);v.bodyQz=Float(p[47]);v.bodyQw=Float(p[48]);float norm=v.bodyQx*v.bodyQx+v.bodyQy*v.bodyQy+v.bodyQz*v.bodyQz+v.bodyQw*v.bodyQw;if(Math.Abs(v.bodyHeight)>5000||(v.hasBodyPose&&(norm<.9f||norm>1.1f)))throw new FormatException("Invalid body pose");
                if(p.Length==74){foreach(int k in new[]{49,51,69,70,71,72,73})if(p[k]!="0"&&p[k]!="1")throw new FormatException("Invalid extended car state");v.repairRequested=p[49]=="1";v.serviceDuration=Float(p[50]);v.repairForService=p[51]=="1";if(v.serviceDuration<PitServiceTimeline.Duration||v.serviceDuration>300||v.serviceRemaining<0||v.serviceRemaining>v.serviceDuration+.01f)throw new FormatException("Invalid service clock");for(int k=0;k<15;k++){v.damageRegions[k]=Float(p[52+k]);if(v.damageRegions[k]<0||v.damageRegions[k]>1)throw new FormatException("Invalid damage region");}v.retirementReason=Unname(p[67]);if(v.retirementReason.Length>80)throw new FormatException("Invalid retirement reason");v.ersCharge=Float(p[68]);if(v.ersCharge<0||v.ersCharge>1)throw new FormatException("Invalid battery state");v.ersAuto=p[69]=="1";v.ersOvertake=p[70]=="1";v.ersActive=p[71]=="1";v.tyresReplaced=p[72]=="1";v.bodyRepaired=p[73]=="1";}
                if(Math.Abs(v.position.x)>5000||Math.Abs(v.position.z)>5000)throw new FormatException("Position out of bounds");race.cars.Add(v);
            }return race;
        }
    }
    public enum SessionRole{Offline,Host,Client}
    public sealed class GameSession:IDisposable
    {
        public SessionRole role;
        public IGameChannel channel;
        public Race race;
        public bool connected;
        public string status="Offline",peer,PeerName="",peerNonce="",name="Driver",sessionToken="",nonce=Guid.NewGuid().ToString("N");
        public int localModel,localId,sequence,lastSequence=-1,inputSequence,lastInputSequence=-1,PeerModel;
        public float silence,sendTimer,joinTimer,inputSilence;bool remoteResetHeld;
        // The adopted LAN plan's explicit start handoff: the host sends START|token when the
        // room launches; the client gates its race-scene entry on receiving its exact token.
        public bool startRequested,startReceived;
        // Set by AdoptRace: the host's live simulated race replaced the lobby race; the
        // hosting UI stops ticking the session at this point (the drive controller took over).
        public bool RaceAdopted;
        public readonly Dictionary<string,string> rooms=new Dictionary<string,string>();
        public DriverInput localInput;
        public bool pitRequest,resetRequest;
        public Compound compound;
        public GameSession(Race race){this.race=race;role=SessionRole.Offline;localId=0;localModel=race.cars.Count>0?race.cars[0].carModel:0;}
        public void Host(IGameChannel transport,string driver)
        {
            channel=transport;role=SessionRole.Host;name=driver;sessionToken=Guid.NewGuid().ToString("N");race.countdown=9999;status="Waiting for a second driver";
        }
        public void Join(IGameChannel transport,string address,string driver)
        {channel=transport;role=SessionRole.Client;peer=address;name=driver;localId=1;status="Connecting";joinTimer=2;silence=0;}
        // Host-side race start (the adopted LAN plan's explicit START): arms the real 5 s
        // countdown (the lobby holds countdown at 9999 so the race-start semantics stay the
        // single-player ones) and tells the client to enter its race scene. The client gates
        // its transition on receiving START with its exact session token.
        public void StartRace()
        {
            if(role!=SessionRole.Host||!connected)throw new InvalidOperationException("StartRace requires a connected host session");
            if(race.started)throw new InvalidOperationException("Race already started");
            race.countdown=5f;startRequested=true;channel.Send("START|"+sessionToken,peer);
        }
        // Host-side adoption of the live race: after the race scene is built, the session
        // snapshots THE SIMULATED race (the lobby race object is discarded) and the INPUT
        // handler's cars[1] writes land on the live remote-driven car. The remote car must
        // already exist in the live race (carAdded by the race scene from PeerName/PeerModel).
        public void AdoptRace(Race liveRace)
        {
            if(role!=SessionRole.Host)throw new InvalidOperationException("Only the host adopts the live race");
            if(liveRace==null||liveRace.cars.Count<2)throw new ArgumentException("The live race must carry the remote-driven car");
            race=liveRace;RaceAdopted=true;
        }
        public void Tick(float dt)
        {
            if(channel==null)return;silence+=dt;sendTimer+=dt;joinTimer+=dt;inputSilence+=dt;
            if(role==SessionRole.Host&&connected&&inputSilence>.5f&&race.cars.Count>1)race.cars[1].input=new DriverInput(0,1,0);
            string message,sender;int budget=64;
            while(budget-->0&&channel.Receive(out message,out sender))
            {
                try{Process(message,sender);}catch(Exception e){if(e is FormatException||e is OverflowException||e is IndexOutOfRangeException){status="Rejected malformed packet";}else throw;}
            }
            if(role==SessionRole.Client&&!connected&&joinTimer>1){joinTimer=0;channel.Send("HELLO|"+Wire.Version+"|"+nonce+"|"+Wire.Name(name)+"|"+localModel,peer);}
            if(sendTimer>=.066f)
            {
                sendTimer=0;
                if(role==SessionRole.Host&&connected)channel.Send(Wire.Snapshot(race,++sequence),peer);
                if(role==SessionRole.Client&&connected)
                {
                    var input=localInput.Safe();channel.Send("INPUT|"+sessionToken+"|"+(++inputSequence)+"|"+Wire.F(input.throttle)+"|"+Wire.F(input.brake)+"|"+Wire.F(input.steer)+"|"+((input.handbrake?1:0)+(input.reverse?2:0))+"|"+(pitRequest?1:0)+"|"+(int)compound+"|"+(resetRequest?1:0),peer);
                }
            }
            if(connected&&silence>5)
            {
                connected=false;status="Connection lost — return to menu to reconnect";
                if(role==SessionRole.Host&&race.cars.Count>1){race.cars[1].human=false;race.cars[1].driver="AI (disconnected)";}
            }
        }
        public void Process(string message,string sender)
        {
            if(string.IsNullOrEmpty(message)||message.Length>16000)return;string[] p=message.Split('|');
            if(p[0]=="DISCOVER"&&role==SessionRole.Host&&p.Length==2&&p[1]==Wire.Version){channel.Send("OFFER|"+Wire.Version+"|"+Wire.Name(name),sender);return;}
            if(p[0]=="OFFER"&&p.Length==3&&p[1]==Wire.Version){rooms[sender]=Wire.Unname(p[2]);return;}
            if(p[0]=="HELLO"&&role==SessionRole.Host&&p.Length==5&&p[1]==Wire.Version)
            {
                if(race.started||connected&&sender!=peer){channel.Send("BUSY|Race already started",sender);return;}
                if(p[2].Length!=32)return;
                int model=Wire.Int(p[4]);string peerName=Wire.Unname(p[3]);if(model<0||model>=CarCatalog.Count||race.cars.Count<2)return;if(peerNonce!=p[2]){lastInputSequence=-1;remoteResetHeld=false;sessionToken=Guid.NewGuid().ToString("N");peerNonce=p[2];}race.cars[1].carModel=model;PeerName=peerName;PeerModel=model;peer=sender;connected=true;silence=0;inputSilence=0;race.cars[1].human=true;race.cars[1].driver=peerName;status="Second driver connected";
                channel.Send("ACCEPT|"+Wire.Version+"|"+p[2]+"|"+sessionToken,peer);channel.Send(Wire.Snapshot(race,++sequence),peer);return;
            }
            if(sender!=peer)return;
            if(p[0]=="ACCEPT"&&role==SessionRole.Client&&p.Length==4&&p[1]==Wire.Version&&p[2]==nonce&&p[3].Length==32){sessionToken=p[3];connected=true;silence=0;status="Connected — waiting for host";return;}
            if(p[0]=="BUSY"){status="Host race already started";return;}
            if(p[0]=="START"&&role==SessionRole.Client&&connected&&p.Length==2&&p[1]==sessionToken){startReceived=true;silence=0;status="Race starting";return;}
            if(p[0]=="INPUT"&&role==SessionRole.Host&&connected&&p.Length==10&&p[1]==sessionToken)
            {
                int seq=Wire.Int(p[2]);if(seq<=lastInputSequence)return;
                int flags=Wire.Int(p[6]),compound=Wire.Int(p[8]);var input=new DriverInput(Wire.Float(p[3]),Wire.Float(p[4]),Wire.Float(p[5])){handbrake=(flags&1)!=0,reverse=(flags&2)!=0}.Safe();
                if(flags<0||flags>3||compound<0||compound>3||(p[7]!="0"&&p[7]!="1")||(p[9]!="0"&&p[9]!="1"))throw new FormatException("Invalid command");
                lastInputSequence=seq;silence=0;inputSilence=0;Vehicle v=race.cars[1];v.input=input;
                if(!v.finished&&!v.retired&&!v.servicing){if(p[7]=="1")v.pitRequested=true;v.nextCompound=(Compound)compound;}
                bool reset=p[9]=="1";if(reset&&!remoteResetHeld)race.Reset(v);remoteResetHeld=reset;if(reset)channel.Send("RESET_ACK|"+sessionToken,peer);return;
            }
            if(p[0]=="RESET_ACK"&&role==SessionRole.Client&&connected&&p.Length==2&&p[1]==sessionToken){resetRequest=false;return;}
            if(p[0]=="STATE"&&role==SessionRole.Client&&connected)
            {int seq;Race snapshot=Wire.ReadSnapshot(p,out seq);if(seq<=lastSequence)return;lastSequence=seq;race=snapshot;if(race.cars.Count>localId&&(race.cars[localId].pitRequested||race.cars[localId].servicing))pitRequest=false;silence=0;status=race.started?"Connected":"Connected — waiting for host";return;}
            if(p[0]=="BYE"&&connected){connected=false;status="Other driver left";if(role==SessionRole.Host)race.cars[1].human=false;}
        }
        public void Dispose(){if(channel!=null){if(connected)channel.Send("BYE",peer);channel.Dispose();channel=null;}connected=false;}
    }
}
