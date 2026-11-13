using UnityEngine;

namespace CandyRush.Integrated
{
    /// <summary>Expõe o tanque e as configurações no Inspector sem criar outro loop de física.</summary>
    public sealed class KartRaceSystems:MonoBehaviour
    {
        public FuelRaceSettings settings;
        public FuelSystem fuel;
        public Kart Kart {get;private set;}
        public void Bind(Kart kart) {Kart=kart;fuel=kart.Fuel;settings=kart.Settings;}
    }
}
