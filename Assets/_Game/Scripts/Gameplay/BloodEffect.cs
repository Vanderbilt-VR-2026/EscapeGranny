using UnityEngine;

/// <summary>
/// A short spray of blood drops where the Prisoner was hit. Built in code (a small particle burst
/// that falls with gravity and fades), so there's no prefab to set up. PrisonerHealth calls it on
/// both headsets whenever the Prisoner takes a hit.
/// </summary>
public static class BloodEffect
{
    // Kept small on purpose: Quest draws every particle, and a few dozen is plenty to read as blood.
    private const int Drops = 30;

    public static void Spawn(Vector3 position, Vector3 direction, Material material)
    {
        if (material == null) return;

        var go = new GameObject("Blood Spray");
        go.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.up));

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // settings can only change while stopped

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0f, 0f), new Color(0.25f, 0f, 0f));
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy; // cleans itself up when the drops are gone

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Drops) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.03f;

        // Drops shrink as they fall, so they fade out instead of popping away.
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
    }
}
