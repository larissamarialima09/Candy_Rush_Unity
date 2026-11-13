using System;
using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Carga da rampa e impulso forte, separados do drift.</summary>
    [Serializable]
    public sealed class TurboSystem
    {
        readonly FuelSystem fuel;
        readonly FuelRaceSettings settings;
        [SerializeField,Range(0,1)] float charge;
        float remaining;
        public float Charge=>charge;
        public bool Ready=>charge>=.9999f;
        public bool IsBoosting=>remaining>0;
        public float Remaining=>Mathf.Clamp01(remaining/Mathf.Max(.1f,settings.turboSeconds));
        public bool JustStarted {get;private set;}
        public event Action OnTurboReady,OnTurboStart,OnTurboEnd;
        public TurboSystem(FuelSystem tank,FuelRaceSettings tuning) {fuel=tank;settings=tuning;}
        public void FillCharge(float amount)
        {
            bool wasReady=Ready; charge=Mathf.Clamp01(charge+Mathf.Max(0,amount));
            if(!wasReady && Ready) OnTurboReady?.Invoke();
        }
        public bool TryActivate()
        {
            if(!Ready || IsBoosting || fuel.Refueling) return false;
            if(fuel.IsEmpty) return fuel.Reject("SEM COMBUSTÍVEL PARA TURBO");
            if(!fuel.TrySpend(settings.turboCost,"TURBO")) return false;
            charge=0; remaining=Mathf.Max(.1f,settings.turboSeconds);
            JustStarted=true; OnTurboStart?.Invoke(); return true;
        }
        public void Step(float dt)
        {
            JustStarted=false;
            if(!IsBoosting) return;
            remaining=Mathf.Max(0,remaining-dt); if(fuel.IsEmpty) remaining=0;
            if(!IsBoosting) OnTurboEnd?.Invoke();
        }
        public float Force=>IsBoosting ? settings.turboForce*Mathf.Pow(Remaining,.55f):0;
        public float SpeedBonus=>IsBoosting ? settings.turboSpeedBonus*Remaining:0;
        public void Cancel() {bool active=IsBoosting;remaining=0;if(active) OnTurboEnd?.Invoke();}
        public void Reset() {Cancel();charge=0;JustStarted=false;}
    }
}
