using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Decisões determinísticas por piloto; a parada é forçada no máximo na penúltima volta.</summary>
    public sealed class BotPitStrategy
    {
        readonly int style;
        int preferredLap;
        float threshold,cooldown;
        readonly System.Random random;
        float sinceDrift,holdRemaining,turboClock;
        public BotPitStrategy(int index)
        {
            style=index%3;random=new System.Random(unchecked(System.Environment.TickCount+index*7919));Reset();
        }
        public void Reset()
        {
            sinceDrift=0;holdRemaining=0;turboClock=0;
            threshold=(style==0?32:style==1?26:20)+(float)random.NextDouble()*8;
            preferredLap=random.Next(Mathf.Max(1,RaceConfig.Laps-1));
            cooldown=(style==0?12:style==1?18:26)*(.85f+(float)random.NextDouble()*.3f);
        }
        public void Update(float dt,Competitor racer,CandyPitStop pit,InputState input)
        {
            var kart=racer.Kart;sinceDrift+=dt;turboClock+=dt;
            if(!kart.Fuel.HasRefueled && (kart.Fuel.Current<threshold || racer.Tracker.Lap>=Mathf.Max(0,RaceConfig.Laps-2)
                || racer.Tracker.Lap>=preferredLap && kart.Fuel.Current<55)) pit.Request(kart);
            input.DriftHeld=false;input.TurboPressed=false;
            if(kart.PitControlled || racer.Finished) return;
            float approach=pit.DistanceToEntry(kart);
            if(pit.IsRequested(kart) && approach<90) return; // Guarda combustível e evita derrapar na entrada.
            if(holdRemaining>0)
            {holdRemaining-=dt;input.DriftHeld=Mathf.Abs(input.Steer)>.2f;}
            else if(sinceDrift>cooldown && Mathf.Abs(input.Steer)>.35f && kart.Fuel.CanAfford(kart.Settings.shortDriftCost))
            {holdRemaining=style==0?2.15f:1.1f;sinceDrift=0;input.DriftHeld=true;}
            float reserve=style==2?35:15;
            if(kart.Turbo.Ready && turboClock>8 && Mathf.Abs(input.Steer)<.25f
                && kart.Fuel.Available>=kart.Settings.turboCost+reserve)
            {input.TurboPressed=true;turboClock=0;}
        }
    }
}
