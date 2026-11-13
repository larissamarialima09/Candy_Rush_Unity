using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Fila FIFO, reservas de bombas e rota separada. Avança exclusivamente no passo fixo da corrida.</summary>
    public sealed class CandyPitStop : MonoBehaviour
    {
        [Serializable] public sealed class Pump
        {
            public Transform approach,stop,clear;
            public PitCrewAnimation crew;
            [NonSerialized] public Kart owner;
        }
        public FuelRaceSettings settings;
        public Transform[] entryWaypoints,exitWaypoints,queueWaypoints;
        public Pump[] pumps;
        public BoxCollider entryTrigger;
        public Texture2D trackTexture,trackNormals;
        public event Action<Kart> OnPitEntered,OnPitExited;
        public int QueueCount=>queue.Count;
        CircuitPath path;
        bool paused;
        readonly List<Visit> visits=new List<Visit>();
        readonly List<Visit> queue=new List<Visit>();
        enum Stage {None,Entry,Queued,Approach,Docking,Refueling,Clearing,Exit,Done}
        sealed class Visit
        {
            public Competitor competitor; public Stage stage; public int waypoint,pump=-1;
            public float speed,timer; public bool requested;
        }
        public Vector3 EntryPoint=>Position(entryWaypoints[0]);
        public float EntryDistance {get;private set;}
        public void Configure(CircuitPath circuit,FuelRaceSettings tuning)
        {
            path=circuit;settings=tuning;
            if(entryWaypoints==null || entryWaypoints.Length==0) PitStopVisuals.Build(this,circuit,tuning);
            if(exitWaypoints==null || exitWaypoints.Length==0 || pumps==null || pumps.Length==0)
                throw new InvalidOperationException("Posto Doce: configure as bombas e os waypoints de saída.");
            foreach(var pump in pumps) if(pump.approach==null || pump.stop==null || pump.clear==null)
                throw new InvalidOperationException("Posto Doce: cada bomba precisa de aproximação, parada e liberação.");
            if(queueWaypoints==null || queueWaypoints.Length==0) queueWaypoints=new[]{entryWaypoints[entryWaypoints.Length-1]};
            EntryDistance=path.Samples[path.NearestSampleIndex(EntryPoint.x,EntryPoint.z)].Distance;
        }
        public static Vector3 Position(Transform point)=>ThreeSpace.FromUnity(point.position);
        public void Bind(IReadOnlyList<Competitor> competitors)
        {
            ResetRace();visits.Clear();
            foreach(var c in competitors) visits.Add(new Visit{competitor=c});
        }
        public void Request(Kart kart) {var v=Find(kart);if(v!=null) v.requested=true;}
        public bool IsRequested(Kart kart)=>Find(kart)?.requested==true;
        public bool IsInDriveArea(Kart kart)
        {var p=kart.Body.ChassisPosition;return path.PitLane.Contains(p.x,p.z,.5f);}
        int NearbyPump(Kart kart,bool freeOnly)
        {
            int best=-1;float nearest=settings.playerRefuelRadius*settings.playerRefuelRadius;
            for(int i=0;i<pumps.Length;i++)
            {
                if(freeOnly && pumps[i].owner!=null) continue;
                var delta=Position(pumps[i].stop)-kart.Body.ChassisPosition;delta.y=0;
                if(delta.sqrMagnitude>nearest) continue;
                nearest=delta.sqrMagnitude;best=i;
            }
            return best;
        }
        public bool IsNearPump(Kart kart)=>NearbyPump(kart,false)>=0;
        public bool CanRefuelPlayer(Kart kart)
        {
            var v=Find(kart);
            return v!=null && v.competitor.IsPlayer && !v.competitor.Finished && v.stage==Stage.None
                && !kart.Fuel.HasRefueled && NearbyPump(kart,true)>=0;
        }
        /// <summary>Somente o clique do jogador inicia a parada. Aproximar-se não toma o volante.</summary>
        public bool TryRefuelPlayer(Kart kart)
        {
            if(!CanRefuelPlayer(kart)) return false;
            var v=Find(kart);v.pump=NearbyPump(kart,true);
            pumps[v.pump].owner=kart;v.stage=Stage.Docking;v.speed=kart.Telemetry.Speed;
            kart.PitControlled=true;kart.Drift.CancelRide();OnPitEntered?.Invoke(kart);return true;
        }
        Visit Find(Kart kart) {foreach(var v in visits) if(v.competitor.Kart==kart) return v;return null;}
        public float DistanceToEntry(Kart kart)
        {
            var p=kart.Body.ChassisPosition;
            int index=path.NearestSampleIndex(p.x,p.z);
            return Mathf.Repeat(EntryDistance-path.Samples[index].Distance,path.TotalLength);
        }
        public bool TryEnter(Kart kart)
        {
            var visit=Find(kart);
            if(visit==null || visit.competitor.IsPlayer || visit.stage!=Stage.None || kart.Fuel.HasRefueled || visit.competitor.Finished) return false;
            visit.stage=Stage.Entry;visit.waypoint=0;visit.speed=kart.Telemetry.Speed;
            kart.PitControlled=true;kart.Drift.CancelRide();
            OnPitEntered?.Invoke(kart);return true;
        }
        public void Step(float dt)
        {
            foreach(var v in visits)
            {
                Kart kart=v.competitor.Kart;
                kart.InPitLane=IsInDriveArea(kart);
                if(!v.competitor.IsPlayer && v.stage==Stage.None && !kart.Fuel.HasRefueled && !v.competitor.Finished)
                {
                    float ahead=DistanceToEntry(kart);
                    bool mandatory=v.competitor.Tracker.Lap>=Mathf.Max(0,RaceConfig.Laps-2);
                    // Captura começa antes do desvio: o percurso é contínuo, sem teleportar até a bomba.
                    if(ahead<14 && (mandatory || v.requested)) TryEnter(kart);
                }
                StepVisit(v,dt);
            }
            // Uma reserva por bomba; ninguém ultrapassa o primeiro da fila.
            for(int i=0;i<pumps.Length && queue.Count>0;i++)
            {
                if(pumps[i].owner!=null) continue;
                var v=queue[0];queue.RemoveAt(0);
                pumps[i].owner=v.competitor.Kart;v.pump=i;v.stage=Stage.Approach;
            }
            foreach(var pump in pumps) if(pump.crew!=null) pump.crew.Step(dt);
        }
        void StepVisit(Visit v,float dt)
        {
            Kart kart=v.competitor.Kart;
            switch(v.stage)
            {
                case Stage.Entry:
                    if(v.waypoint==entryWaypoints.Length-1 && queue.Count>0)
                    {v.stage=Stage.Queued;queue.Add(v);break;}
                    if(Move(v,Position(entryWaypoints[v.waypoint]),dt,false) && ++v.waypoint>=entryWaypoints.Length)
                    {v.stage=Stage.Queued;queue.Add(v);}
                    break;
                case Stage.Queued:
                    int q=Mathf.Max(0,queue.IndexOf(v));
                    Vector3 waiting=Position(queueWaypoints[Mathf.Min(q,queueWaypoints.Length-1)]);
                    if(q>=queueWaypoints.Length) waiting-=path.Samples[path.IndexAtFraction(settings.pitFraction)].Tangent*3*(q-queueWaypoints.Length+1);
                    Move(v,waiting,dt,true);break;
                case Stage.Approach:
                    if(Move(v,Position(pumps[v.pump].approach),dt,false)) v.stage=Stage.Docking;
                    break;
                case Stage.Docking:
                    if(Move(v,Position(pumps[v.pump].stop),dt,true))
                    {
                        v.stage=Stage.Refueling;v.timer=0;kart.Fuel.BeginRefuel();
                        pumps[v.pump].crew?.SetWorking(true);
                    }
                    break;
                case Stage.Refueling:
                    kart.SetPitPose(Position(pumps[v.pump].stop),kart.Body.Orientation,0,dt);
                    v.timer+=dt;kart.Fuel.SetRefuelProgress(v.timer/Mathf.Max(.1f,settings.refuelSeconds));
                    if(v.timer>=settings.refuelSeconds)
                    {
                        kart.Fuel.CompleteRefuel();pumps[v.pump].crew?.SetWorking(false);
                        v.speed=0;
                        if(v.competitor.IsPlayer)
                        {v.stage=Stage.Done;kart.PitControlled=false;OnPitExited?.Invoke(kart);}
                        else v.stage=Stage.Clearing;
                    }
                    break;
                case Stage.Clearing:
                    if(Move(v,Position(pumps[v.pump].clear),dt,false))
                    {pumps[v.pump].owner=null;v.pump=-1;v.stage=Stage.Exit;v.waypoint=0;}
                    break;
                case Stage.Exit:
                    if(Move(v,Position(exitWaypoints[v.waypoint]),dt,false) && ++v.waypoint>=exitWaypoints.Length)
                    {kart.PitControlled=false;v.stage=Stage.Done;OnPitExited?.Invoke(kart);}
                    break;
                case Stage.Done:
                    // A bomba só fica livre quando o jogador realmente sai da vaga.
                    if(v.pump>=0 && Vector3.Distance(kart.Body.ChassisPosition,Position(pumps[v.pump].stop))>settings.playerRefuelRadius+1.2f)
                    {pumps[v.pump].owner=null;v.pump=-1;}
                    break;
            }
        }
        bool Move(Visit v,Vector3 destination,float dt,bool stop)
        {
            Kart kart=v.competitor.Kart;
            Vector3 offset=destination-kart.Body.ChassisPosition;
            float distance=offset.magnitude;
            float target=stop ? Mathf.Min(settings.pitSpeed,Mathf.Sqrt(16*distance)):settings.pitSpeed;
            v.speed=Mathf.MoveTowards(v.speed,target,12*dt);
            float step=Mathf.Min(distance,Mathf.Max(.35f,v.speed)*dt);
            Vector3 next=distance>.001f ? kart.Body.ChassisPosition+offset/distance*step:destination;
            Vector3 flat=new Vector3(offset.x,0,offset.z);
            Quaternion heading=flat.sqrMagnitude>.001f ? Quaternion.LookRotation(flat):kart.Body.Orientation;
            Quaternion rotation=Quaternion.Slerp(kart.Body.Orientation,heading,1-Mathf.Exp(-dt*7));
            bool arrived=distance<.15f || step>=distance;
            kart.SetPitPose(arrived?destination:next,rotation,arrived && stop?0:v.speed,dt);
            return arrived;
        }
        public void ResetRace()
        {
            foreach(var v in visits) {v.competitor.Kart.PitControlled=v.competitor.Kart.InPitLane=false;v.stage=Stage.None;v.requested=false;v.pump=-1;v.timer=v.speed=0;}
            queue.Clear();
            if(pumps!=null) foreach(var pump in pumps) {pump.owner=null;pump.crew?.SetWorking(false);}
        }
        public void SetPaused(bool value)
        {
            if(paused==value) return;paused=value;
            foreach(var pump in pumps) pump.crew?.SetPaused(value);
        }
    }

    /// <summary>Adaptador opcional para futuros karts com Rigidbody/Collider; o port atual usa a região lógica.</summary>
    public sealed class CandyPitEntryTrigger:MonoBehaviour
    {
        public CandyPitStop pit;
        void OnTriggerEnter(Collider other)
        {var binding=other.GetComponentInParent<KartRaceSystems>();if(binding!=null) pit.TryEnter(binding.Kart);}
    }
}
