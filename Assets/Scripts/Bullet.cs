using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    public GameObject Shooter;
    private Coroutine fadeCoroutine;
    private readonly int damage = 5;

    [SerializeField] private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // Fully opaque
        fadeCoroutine = StartCoroutine(FadeAndDestroy());
    }

    private void OnDisable()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject == Shooter) return; // Prevent self-hit

        if (collision.gameObject.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.TakeDamage(damage);
            ScoreSystem.Instance.RegisterHit(PlayerAttack.WeaponType.Shotgun, 0.2f);
        }

        else if (collision.gameObject.TryGetComponent<PlayerAttack>(out var playerAttack))
        {
            playerAttack.TakeDamage(damage);
        }

        else if (collision.gameObject.TryGetComponent<DestructibleObject>(out var destructible))
        {
            destructible.TakeDamage(damage);
        }
    }

    private IEnumerator FadeAndDestroy()
    {
        float elapsedTime = 0f;

        while (elapsedTime < 0.4f)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(1f, 0f, elapsedTime * 2.5f);
            Color currentColor = spriteRenderer.color;
            currentColor.a = newAlpha;
            spriteRenderer.color = currentColor;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}