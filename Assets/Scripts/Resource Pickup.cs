using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    public int type; // 1 = rifle, 2 = smg, 3 = shotgun, 4 = health, 5 = armour
    public int amount;

    public void Initialise(int type, int amount)
    {
        this.type = type;
        this.amount = amount;
    }
}