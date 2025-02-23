using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Enemy : MonoBehaviour
{
    private enum WeaponType { Rifle, SMG, Shotgun };
    private enum State { Roaming, Investigating, Chasing, Attacking, Staggered };
    private Vector3 startingPosition, roamPosition;
    private float angleStep, startAngle, timeSinceLastLOS;
    private PlayerAttack playerAttack;
    private ObjectPooler pooler;
    private EnemyCluster cluster;
    private Coroutine staggeredCoroutine;

    private readonly Color originalColor = Color.white;
    private readonly int FOV = 80;
    private bool hasBeenStaggered;

    [Header("Taking damage")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private DamageFlash flash;
    [SerializeField] private Collider2D coll;
    [SerializeField] private int maxHealth, health;
    [SerializeField] private float staggerDuration;

    [Header("Movement")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private State state;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Header("Attacking")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private float reloadSpeed, attackRange, fireRate, lastAttackTime, waitTime, shotgunSpreadAngle, accuracy;
    [SerializeField] private int damage, ammo, maxAmmo;

    private void Awake()
    {
        playerAttack = GameObject.Find("Player").GetComponent<PlayerAttack>();
        pooler = GameObject.Find("Game Manager").GetComponent<ObjectPooler>();
        agent.updateRotation = false;
        agent.updateUpAxis = false; // Needed for 2D NavMesh
    }

    public void SetCluster(EnemyCluster cluster)
    {
        this.cluster = cluster;
    }

    private void Start()
    {
        ResetEnemy();

        // Calculate the angle offset for each shotgun pellet to be evenly distributed within the spread angle
        angleStep = shotgunSpreadAngle / (4);
        startAngle = -shotgunSpreadAngle / 2; // Start at half of the spread angle to the left
    }

    public void ResetEnemy()
    {
        startingPosition = transform.position;
        agent.Warp(startingPosition);
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        roamPosition = GetRoamingPosition();
        animator.SetBool("Alert", false);
        state = State.Roaming;
        coll.enabled = true;
        hasBeenStaggered = false;
        health = maxHealth;
        spriteRenderer.color = originalColor;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        cluster.Alert(playerAttack.transform.position);

        if (health <= 0)
        {
            animator.SetTrigger("Death"); // Trigger death animation
            StartCoroutine(Die(false)); // Start fading after 1s, fade over 2s
        }
        else if (health < maxHealth * 0.4 && !hasBeenStaggered) // Less than 40% health and hasn't been staggered
        {
            animator.SetBool("Alert", false);
            animator.SetTrigger("Staggered");
            state = State.Staggered;
            hasBeenStaggered = true;
            agent.isStopped = true;
            cluster.staggeredEnemies.Add(this);
            staggeredCoroutine = StartCoroutine(Staggered());
        }
        else if (state != State.Staggered) // More than 40% health and not staggered
        {
            flash.CallDamageFlash();
            animator.SetBool("Alert", true);
            timeSinceLastLOS = 0;
            state = State.Chasing;
        }
    }

    private IEnumerator Die(bool wasFinisher)
    {
        ScoreSystem.Instance.RegisterKill(gameObject.transform, wasFinisher);

        if (staggeredCoroutine != null)
        {
            StopCoroutine(staggeredCoroutine);
            staggeredCoroutine = null;
            spriteRenderer.color = originalColor;
            cluster.staggeredEnemies.Remove(this);
        }

        cluster.RemoveEnemy(this);
        cluster.staggeredEnemies.Remove(this);
        agent.isStopped = true;
        coll.enabled = false;
        LootSystem.Instance.DropLoot(weaponType.ToString() + " Enemy", transform.position); // Drop loot according to this enemy type
        yield return new WaitForSeconds(1); // Wait for death animation to play
        
        float elapsedTime = 0f;

        while (elapsedTime < 2)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / 2);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha); // Decrease the alpha
            yield return null;
        }

        gameObject.SetActive(false); // Return this object to the pool
    }

    private IEnumerator Staggered()
    {
        float flashInterval = 0.2f; // How fast the enemy flashes
        float elapsedTime = 0f;
        bool isFlashing = false;

        while (elapsedTime < staggerDuration)
        {
            spriteRenderer.color = isFlashing ? originalColor : Color.red;
            isFlashing = !isFlashing;

            yield return new WaitForSeconds(flashInterval);
            elapsedTime += flashInterval;
        }

        spriteRenderer.color = originalColor; // Reset color after stagger ends
        agent.isStopped = false; // Re-enable agent movement
        cluster.staggeredEnemies.Remove(this);
        animator.SetBool("Alert", true);
        state = State.Chasing; // Return to normal AI behavior
    }

    public void Finish()
    {
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + 180);
        animator.SetTrigger("Finished"); // Alternate death animation
        StartCoroutine(Die(true));
    }

    private void Update()
    {
        if (agent.isStopped) { return; } // This enemy has been killed or staggered

        timeSinceLastLOS += Time.deltaTime;
        CheckForPlayer();

        switch (state)
        {
            case State.Roaming: // Roam around randomly
                Roam();
                break;
            case State.Investigating:
                Investigate();
                break;
            case State.Chasing: // Chase the player until they are in line of sight
                Chase();
                break;
            case State.Attacking: // Stay still and shoot the player
                HandleAttacking();
                break;
            default:
                break;
        }

        if (agent.velocity.sqrMagnitude > 0.1f) // If moving, rotate smoothly in direction of movement
        {
            float angle = Mathf.Atan2(agent.velocity.y, agent.velocity.x) * Mathf.Rad2Deg;
            float smoothedAngle = Mathf.LerpAngle(rb.rotation, angle, Time.deltaTime * 20);
            rb.MoveRotation(smoothedAngle);
        }

        // Player fired a shot nearby
        if (Time.time - playerAttack.lastAttackTime == 0 && Vector3.Distance(transform.position, playerAttack.transform.position) < attackRange + 2)
        {
            cluster.Alert(playerAttack.transform.position);
        }

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void CheckForPlayer()
    {
        int rayCount = 10; // Number of rays to cast
        float angleStep = FOV / (rayCount - 1); // Angle difference between rays

        for (int i = 0; i < rayCount; i++)
        {
            float angleOffset = -FOV / 2 + (angleStep * i);
            Vector2 rayDirection = Quaternion.Euler(0, 0, angleOffset) * firePoint.right;
            RaycastHit2D hit = Physics2D.Raycast(firePoint.position, rayDirection, attackRange, LayerMask.GetMask("Default", "Player"));

            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                cluster.Alert(hit.collider.transform.position); // Alert nearby enemies
                timeSinceLastLOS = 0f;
                return; // Stop checking after first detection
            }
        }
    }

    private void Roam()
    {
        agent.SetDestination(roamPosition);

        if (Vector3.Distance(transform.position, roamPosition) < 0.5f)
        {
            if (waitTime < 0f)
            {
                waitTime = 1f;
                roamPosition = GetRoamingPosition(); // Then go to the next roamPosition
            }
            else
            {
                waitTime -= Time.deltaTime;
            }
        }

        // This enemy spotted the player
        if (timeSinceLastLOS == 0)
        {
            animator.SetBool("Alert", true);
            state = State.Chasing;
        }

        // This or another enemy detected the player nearby
        if (cluster.investigateTimer < 0.3 && Vector3.Distance(transform.position, cluster.investigatePos) < attackRange + 2)
        {
            animator.SetBool("Alert", true);
            state = State.Investigating;
        }
    }

    private void Chase()
    {
        if (timeSinceLastLOS == 0)
        {
            state = State.Attacking; // Start shooting player
        }
        else if (timeSinceLastLOS < 1) 
        {
            agent.SetDestination(playerAttack.transform.position); // Infer where the player is and go there
        }
        else
        {
            cluster.investigatePos = playerAttack.transform.position;
            state = State.Investigating;
        }
    }

    private void Investigate()
    {
        agent.SetDestination(cluster.investigatePos);

        if (timeSinceLastLOS == 0) // Investigation successful and player found
        {
            state = State.Attacking;
        }
        
        if (cluster.investigateTimer > 5) // 5 seconds have passed since this enemy was alerted
        {
            animator.SetBool("Alert", false);
            state = State.Roaming;
        }
    }

    private void HandleAttacking()
    {
        agent.SetDestination(transform.position); // Stay still

        if (timeSinceLastLOS != 0) // Go back to chasing if can't see the player
        {
            state = State.Chasing;
        }

        if (ammo > 0 && Time.time - lastAttackTime > fireRate) // Fire if there is ammo
        {
            lastAttackTime = Time.time;
            Attack();
        }
        else if (ammo <= 0 && Time.time - lastAttackTime > reloadSpeed) // Reload if out of ammo
        {
            ammo = maxAmmo;
            lastAttackTime = Time.time;
        }

        Vector3 direction = (playerAttack.transform.position - transform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rb.MoveRotation(angle);
    }

    private Vector3 GetRoamingPosition()
    {
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        Vector3 targetPosition = startingPosition + randomDir * Random.Range(2f, 4f); // Roaming distance

        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            return hit.position; // Return valid position on NavMesh
        }

        return startingPosition; // If no valid position is found, stay at starting position
    }

    private void Attack()
    {
        if (weaponType == WeaponType.Rifle || weaponType == WeaponType.SMG)
        {
            FireRaycast();
        }
        else
        {
            FireProjectile();
        }
        ammo--;
    }

    private void FireRaycast()
    {
        float maxAngle = (1f - accuracy) * 60f; // 60 degrees max offset for 0 accuracy
        float angleOffset = Random.Range(-maxAngle, maxAngle);
        Vector2 firingDirection = Quaternion.Euler(0, 0, angleOffset) * firePoint.right;
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, firingDirection, 25, LayerMask.GetMask("Default", "Player"));
        Vector2 targetPosition = hit.collider ? hit.point : (Vector2)firePoint.position + (firingDirection * 25);
        GameObject trail = pooler.GetFromPool("Bullet Trail", firePoint.position, Quaternion.identity);
        StartCoroutine(MoveTrail(trail, targetPosition));

        if (hit.collider)
        {
            // Check for PlayerController and apply damage
            if (hit.collider.TryGetComponent<PlayerAttack>(out var playerAttack))
            {
                playerAttack.TakeDamage(damage);
            }
        }
    }

    private IEnumerator MoveTrail(GameObject trail, Vector2 targetPosition)
    {
        while ((Vector2)trail.transform.position != targetPosition)
        {
            float step = 60 * Time.deltaTime; // 60 units per frame
            trail.transform.position = Vector2.MoveTowards(trail.transform.position, targetPosition, step); // Move towards target
            yield return null; // Wait for the next frame
        }

        trail.SetActive(false);
    }

    private void FireProjectile()
    {
        if (weaponType == WeaponType.Shotgun)
        {
            for (int i = 0; i < 5; i++)
            {
                Quaternion pelletRotation = firePoint.rotation * Quaternion.Euler(0, 0, startAngle + (angleStep * i));
                GameObject bullet = pooler.GetFromPool("Bullet", firePoint.position, pelletRotation);
                bullet.GetComponent<Bullet>().Shooter = gameObject;
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
                bulletRb.AddForce(bullet.transform.right * 20f, ForceMode2D.Impulse);
            }
        }
        // add rpg logic here
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float maxAngle = FOV / 2f;
        Vector2 leftBound = Quaternion.Euler(0, 0, -maxAngle) * firePoint.right;
        Vector2 rightBound = Quaternion.Euler(0, 0, maxAngle) * firePoint.right;
        Gizmos.DrawRay(firePoint.position, leftBound * attackRange);  // Left boundary
        Gizmos.DrawRay(firePoint.position, rightBound * attackRange); // Right boundary

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attackRange + 2); // Hearing range
    }
}