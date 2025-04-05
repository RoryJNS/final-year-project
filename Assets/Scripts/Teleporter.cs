using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class Teleporter : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lockIcon;
    [SerializeField] private Collider2D collider2d;
    public DungeonGenerator.MainRoom linkedMainRoom; // Room that this teleporter goes to
    private Teleporter destination;
    private Image fadeImage;
    private CinemachineCamera cinemachineCamera;
    private CinemachineRotationComposer composer;

    private void Awake()
    {
        fadeImage = GameObject.Find("Fade image").GetComponent<Image>();
        cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        composer = cinemachineCamera.GetComponent<CinemachineRotationComposer>();
    }
    
    private System.Collections.IEnumerator OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Player"))
        {
            // Temporarily disable camera damping
            composer.Damping = Vector2.zero;

            yield return StartCoroutine(FadeOut()); // Wait for fade-out to complete

            // Teleport the player
            collider.transform.position = destination.transform.position + 2 * (destination.transform.position - transform.position).normalized;

            if (linkedMainRoom != null && (DungeonGenerator.Instance.currentMainRoom == null || linkedMainRoom.roomNumber > DungeonGenerator.Instance.currentMainRoom.roomNumber))
            {
                DungeonGenerator.Instance.currentMainRoom = linkedMainRoom; // Update the current main room
                DungeonGenerator.Instance.SpawnEnemies(); // Spawn enemies in the next main room
                ScoreSystem.Instance.ProceedToNextRoom();
                linkedMainRoom.LockRoom();
            }

            yield return StartCoroutine(FadeIn()); // Wait for fade-out to complete

            // Restore original damping
            composer.Damping = new(0.3f, 0.3f);
        }
    }

    public void Lock()
    {
        collider2d.isTrigger = false;
        lockIcon.enabled = true;
    }

    public void Unlock()
    {
        collider2d.isTrigger = true;
        lockIcon.enabled = false;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < .3f)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsed / .3f);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f; // Ensure it's fully opaque
        fadeImage.color = color;
        StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < .3f)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Clamp01(1f - (elapsed / .3f));
            fadeImage.color = color;
            yield return null;
        }

        color.a = 0f; // Ensure it's fully transparent
        fadeImage.color = color;
    }

    public void SetDestination(Teleporter newDestination)
    {
        destination = newDestination;
    }
}