using UnityEngine;

public struct BounceImpactData
{
    public int damage;
    public Vector2 direction;
    public GameObject source;

    public BounceImpactData(int damage, Vector2 direction, GameObject source)
    {
        this.damage = damage;
        this.direction = direction;
        this.source = source;
    }
}
