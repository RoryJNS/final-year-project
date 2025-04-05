using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    [SerializeField] private int maxHealth, health;

    private void Start()
    {
        ResetObject();
    }

    public void ResetObject()
    {
        health = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;

        if (health <= 0)
        {
            gameObject.SetActive(false); // Return this object to the pool
        }
    }
}