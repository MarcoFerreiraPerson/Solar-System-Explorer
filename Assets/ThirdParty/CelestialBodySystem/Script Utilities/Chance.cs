using UnityEngine;

public class Chance
{
    private float value;

    public Chance(System.Random prng)
    {
        value = (float)prng.NextDouble();
    }

    public Chance(PRNG prng)
    {
        value = prng.Value();
    }

    public bool Percent(float percent)
    {
        if (value <= 0f)
        {
            return false;
        }

        float t = percent / 100f;
        value -= t;
        return value <= 0f;
    }
}
