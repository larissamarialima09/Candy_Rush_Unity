using System;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Drift segurado, independente da rampa. Só o fim válido cobra combustível.</summary>
    [Serializable]
    public sealed class DriftSystem
    {
        readonly FuelSystem fuel;
        readonly TurboSystem turbo;
        readonly FuelRaceSettings settings;
        public bool Drifting {get;private set;}
        public int Level {get;private set;}
        public float Duration {get;private set;}
        public float Direction {get;private set;}
        bool waitForRelease;
        float miniRemaining,miniDuration;
        int miniLevel;
        public bool BoostJustFired {get;private set;}
        public bool SpunOut {get;private set;}
        public event Action OnDriftStart;
        public event Action<int> OnDriftLevelUp;
        public event Action<int,float> OnDriftEnd;
        public DriftSystem(FuelSystem tank,TurboSystem boost,FuelRaceSettings tuning)
        {fuel=tank;turbo=boost;settings=tuning;}
        public void Update(float dt,float speed,float slip,bool grounded,bool held,float steer)
        {
            BoostJustFired=SpunOut=false;
            turbo.Step(dt);miniRemaining=Mathf.Max(0,miniRemaining-dt);
            if(fuel.IsEmpty) {CancelRide();if(held) fuel.Reject("SEM COMBUSTÍVEL PARA DRIFT");return;}
            if(!held) waitForRelease=false;
            bool eligible=grounded && speed>=settings.minimumDriftSpeed && Mathf.Abs(steer)>=settings.minimumDriftSteer;
            if(!Drifting)
            {
                if(!held || !eligible || waitForRelease) return;
                if(!fuel.ReserveDrift(settings.shortDriftCost)) {waitForRelease=true;return;}
                Drifting=true;Level=1;Duration=0;Direction=Mathf.Sign(steer);OnDriftStart?.Invoke();
            }
            if(!held || !eligible) {Finish();return;}
            if(Mathf.Abs(slip)>=DriftConfig.Detection.SpinOutAngleDeg)
            {SpunOut=true;CancelDrift();waitForRelease=true;return;}
            if(Level==1 && Duration+dt>=settings.longDriftSeconds)
            {
                // Sem os 20%, termina no nível curto antes de atravessar o limite longo.
                if(!fuel.ReserveDrift(settings.longDriftCost)) {Finish();waitForRelease=true;return;}
                Level=2;OnDriftLevelUp?.Invoke(Level);
            }
            Duration+=dt;
            if(Mathf.Abs(slip)>2) Direction=Mathf.Sign(slip);
        }
        void Finish()
        {
            int level=Level;float duration=Duration;Drifting=false;Level=0;Direction=0;
            if(fuel.CompleteDrift(level==2?settings.longDriftCost:settings.shortDriftCost))
            {
                turbo.FillCharge(level==2?settings.longDriftCharge:settings.shortDriftCharge);
                if(!fuel.IsEmpty)
                {
                    miniLevel=level;miniDuration=miniRemaining=level==2?settings.longDriftBoostSeconds:settings.shortDriftBoostSeconds;
                    BoostJustFired=true;
                }
                OnDriftEnd?.Invoke(level,duration);
            }
        }
        public void CancelDrift() {Drifting=false;Level=0;Duration=0;Direction=0;fuel.CancelDriftReservation();}
        public void CancelRide() {CancelDrift();turbo.Cancel();miniRemaining=0;BoostJustFired=false;}
        public void Reset() {CancelRide();turbo.Reset();waitForRelease=false;}
        // Compatibilidade com a rampa, efeitos e câmera; a carga agora pertence ao turbo.
        public float Charge=>turbo.Charge*DriftConfig.Charge.Levels[2];
        public void FillCharge(float amount)=>turbo.FillCharge(amount/DriftConfig.Charge.Levels[2]);
        public bool TryFireBoost()=>turbo.TryActivate();
        public bool IsBoosting=>turbo.IsBoosting || miniRemaining>0;
        public int BoostLevel=>turbo.IsBoosting?3:miniRemaining>0?miniLevel:0;
        public float BoostTimeRemaining=>turbo.IsBoosting?turbo.Remaining:miniDuration>0?Mathf.Clamp01(miniRemaining/miniDuration):0;
        public float BoostForce=>turbo.IsBoosting?turbo.Force:miniRemaining>0?DriftConfig.Boost.Force[miniLevel-1]*BoostTimeRemaining:0;
        public float SpeedCeilingBonus=>turbo.IsBoosting?turbo.SpeedBonus:miniRemaining>0?DriftConfig.Boost.SpeedCeilingBonus[miniLevel-1]*BoostTimeRemaining:0;
    }
}
