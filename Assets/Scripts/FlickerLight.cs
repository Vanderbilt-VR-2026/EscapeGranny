// FlickerLight.cs
//
// Makes a light buzz and dip like a dying bulb, with the occasional short blackout.
// HouseBuilder's "Apply Horror Lighting" adds this to a few lamps automatically,
// but you can drop it on any Light by hand too.
//
// Keep this file OUTSIDE any Editor folder (e.g. Assets/Scripts/), because it runs in the game.

using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Tooltip("Brightest the bulb gets")]
    public float maxIntensity = 1f;

    [Tooltip("Dimmest the bulb gets while flickering (not counting blackouts)")]
    public float minIntensity = 0.3f;

    [Tooltip("How fast the brightness wobbles")]
    public float speed = 6f;

    [Tooltip("Roughly how many short blackouts per minute")]
    public float blackoutsPerMinute = 4f;

    Light lamp;
    float seed;          // so lamps don't all flicker in sync
    float blackoutUntil; // time when the current blackout ends

    void Awake()
    {
        lamp = GetComponent<Light>();
        seed = Random.value * 100f;
    }

    void Update()
    {
        // In a blackout: stay off until it ends
        if (Time.time < blackoutUntil)
        {
            lamp.intensity = 0f;
            return;
        }

        // Small chance each frame to start a blackout of 0.05 to 0.3 seconds
        if (Random.value < blackoutsPerMinute / 60f * Time.deltaTime)
        {
            blackoutUntil = Time.time + Random.Range(0.05f, 0.3f);
            lamp.intensity = 0f;
            return;
        }

        // Otherwise wobble smoothly between min and max
        float noise = Mathf.PerlinNoise(seed, Time.time * speed);
        lamp.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}
