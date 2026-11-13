using System;
using UnityEngine;
using UnityEngine.Events;

namespace CandyRush.Integrated
{
    /// <summary>Tanque independente de PhysX, compartilhado pela mesma lógica de humanos e bots.</summary>
    [Serializable]
    public sealed class FuelSystem
    {
        public FuelRaceSettings settings;
        [SerializeField, Range(0,100)] float current = 100;
        [SerializeField] bool hasRefueled;
        float reservedDrift, notificationCooldown,refuelStart;
        public float Current => current;
        public float Normalized => current / 100;
        public float Available => Mathf.Max(0,current-reservedDrift);
        public bool IsEmpty => current <= .0001f;
        public bool IsLow => current < settings.lowFuelThreshold;
        public bool HasRefueled => hasRefueled;
        public bool Refueling { get; private set; }
        public float RefuelProgress { get; private set; }
        public string LastWarning { get; private set; }
        public int WarningSequence { get; private set; }
        public int SpendSequence { get; private set; }
        public float LastSpend { get; private set; }
        public string LastAction { get; private set; }
        public event Action<float> OnFuelChanged;
        public event Action OnFuelLow, OnFuelEmpty, OnRefueled;
        public event Action<float,string> OnFuelSpent;
        public event Action<string> OnActionRejected;
        public UnityEvent<float> fuelChanged = new UnityEvent<float>();
        public UnityEvent fuelLow = new UnityEvent(), fuelEmpty = new UnityEvent(), refueled = new UnityEvent();
        public FuelSystem(FuelRaceSettings tuning) { settings=tuning; }
        void SetFuel(float value)
        {
            float old=current; current=Mathf.Clamp(value,0,100);
            if (Mathf.Approximately(old,current)) return;
            OnFuelChanged?.Invoke(current); fuelChanged.Invoke(current);
            if (old>=settings.lowFuelThreshold && IsLow) { OnFuelLow?.Invoke(); fuelLow.Invoke(); }
            if (old>0 && IsEmpty) { OnFuelEmpty?.Invoke(); fuelEmpty.Invoke(); }
        }
        public void Step(float dt,float speed)
        {
            notificationCooldown=Mathf.Max(0,notificationCooldown-dt);
            if (!Refueling) SetFuel(current-Mathf.Min(Available,Mathf.Max(0,speed)*Mathf.Max(0,settings.consumptionPerMeter)*dt));
        }
        public bool CanAfford(float cost) => Available+.0001f>=Mathf.Max(0,cost) && !Refueling;
        public bool Reject(string message)
        {
            if (notificationCooldown>0) return false;
            notificationCooldown=1; LastWarning=message; WarningSequence++;
            OnActionRejected?.Invoke(message); return false;
        }
        public bool TrySpend(float cost,string action)
        {
            cost=Mathf.Clamp(cost,0,100);
            if (!CanAfford(cost)) return Reject("COMBUSTÍVEL INSUFICIENTE: "+action);
            SetFuel(current-cost); LastSpend=cost; LastAction=action; SpendSequence++;
            OnFuelSpent?.Invoke(cost,action); return true;
        }
        // Reserva sem descontar: o consumo contínuo e o turbo não podem gastar o custo do drift pendente.
        public bool ReserveDrift(float cost)
        {
            if (Refueling || current+.0001f<cost) return Reject("COMBUSTÍVEL INSUFICIENTE PARA DRIFT");
            reservedDrift=Mathf.Clamp(cost,0,100); return true;
        }
        public bool CompleteDrift(float cost) { reservedDrift=0; return TrySpend(cost,"DRIFT"); }
        public void CancelDriftReservation() { reservedDrift=0; }
        public void BeginRefuel() {if(hasRefueled) return;Refueling=true;RefuelProgress=0;reservedDrift=0;refuelStart=current;}
        public void SetRefuelProgress(float progress)
        {if(!Refueling) return;RefuelProgress=Mathf.Clamp01(progress);SetFuel(Mathf.Lerp(refuelStart,100,RefuelProgress));}
        public bool CompleteRefuel()
        {
            if (hasRefueled || !Refueling) return false;
            SetFuel(100); hasRefueled=true; Refueling=false; RefuelProgress=1;
            OnRefueled?.Invoke(); refueled.Invoke(); return true;
        }
        public void ResetRace()
        {
            reservedDrift=notificationCooldown=0; hasRefueled=Refueling=false;
            RefuelProgress=0; LastWarning=null; WarningSequence=SpendSequence=0; SetFuel(100);
        }
    }
}
