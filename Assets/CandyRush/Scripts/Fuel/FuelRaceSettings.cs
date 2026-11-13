using UnityEngine;
using UnityEngine.InputSystem;

namespace CandyRush.Integrated
{
    /// <summary>Balanceamento compartilhado, editável antes do Play pelo asset em Resources.</summary>
    [CreateAssetMenu(menuName = "Candy Rush/Combustível e posto")]
    public sealed class FuelRaceSettings : ScriptableObject
    {
        [Header("Combustível: pontos percentuais")]
        [Min(0)] public float consumptionPerMeter = .005f;
        [Range(0,100)] public float shortDriftCost = 15, longDriftCost = 20, turboCost = 30;
        [Range(1,99)] public float lowFuelThreshold = 25;
        [Range(.05f,1)] public float emptySpeedFactor = .3f;
        [Header("Drift real")]
        [Min(.1f)] public float longDriftSeconds = 1.8f;
        [Min(0)] public float minimumDriftSpeed = 6;
        [Range(.05f,1)] public float minimumDriftSteer = .25f;
        [Range(.1f,1)] public float driftRearGrip = .55f;
        [Range(0,1)] public float shortDriftCharge = .1f, longDriftCharge = .2f;
        [Min(.05f)] public float shortDriftBoostSeconds = .55f, longDriftBoostSeconds = .9f;
        [Header("Turbo da rampa")]
        [Min(.1f)] public float turboSeconds = 1.85f;
        [Min(0)] public float turboForce = 1550, turboSpeedBonus = 8;
        [Header("Controles")]
        public bool autoAcceleration = false, automaticDrift;
        public InputActionReference driftAction, turboAction;
        public string driftBinding = "<Keyboard>/leftShift", alternativeDriftBinding = "<Keyboard>/space";
        public string turboBinding = "<Keyboard>/leftCtrl", alternativeTurboBinding = "<Keyboard>/e";
        [Header("Posto")]
        [Tooltip("O posto usa a construção junto à largada (CircuitConfig.PitAt).")]
        [Range(0,.85f)] public float pitFraction = .035f;
        [Range(2,3)] public float refuelSeconds = 2.5f;
        [Range(1,4)] public int pumpCount = 2;
        [Min(1)] public float pitSpeed = 7;
        [Min(10)] public float approachWarningMeters = 85;
        [Min(1)] public float playerRefuelRadius = 3.4f;
        public bool mandatoryPlayerStop = false; // Compatibilidade: jogadores sempre decidem e confirmam na bomba.
        public static FuelRaceSettings Load()
        {
            var asset = Resources.Load<FuelRaceSettings>("Candy/FuelRaceSettings");
            return asset != null ? asset : CreateInstance<FuelRaceSettings>();
        }
    }
}
