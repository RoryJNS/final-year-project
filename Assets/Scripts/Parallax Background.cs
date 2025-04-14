using UnityEngine;

public class BackgroundController : MonoBehaviour
{
    [SerializeField] private GameObject cam;
    [SerializeField] private float parallaxEffect;
    private float startPos, width;

    private void Start()
    {
        startPos = transform.position.x;
        width = GetComponent<SpriteRenderer>().bounds.size.x;

    }

    private void FixedUpdate()
    {
        float distance = cam.transform.position.x * parallaxEffect;
        float movement = cam.transform.position.x * (1 - parallaxEffect);
        transform.position = new(startPos + distance, transform.position.y , transform.position.z);

        // If background has reached the end of its width then adjust its position for infinite scrolling
        if (movement > startPos + width) { startPos += width; }
        else if (movement < startPos - width) { startPos -= width; }
    }
}