using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    [SerializeField] private int maxHealth, health;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color originalColor;

    private void Start()
    {
        originalColor = Color.white;
        ResetObject();
    }

    public void ResetObject()
    {
        health = maxHealth;
        spriteRenderer.color = originalColor;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;

        if (health <= 0)
        {            
            StartCoroutine(FadeOutAndDisable(1f, 2f)); // Start fading after 0.5s, fade over 1s
        }
    }

    private System.Collections.IEnumerator FadeOutAndDisable(float delay, float fadeDuration)
    {
        yield return new WaitForSeconds(delay); // Wait for animation to play

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        gameObject.SetActive(false); // Return this object to the pool
    }
}