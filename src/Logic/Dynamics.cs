using System;
namespace MySimulator.Rebuild
{
    // Four-wheel planar vehicle model: Magic-Formula tyres (slip-vector combined slip), per-wheel spin with implicit
    // integration, longitudinal + lateral load transfer, aero, power-limited drive, bias-split brakes, ABS/TC.
    // Structure follows the open Pacejka/Beckman formulations used by Rigs of Rods, Speed Dreams and Project Chrono.
    [Serializable] public class DynState
    {
        public float[] w=new float[4];public float[] kappa=new float[4];public float[] fz=new float[4];public float[] alpha=new float[4];
        public float ax,ay;public bool init;
    }
    public static class Dynamics
    {
        const float R=.33f,Iw=1.6f,Kappa0=.11f,Alpha0=.12f,PB=1.75f,PC=1.5f,PE=.2f,G=9.81f;
        // combined slip: one normalised slip vector, one Magic-Formula magnitude, split along the slip direction
        static void Tyre(float fz,float mu,float kappa,float alpha,out float fx,out float fy)
        {
            float kn=kappa/Kappa0,an=alpha/Alpha0,s=(float)Math.Sqrt(kn*kn+an*an);
            if(s<1e-5f){fx=0;fy=0;return;}
            float bs=PB*s,f=(float)Math.Sin(PC*Math.Atan(bs-PE*(bs-Math.Atan(bs))))*mu*fz;
            fx=f*kn/s;fy=-f*an/s;
        }
        // downforce N per (m/s)^2: road cars keep the recovered value, wing cars scale up toward real Cl*A (F1 ~2.4)
        public static float DownK(int car){float df=RecoveredVehicleProfiles.Downforce[Math.Max(0,Math.Min(RecoveredVehicleProfiles.Count-1,car))];return Math.Max(0,df-.9f)*(.55f+Math.Max(0,df-1.5f)*.45f)*(df>2.5f?DownM:1f);}
        // slick racing tyres on wing cars grip harder than the road-car baseline
        public static float GripMul(int car){float df=RecoveredVehicleProfiles.Downforce[Math.Max(0,Math.Min(RecoveredVehicleProfiles.Count-1,car))];return MathUtil.Clamp(1+GripSlope*(df-2f),1f,GripMax);}
        public static readonly float PowerMulEnv=float.Parse(Environment.GetEnvironmentVariable("MYSIM_PMUL")??"1",System.Globalization.CultureInfo.InvariantCulture);
        public static readonly float AeroF=float.Parse(Environment.GetEnvironmentVariable("MYSIM_AEROF")??"0.42",System.Globalization.CultureInfo.InvariantCulture);
        public static readonly float GripSlope=float.Parse(Environment.GetEnvironmentVariable("MYSIM_GSLOPE")??"1.2",System.Globalization.CultureInfo.InvariantCulture),GripMax=float.Parse(Environment.GetEnvironmentVariable("MYSIM_GMAX")??"2.8",System.Globalization.CultureInfo.InvariantCulture);
        public static readonly float DownM=float.Parse(Environment.GetEnvironmentVariable("MYSIM_DOWNM")??"2",System.Globalization.CultureInfo.InvariantCulture);
        public static float SteeringLock(float speed){float vs=Math.Max(Math.Abs(speed),.1f),frac=MathUtil.Clamp((45f-vs)/37f,0,1);return Math.Max(frac*(.55f-.085f)+.085f,1f/8192f);}
        public static float SteeringTarget(float command,float speed){float limit=SteeringLock(speed);return MathUtil.Clamp(command*330f/256f*limit,-limit,limit);}
        public static float SteeringCommand(float angle,float speed)=>MathUtil.Clamp(angle/(330f/256f*SteeringLock(speed)),-1,1);
        public static float WheelBrakeForce(float serviceForce,float mass,bool front,bool handbrake)=>serviceForce*(front?.29f:.21f)+(!front&&handbrake?mass*3f*.5f:0f);
        public static void NormalLoads(float mass,float wheelbase,float track,float height,float down,float ax,float ay,float[] loads){
            float total=mass*G+Math.Max(0,down),front=MathUtil.Clamp(mass*G*.52f+Math.Max(0,down)*AeroF-mass*ax*height/wheelbase,0,total),rear=total-front;
            float lf=MathUtil.Clamp(front*.5f+mass*ay*height/track*.55f,0,front),lr=MathUtil.Clamp(rear*.5f+mass*ay*height/track*.45f,0,rear);
            loads[0]=lf;loads[1]=front-lf;loads[2]=lr;loads[3]=rear-lr;
        }
        public static void Step(Vehicle v,float dt,float wetness,Circuit circuit)
        {
            int m=Math.Max(0,Math.Min(RecoveredVehicleProfiles.Count-1,v.carModel));
            float mass=RecoveredVehicleProfiles.Mass[m],vmax=RecoveredVehicleProfiles.Speed[m]*.44704f,t60=RecoveredVehicleProfiles.Acceleration[m],stop60=RecoveredVehicleProfiles.Brakes[m]*.3048f,df=RecoveredVehicleProfiles.Downforce[m];
            float L=RecoveredGeometry.Wheelbase[Math.Max(0,Math.Min(RecoveredGeometry.Wheelbase.Length-1,v.carModel))],a=L*.48f,b=L*.52f,track=Math.Max(1.4f,L*.5f),h=.29f+L*.02f;
            DynState st=v.dyn;
            if(v.hasIntegrated){Vec push=v.position-v.lastIntegrated;float pm=push.Magnitude;v.dbgPush=pm;if(pm>.002f){Vec n=push*(1/pm);float vn=Vec.Dot(v.velocity,n);if(vn<0)v.velocity=v.velocity-n*vn*1.3f;v.velocity=v.velocity*(1-Math.Min(.5f,pm*.8f));}}
            float u=Vec.Dot(v.velocity,v.Forward),vy=Vec.Dot(v.velocity,v.Right);float speed=v.velocity.Magnitude;
            if(st==null||!st.init){st=v.dyn=new DynState();st.init=true;for(int i=0;i<4;i++)st.w[i]=u/R;}
            var inp=v.input;
            bool road=circuit.OnRoad(v.progress,v.lateral)||v.onDrivingSurface;
            // steering: RR3 speed-sensitive lock, rate-limited
            float maxSteer=SteeringLock(u);
            v.currentMaxSteer=maxSteer;float target=SteeringTarget(inp.steer,u);
            float stp=3.2f*dt,prev=v.steering;v.steering+=MathUtil.Clamp(target-v.steering,-stp,stp);if((target-v.steering)*(target-prev)<0)v.steering=target;
            float delta=v.steering,cd=(float)Math.Cos(delta),sd=(float)Math.Sin(delta);
            // power, drag, aero
            float pmul=PowerMulEnv;float power=mass*26.82f*26.82f/(2*t60)*1.35f*pmul,dragK=power/pmul/(vmax*vmax*vmax);
            float down=DownK(v.carModel)*speed*speed;
            float[] fz=st.fz;NormalLoads(mass,L,track,h,down,st.ax,st.ay,fz);float fz0=mass*G*.25f;
            // pedals -> wheel torques
            float wMean=(st.w[2]+st.w[3])*.5f;
            float driveTotal=Math.Min(power/Math.Max(Math.Max(wMean*R,u),6f),mass*G*2.2f)*inp.throttle+HybridDeployment.Step(v,inp,dt)*inp.throttle;
            if(inp.reverse)driveTotal=u<-11f?0f:-Math.Min(mass*3.5f,mass*G*.4f)*(inp.throttle+inp.brake)*.5f;
            float brakeDecel=26.82f*26.82f/(2*stop60);float brakeTotal=(inp.reverse?0:inp.brake)*brakeDecel*mass*(road?1:.7f);
            float fxB=0,fyB=0,mz=0;float slipAcc=0;
            for(int i=0;i<4;i++)
            {
                bool front=i<2,left=(i&1)==0;float xi=front?a:-b,yi=left?-track*.5f:track*.5f;
                float d=front?delta:0,c=front?cd:1,s=front?sd:0;
                float vxB=u-v.yawRate*yi,vyB=vy+v.yawRate*xi;
                float vxw=vxB*c+vyB*s,vyw=vyB*c-vxB*s;
                float mu0=v.tyres[i].Grip(wetness)*1.18f*GripMul(v.carModel)*(road?1:.62f);
                float mu=mu0*MathUtil.Clamp(1-.12f*(fz[i]/fz0-1),.7f,1.15f);
                float vref=Math.Max(Math.Abs(vxw),2f),vlat=Math.Max(Math.Abs(vxw),4f);
                float alpha=(float)Math.Atan2(vyw,vlat),kappa=(st.w[i]*R-vxw)/vref;
                float fx0,fy0,fx1,fy1;Tyre(fz[i],mu,kappa,alpha,out fx0,out fy0);Tyre(fz[i],mu,kappa+.02f,alpha,out fx1,out fy1);
                float k=Math.Max(0,(fx1-fx0)/.02f);
                // torques: rear-wheel drive with TC, front-biased brakes with ABS
                float td=front?0:driveTotal*.5f*R;if(!front&&v.traction&&!inp.reverse)td*=MathUtil.Clamp(1-(st.kappa[i]-.14f)/.1f,.1f,1f);
                float serviceTb=WheelBrakeForce(brakeTotal,mass,front,false)*R;
                if(v.abs&&inp.brake>0)serviceTb*=MathUtil.Clamp(1-(-st.kappa[i]-.13f)/.08f,.15f,1f);
                float tb=serviceTb+WheelBrakeForce(0,mass,front,inp.handbrake)*R;
                float wi=st.w[i],sg=Math.Abs(wi)>.05f?Math.Sign(wi):0;
                float torque=td-sg*tb;
                float wn=wi+dt/Iw*(torque-R*fx0)/(1+dt*R*R*k/(Iw*vref));
                if(tb>0&&wi*wn<0)wn=0;
                st.w[i]=wn;float kn=(wn*R-vxw)/vref;st.kappa[i]=kn;
                float fx,fy;Tyre(fz[i],mu,kn,alpha,out fx,out fy);st.alpha[i]=alpha;
                float fbx=fx*c-fy*s,fby=fx*s+fy*c;
                fxB+=fbx;fyB+=fby;mz+=xi*fby-yi*fbx;
                slipAcc=Math.Max(slipAcc,(float)Math.Sqrt(alpha*alpha+.25f*kn*kn));
                v.tyres[i].Step(dt,speed,(float)Math.Sqrt(alpha*alpha+.25f*kn*kn),1,wetness);
            }
            // resistance
            float drag=dragK*u*Math.Abs(u),roll=mass*G*(road?.014f:.09f);
            float Iz=mass*L*L*.28f;
            float un=u+(fxB/mass+vy*v.yawRate)*dt;
            float res=(Math.Abs(drag)+roll)/mass*dt;float resSign=Math.Sign(un);un=resSign*Math.Max(0,Math.Abs(un)-res);
            if(inp.reverse&&driveTotal<0&&un>0)un=Math.Max(0,un-brakeDecel*dt);
            float vn2=vy+(fyB/mass-u*v.yawRate)*dt,rn=v.yawRate+mz/Iz*dt;
            float low=Math.Max(0,1-Math.Abs(un)/5f);vn2*=1-dt*8*low;rn*=1-dt*(1.1f*(1-low)+6*low);
            // kinematic yaw at crawl speeds where slip-based tyre forces are ill-conditioned
            float kin=un/L*(float)Math.Tan(delta);rn=MathUtil.Lerp(kin,rn,MathUtil.Clamp((speed-3)/9,0,1));
            if(v.steerAssist){float maxYaw=mass*G*1.8f/mass/Math.Max(Math.Abs(un),3)*1.08f;rn=MathUtil.Clamp(rn,-maxYaw,maxYaw);}
            st.ax+=(((fxB-Math.Sign(un)*(Math.Abs(drag)+roll))/mass)-st.ax)*Math.Min(1,dt/.05f);
            st.ay+=((fyB/mass)-st.ay)*Math.Min(1,dt/.05f);
            v.yawRate=rn;v.heading=MathUtil.Wrap(v.heading+rn*dt);
            v.velocity=v.Forward*un+v.Right*vn2;v.velocity+=v.surfaceGravity*dt;
            v.position+=v.velocity*dt;v.speed=v.velocity.Magnitude;v.totalDistance+=v.speed*dt;v.lastIntegrated=v.position;v.hasIntegrated=true;
            v.dbgLong=un;v.dbgAcc=st.ax;v.dbgDriveF=driveTotal;v.dbgBrakeF=brakeTotal;
            int gearCount=RecoveredVehicleProfiles.GearData[m].Length;float ratio=vmax/gearCount;
            v.gear=un<-.1f?-1:Math.Max(1,Math.Min(gearCount,1+(int)(Math.Abs(un)/(ratio*.92f))));
            float inGear=(Math.Abs(un)-(v.gear-1)*ratio*.92f)/(ratio*.92f);v.rpm=MathUtil.Clamp(v.gear<0?2500:3600+inGear*4600,900,RecoveredVehicleProfiles.Redline[m]);
        }
    }
}
