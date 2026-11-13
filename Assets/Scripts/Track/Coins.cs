using System.Collections.Generic;
using CandyRush.Rendering;
using CandyRush.Vehicle;
using UnityEngine;
using Coin = CandyRush.Config.FeaturesConfig.Coins;

namespace CandyRush.Track
{
    /// <summary>
    /// Moedas de bala ao longo da volta, numa fileira que ondula de um lado ao
    /// outro (convertido de src/track/coins.ts). Giram, flutuam e voltam alguns
    /// segundos depois de pegas. A coleta é regra de jogo: roda no passo fixo.
    /// </summary>
    public sealed class Coins
    {
        private sealed class Item
        {
            public Transform View;
            public Vector3 Position;
            public float Phase;
            public float Cooldown;
        }

        private readonly List<Item> _coins = new List<Item>();
        private float _clock;

        /// <summary>Quantas moedas o jogador pegou.</summary>
        public int Collected { get; private set; }
        /// <summary>True apenas no passo em que uma moeda foi pega.</summary>
        public bool JustCollected { get; private set; }
        public int Total => _coins.Count;

        public Coins(CircuitPath path, Transform parent)
        {
            Transform root = SceneryBuilder.Group(parent, "Moedas", Vector3.zero, Quaternion.identity);
            Material material = CandyMaterials.Candy(Coin.Color, 0.25f);
            CandyMaterials.SetGlow(material, Coin.Emissive, Coin.EmissiveIntensity);
            // Disco virado para Z (o cilindro nasce em pé).
            Mesh disc = ProceduralMeshes.Cylinder(Coin.Radius, Coin.Radius, Coin.Thickness, 18);

            float nextDistance = 0f;
            int index = 0;
            for (int i = 0; i < path.Count; i++)
            {
                PathSample sample = path.Samples[i];
                if (sample.Distance < nextDistance) continue;
                nextDistance = sample.Distance + Coin.Spacing;

                float weave = Mathf.Sin(index / Coin.WeaveLength * Mathf.PI * 2f);
                float lateral = weave * sample.HalfWidth * Coin.LateralFraction;
                Vector3 position = path.PointAt(i, lateral, Coin.Height);

                Transform holder = SceneryBuilder.Group(root, "Moeda", position, Quaternion.identity);
                SceneryBuilder.Part(holder, "Disco", disc, material, Vector3.zero, SceneryBuilder.Euler(Mathf.PI / 2f, 0f, 0f), Vector3.one, false);
                _coins.Add(new Item { View = holder, Position = position, Phase = index * 0.7f });
                index++;
            }
        }

        public void Update(float dt, Kart kart)
        {
            _clock += dt;
            JustCollected = false;
            Vector3 kartPosition = kart.Body.ChassisPosition;
            float radiusSquared = Coin.PickupRadius * Coin.PickupRadius;
            float spin = _clock * Coin.SpinSpeed * 360f;

            foreach (Item coin in _coins)
            {
                if (coin.Cooldown > 0f)
                {
                    coin.Cooldown -= dt;
                    if (coin.Cooldown > 0f) continue;
                    coin.View.gameObject.SetActive(true);
                }

                if ((coin.Position - kartPosition).sqrMagnitude < radiusSquared)
                {
                    coin.Cooldown = Coin.RespawnSeconds;
                    coin.View.gameObject.SetActive(false);
                    Collected++;
                    JustCollected = true;
                    continue;
                }

                coin.View.localPosition = coin.Position + Vector3.up * (Mathf.Sin(_clock * Coin.BobSpeed + coin.Phase) * Coin.BobAmplitude);
                coin.View.localRotation = Quaternion.AngleAxis(spin + coin.Phase * Mathf.Rad2Deg, Vector3.up);
            }
        }

        public void Reset()
        {
            Collected = 0;
            foreach (Item coin in _coins)
            {
                coin.Cooldown = 0f;
                coin.View.gameObject.SetActive(true);
            }
        }
    }
}
