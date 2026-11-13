using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyRush.Integrated
{
    [DisallowMultipleComponent]
    public sealed class KartVisuals : MonoBehaviour
    {
        [Serializable]
        public sealed class VisualWheel
        {
            public Transform pivot;
            [Min(.01f)] public float radius = .25f;
            public bool front;
            [NonSerialized] public Vector3 restPosition;
            [NonSerialized] public Quaternion restRotation;
            [NonSerialized] public float spin, steer;
        }

        [Header("References (visual transforms only)")]
        public Transform bodyRoot;
        public VisualWheel[] wheels = Array.Empty<VisualWheel>();
        [Header("Materials")]
        public Color bodyColor = Color.white;
        [Range(0, 1)] public float bodySmoothness = .88f;
        [Range(0, 1)] public float tireSmoothness = .13f;
        [Range(0, 1)] public float rimMetallic = .92f, rimSmoothness = .55f;
        public Texture2D tireNormal;
        [Header("Motion")]
        [Range(0, 45)] public float maximumSteer = 28;
        public float wheelRotationSign = 1;
        [Min(.001f)] public float steerSmoothTime = .07f;
        [Range(0, 15)] public float maximumTilt = 5;
        [Min(.1f)] public float fullTiltSpeed = 12;
        [Min(.001f)] public float bodySmoothTime = .13f;
        [Range(0, .15f)] public float suspensionTravel = .045f;
        [Range(0, 10)] public float landingResponse = 1.5f;
        [Min(1)] public float spring = 100;
        [Min(0)] public float damping = 15;

        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Material> paint = new List<Material>(), tires = new List<Material>(), rims = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        Vector3 bodyRestPosition;
        Quaternion bodyRestRotation;
        float tilt, tiltVelocity, bounce, bounceVelocity, lastCompression, lastVerticalSpeed;
        bool sampled, lastGrounded;
        bool materialStateApplied;
        Color appliedColor;
        Vector4 appliedFinish;

        public void Configure(Transform body, Transform[] pivots, Wheel[] physicsWheels)
        {
            bodyRoot = body;
            wheels = new VisualWheel[pivots.Length];
            for (int i = 0; i < pivots.Length; i++)
                wheels[i] = new VisualWheel { pivot = pivots[i], radius = physicsWheels[i].Spec.Radius, front = physicsWheels[i].Spec.Steered };
            CaptureRestPose();
            ApplyMaterials();
        }

        public void CaptureRestPose()
        {
            if (bodyRoot != null) { bodyRestPosition = bodyRoot.localPosition; bodyRestRotation = bodyRoot.localRotation; }
            foreach (var wheel in wheels)
                if (wheel != null && wheel.pivot != null)
                { wheel.restPosition = wheel.pivot.localPosition; wheel.restRotation = wheel.pivot.localRotation; }
        }

        void Start() { if (originals.Count == 0) { CaptureRestPose(); ApplyMaterials(); } }

        void ApplyMaterials()
        {
            if (originals.Count > 0) return;
            Shader standard = Shader.Find("Standard"), lite = Shader.Find("CandyRush/FurLite");
            var renderers = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>(true));
            foreach (var visualWheel in wheels)
                if (visualWheel != null && visualWheel.pivot != null)
                    renderers.AddRange(visualWheel.pivot.GetComponentsInChildren<MeshRenderer>(true));
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                originals.Add(renderer, (Material[])slots.Clone());
                var filter = renderer.GetComponent<MeshFilter>();
                // GLBs have UVs and normals but no authored tangent attribute.
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable
                    && filter.sharedMesh.tangents.Length == 0)
                {
                    var mesh = Instantiate(filter.sharedMesh); mesh.name += " Visual tangents";
                    originalMeshes.Add(filter, filter.sharedMesh); ownedMeshes.Add(mesh);
                    mesh.RecalculateTangents(); filter.sharedMesh = mesh;
                }
                bool wheel = renderer.name.StartsWith("Wheel_", StringComparison.Ordinal);
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (source == null || source.name.Contains("_Fur") || source.name.StartsWith("Belly", StringComparison.Ordinal)) continue;
                    bool softDetail = source.name.Contains("EarInner") || source.name.StartsWith("Nose", StringComparison.Ordinal);
                    var material = new Material(softDetail ? lite : standard) { name = source.name + " Finish (runtime)" };
                    ownedMaterials.Add(material); slots[i] = material;
                    material.SetColor("_Color", VisualMaterialLibrary.ReadColor(source));
                    if (softDetail)
                    {
                        // Separate detail slots act as an all-white smoothness mask.
                        material.SetTexture("_SmoothMask", Texture2D.whiteTexture);
                        material.SetFloat("_DetailSmoothness", .55f);
                        continue;
                    }
                    if (source.name.Contains("_Main"))
                    {
                        bodyColor = VisualMaterialLibrary.ReadColor(source);
                        paint.Add(material);
                        if (wheel) rims.Add(material);
                    }
                    else if (source.name.StartsWith("Tire", StringComparison.Ordinal))
                    {
                        tires.Add(material);
                        material.SetTexture("_BumpMap", tireNormal != null ? tireNormal : VisualMaterialLibrary.TireNormal);
                        material.SetTextureScale("_BumpMap", new Vector2(5, 2));
                        material.SetFloat("_BumpScale", .65f);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    else if (source.name.StartsWith("Metal", StringComparison.Ordinal) || (wheel && source.name.StartsWith("White", StringComparison.Ordinal))) rims.Add(material);
                    else material.SetFloat("_Glossiness", source.name.StartsWith("Seat", StringComparison.Ordinal) ? .18f : .55f);
                }
                renderer.sharedMaterials = slots;
            }
            UpdateMaterials();
        }

        void UpdateMaterials()
        {
            var finish = new Vector4(bodySmoothness, tireSmoothness, rimMetallic, rimSmoothness);
            if (materialStateApplied && appliedColor == bodyColor && appliedFinish == finish) return;
            materialStateApplied = true; appliedColor = bodyColor; appliedFinish = finish;
            foreach (var m in paint) { m.SetColor("_Color", bodyColor); m.SetFloat("_Glossiness", bodySmoothness); }
            foreach (var m in tires) m.SetFloat("_Glossiness", tireSmoothness);
            foreach (var m in rims) { m.SetFloat("_Metallic", rimMetallic); m.SetFloat("_Glossiness", rimSmoothness); }
        }

        /// <summary>Physics remains authoritative; wheel spin/steer are radians, compression in meters.</summary>
        public void Animate(Kart kart, float dt)
        {
            UpdateMaterials();
            if (kart.Telemetry.Respawned) ResetMotion();
            int count = Mathf.Min(wheels.Length, kart.Wheels.Length);
            float compression = 0;
            for (int i = 0; i < count; i++)
            {
                Wheel physics = kart.Wheels[i];
                var visual = wheels[i];
                if (visual == null || visual.pivot == null) continue;
                compression += physics.Compression;
                visual.pivot.localPosition = physics.Spec.PositionLocal
                    - Vector3.up * (KartConfig.Suspension.RestLength - physics.Compression);
                visual.spin = physics.SpinAngle * Mathf.Rad2Deg;
                float target = visual.front ? physics.SteerAngle * Mathf.Rad2Deg : 0;
                visual.steer = !sampled ? target : Mathf.LerpAngle(visual.steer, target, 1 - Mathf.Exp(-dt / Mathf.Max(.001f, steerSmoothTime)));
                visual.pivot.localRotation = visual.restRotation * Quaternion.AngleAxis(visual.steer, Vector3.up)
                    * Quaternion.AngleAxis(visual.spin * wheelRotationSign, Vector3.right);
            }
            compression /= Mathf.Max(1, count);
            if (dt <= 0) return;
            AnimateBody(kart.Telemetry.ForwardSpeed, kart.Telemetry.SteerAngleDeg / KartConfig.Steering.MaxAngleDegrees,
                compression, kart.Telemetry.WheelsOnGround > 0, kart.Body.Velocity.y, dt);
        }

        /// <summary>Optional adapter for a different controller. Speed is signed meters/second; steering is -1..1.</summary>
        public void Animate(float speed, float steering, float compression, bool grounded, float verticalSpeed, float dt)
        {
            UpdateMaterials();
            if (dt <= 0) return;
            foreach (var wheel in wheels)
            {
                if (wheel == null || wheel.pivot == null) continue;
                wheel.spin = Mathf.Repeat(wheel.spin + speed / Mathf.Max(.01f, wheel.radius) * Mathf.Rad2Deg * dt * wheelRotationSign, 360);
                wheel.steer = Mathf.LerpAngle(wheel.steer, wheel.front ? Mathf.Clamp(steering, -1, 1) * maximumSteer : 0,
                    1 - Mathf.Exp(-dt / Mathf.Max(.001f, steerSmoothTime)));
                wheel.pivot.localRotation = wheel.restRotation * Quaternion.Euler(0, wheel.steer, 0) * Quaternion.Euler(wheel.spin, 0, 0);
            }
            AnimateBody(speed, steering, compression, grounded, verticalSpeed, dt);
        }

        void AnimateBody(float speed, float steer, float compression, bool grounded, float verticalSpeed, float dt)
        {
            float targetTilt = -Mathf.Clamp(steer, -1, 1) * maximumTilt * Mathf.Clamp(speed / Mathf.Max(.1f, fullTiltSpeed), -1, 1);
            tilt = Mathf.SmoothDampAngle(tilt, targetTilt, ref tiltVelocity, Mathf.Max(.001f, bodySmoothTime), Mathf.Infinity, dt);
            if (sampled)
            {
                bounceVelocity += Mathf.Clamp(compression - lastCompression, -.04f, .04f) * 5;
                if (grounded && !lastGrounded) bounceVelocity -= Mathf.Min(8, Mathf.Max(0, -lastVerticalSpeed)) * landingResponse * .08f;
            }
            sampled = true; lastCompression = compression; lastGrounded = grounded; lastVerticalSpeed = verticalSpeed;
            float remaining = Mathf.Min(dt, .1f);
            while (remaining > 0)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                bounceVelocity += (-Mathf.Clamp(spring, 1, 400) * bounce - Mathf.Clamp(damping, 0, 50) * bounceVelocity) * step;
                bounce = Mathf.Clamp(bounce + bounceVelocity * step, -suspensionTravel, suspensionTravel);
                remaining -= step;
            }
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = bodyRestPosition + Vector3.up * bounce;
                bodyRoot.localRotation = bodyRestRotation * Quaternion.Euler(0, 0, tilt);
            }
        }

        public void ResetMotion()
        {
            tilt = tiltVelocity = bounce = bounceVelocity = 0; sampled = false;
            if (bodyRoot != null) { bodyRoot.localPosition = bodyRestPosition; bodyRoot.localRotation = bodyRestRotation; }
        }
        void OnDisable() { ResetMotion(); }
        void OnDestroy()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            foreach (var pair in originalMeshes) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
            foreach (var material in ownedMaterials) VisualMaterialLibrary.Dispose(material);
            foreach (var mesh in ownedMeshes) VisualMaterialLibrary.Dispose(mesh);
        }
    }
}
