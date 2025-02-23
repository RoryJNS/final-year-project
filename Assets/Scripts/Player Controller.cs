using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 10f, controllerDeadzone = 0.1f;
    [SerializeField] private bool isGamepad;
    [SerializeField] private Transform reticle;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private ObjectPooler pooler;
    [SerializeField] private HudManager hudManager;

    private Vector3 reticleWorldPos;
    private Vector2 moveInput, lookDirection;
    private Controls controls;

    private void Awake()
    {
        controls = new Controls();
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.started += OnAttack;
        controls.Player.Attack.canceled += OnAttack;
        controls.Player.Interact.performed += OnInteract;
        controls.Player.Finisher.started += OnFinisher;
        controls.Player.DropWeapon.performed += OnForceDropWeapon;
        controls.Player.Reload.performed += OnReload;
    }

    private void OnDisable()
    {
        controls.Disable();
        controls.Player.Attack.started -= OnAttack;
        controls.Player.Attack.canceled -= OnAttack;
        controls.Player.Interact.performed -= OnInteract;
        controls.Player.Finisher.started -= OnFinisher;
        controls.Player.DropWeapon.performed -= OnForceDropWeapon;
        controls.Player.Attack.performed -= OnReload;
    }

    private void FixedUpdate()
    {
        if (playerAttack.meleeAttacking) return;
        if (playerAttack.performingFinisher) return;
        HandleMovement();
        HandleRotation();
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleMovement()
    {
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        animator.SetBool("Moving", moveInput != Vector2.zero);
        if (moveInput.sqrMagnitude > controllerDeadzone * controllerDeadzone)
        {
            transform.position += (Vector3)(moveSpeed * Time.deltaTime * moveInput);
        }
    }

    private void HandleRotation()
    {
        reticleWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(reticle.position.x, reticle.position.y, mainCamera.nearClipPlane));
        lookDirection = (Vector2) reticleWorldPos - (Vector2) mainCamera.transform.position; // Reticle position relative to the camera

        if (lookDirection.sqrMagnitude > 0.001f) // Avoid jittering or dividing by zero
        {
            lookDirection += rb.position - (Vector2) mainCamera.transform.position; // Factor the camera offset from the player, caused by 'look ahead', into where to look
            rb.rotation = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg; // Rotate the player to face the lookDirection
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            switch (playerAttack.weaponType)
            {
                case PlayerAttack.WeaponType.Melee:
                case PlayerAttack.WeaponType.Shotgun:
                case PlayerAttack.WeaponType.RPG:
                    playerAttack.Attack();
                    break;
                case PlayerAttack.WeaponType.Rifle:
                case PlayerAttack.WeaponType.SMG:
                    playerAttack.HoldAttack();
                    break;
            }
        }

        if (context.canceled && (playerAttack.weaponType == PlayerAttack.WeaponType.Melee || 
            playerAttack.weaponType == PlayerAttack.WeaponType.Rifle || playerAttack.weaponType == PlayerAttack.WeaponType.SMG))
        {
            playerAttack.StopHoldAttack();
        }
    }
    
    private void OnInteract(InputAction.CallbackContext context)
    {
        Collider2D interactCollider = Physics2D.OverlapCircle(transform.position, 0.7f, LayerMask.GetMask("Pickups"));

        if (interactCollider == null) return;

        if (interactCollider.CompareTag("Weapon"))
        {
            if (!interactCollider.TryGetComponent<ResourcePickup>(out var newWeapon)) return;

            DropWeapon(interactCollider.transform.position);
            interactCollider.gameObject.SetActive(false);
            playerAttack.SetWeapon((PlayerAttack.WeaponType)newWeapon.type, newWeapon.amount);
        }
    }
    
    private void OnFinisher(InputAction.CallbackContext context)
    {
        if (playerAttack.performingFinisher) return; // Prevent spamming finishers

        Enemy nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        float finisherRange = 4f; // Adjust the range as needed

        if (DungeonGenerator.Instance.currentMainRoom.roomNumber > 0) // Check there is an active enemy cluster
        {
            foreach (Enemy enemy in DungeonGenerator.Instance.FindStaggeredEnemies()) // Find the nearest staggered enemy
            {
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < finisherRange && distance < nearestDistance)
                {
                    Vector2 direction = (enemy.transform.position - transform.position).normalized;
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, LayerMask.GetMask("Default"));

                    if (hit.collider != null && hit.collider.CompareTag("Enemy")) // Only attempt a finisher if the player has LoS on the enemy
                    {
                        nearestDistance = distance;
                        nearestEnemy = enemy;
                    }
                }
            }
        }

        if (nearestEnemy != null)
        {
            playerAttack.performingFinisher = true; 
            StartCoroutine(PerformFinisher(nearestEnemy));
        }
    }

    private System.Collections.IEnumerator PerformFinisher(Enemy enemy)
    {
        if (enemy.TryGetComponent(out Collider2D collider))
        {
            collider.enabled = false; // Allow for some overlapping during animation

        }

        // Snap the enemy to face the player
        Vector2 direction = (transform.position - enemy.transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        enemy.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Snap the player to face the enemy
        Vector2 playerDirection = (enemy.transform.position - transform.position).normalized;
        float playerAngle = Mathf.Atan2(playerDirection.y, playerDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, playerAngle);

        // Smoothly close the distance to the enemy
        while (Vector3.Distance(transform.position, enemy.transform.position) > 0.1f)
        {
            // Move towards the enemy at a constant speed
            transform.position = Vector3.MoveTowards(transform.position, enemy.transform.position, moveSpeed * Time.deltaTime);
            yield return null;
        }

        hudManager.ZoomCamera(4f, 0.25f); // Zoom the camera in
        animator.SetTrigger("Finisher"); // Player execution animation
        enemy.Finish(); // Enemy finished animation and other logic
        yield return new WaitForSeconds(.5f); 
        hudManager.ZoomCamera(5f, 0.25f); // Zoom the camera back out
        yield return new WaitForSeconds(.25f); // Wait for animations to play out
        playerAttack.ReplenishArmour(1);
        playerAttack.performingFinisher = false;
    }

    private void OnReload(InputAction.CallbackContext context)
    {
        playerAttack.Reload();
    }

    private void DropWeapon(Vector2 dropPosition)
    {
        if (playerAttack.weaponType == PlayerAttack.WeaponType.Melee) return;

        // Get a pickup from the pool
        GameObject droppedWeapon = pooler.GetFromPool(playerAttack.weaponType.ToString(), dropPosition, Quaternion.identity);

        if (droppedWeapon.TryGetComponent(out ResourcePickup resourcePickup))
        {
            resourcePickup.Initialise((int)playerAttack.weaponType, playerAttack.currentAmmo);
        }

        playerAttack.StopHoldAttack();
        playerAttack.SetWeapon(0, 1); // Reset to unarmed
    }

    private void OnForceDropWeapon(InputAction.CallbackContext context)
    {
        DropWeapon(new Vector2(transform.position.x, transform.position.y));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.TryGetComponent<ResourcePickup>(out var resourcePickup)) return;

        if (collision.CompareTag("Health"))
        {
            playerAttack.AddHealth(resourcePickup.amount);
            collision.gameObject.SetActive(false);
        }
        else if (collision.CompareTag("Armour"))
        {
            playerAttack.ReplenishArmour(4);
            collision.gameObject.SetActive(false);
        }
        else if (collision.CompareTag("Ammo"))
        {
            playerAttack.AddReserveAmmo(resourcePickup.type, resourcePickup.amount);
            collision.gameObject.SetActive(false);
        }
    }

    public void OnDeviceChange(PlayerInput pi)
    {
        isGamepad = pi.currentControlScheme.Equals("Gamepad");
    }
}