using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    public int type; //1 = health, 2 = armour, 3 = rifle, 4 = smg, 5 = shotgun
    public int amount;

    public void Initialise(int type, int amount)
    {
        this.type = type;
        this.amount = amount;
    }
}