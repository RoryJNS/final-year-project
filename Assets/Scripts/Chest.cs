using UnityEngine;
using System.Collections;

public class Chest : MonoBehaviour
{
    public Coroutine dropLootCoroutine;
    [SerializeField] private Animator animator;

    private void Start()
    {
        if (gameObject.CompareTag("Start Chest"))
        {
            StartCoroutine(DropLootCoroutine(0));
        }
    }

    public void Open()
    {
        dropLootCoroutine ??= StartCoroutine(DropLootCoroutine(0.583f));
    }

    private IEnumerator DropLootCoroutine(float delay)
    {
        animator.SetTrigger("Opened");
        if (!gameObject.CompareTag("Start Chest")) { SoundManager.PlaySound(SoundManager.SoundType.CHESTOPEN); }
        yield return new WaitForSeconds(delay);

        for (int i = 0; i < 3; i++)
        {
            GameObject loot = LootSystem.Instance.DropLoot(transform.position);

            if (loot != null)
            {
                StartCoroutine(AnimateLoot(loot));
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private Vector3 GetRandomDropPosition()
    {
        Vector3 dropPosition;

        do
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(1, 2);
            dropPosition = transform.position + new Vector3(randomDirection.x, 0, randomDirection.y) * randomDistance;
        }
        while (Physics2D.OverlapCircle(dropPosition, 0.5f)); // Prevent overlap with other colliders

        return dropPosition;
    }

    private IEnumerator AnimateLoot(GameObject loot)
    {
        BoxCollider2D collider = loot.GetComponent<BoxCollider2D>();
        collider.enabled = false;
        Vector3 start = loot.transform.position;
        float duration = 0.5f;
        float elapsed = 0f;
        Vector2 targetPosition = GetRandomDropPosition();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            loot.transform.position = Vector3.Lerp(start, targetPosition, t) + new Vector3(0, Mathf.Sin(t * Mathf.PI) * 1.5f, 0);
            yield return null;
        }

        collider.enabled = true;
    }
}