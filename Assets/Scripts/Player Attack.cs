using UnityEngine;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    public enum WeaponType { Melee, Rifle, SMG, Shotgun };
    public WeaponType weaponType;
    public int currentAmmo;
    public bool meleeAttacking, performingFinisher;

    [SerializeField] private int health, maxHealth, armour, maxArmour;
    [SerializeField] private DamageFlash damageFlash;
    [SerializeField] private Transform firePoint; // Offset to send a projectile/raycast from depending on the weapon
    [SerializeField] private GameObject progressWheel;
    [SerializeField] private Unity.Cinemachine.CinemachineImpulseSource impulseSource;
    [SerializeField] private TMPro.TMP_Text ammoText;
    [SerializeField] private HudManager hudManager;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private WeaponStats[] weaponStats; // Array to store stats for each weapon type
    [SerializeField] private int shotgunPelletCount, maxMeleeDamage;
    [SerializeField] private float shotgunSpreadAngle, maxMeleeChargeTime;
    [SerializeField] private Collider2D collider2d;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Coroutine holdFireCoroutine, reloadCoroutine, ammoGainedCoroutine;
    public float lastAttackTime;
    private float angleStep, startAngle;
    [SerializeField] private bool holdFiring, isAlternateAttack;
    [SerializeField] private int initialHealthArmour;

    [System.Serializable]
    private struct WeaponStats
    {
        public int damage;
        public float fireRate, recoil, reloadSpeed;
        public int ammoPerClip, reserveAmmo;
    }

    private void Start()
    {
        // Calculate the angle offset for each pellet to be evenly distributed within the spread angle
        angleStep = shotgunSpreadAngle / (shotgunPelletCount - 1);
        startAngle = -shotgunSpreadAngle / 2; // Start at half of the spread angle to the left
        hudManager.InitialiseHealthAndArmour(maxHealth, maxArmour);
        initialHealthArmour = health + armour;
    }

    public void SetWeapon(WeaponType type, int ammo)
    {
        weaponType = type;

        if (weaponType == WeaponType.Melee)
        {
            collider2d.offset = new(0, collider2d.offset.y);
        }
        else
        {
            collider2d.offset = new(-0.05f, collider2d.offset.y);
        }

        currentAmmo = ammo;
        hudManager.UpdateReticle((int)type);
        UpdateAmmoUI();
        progressWheel.SetActive(false);
        holdFiring = false;
        animator.SetFloat("Weapon Type", (int)type);
        CancelReload();
        ammoGainedCoroutine ??= StartCoroutine(AnimateAmmoText());
    }

    public void Attack()
    {
        if (performingFinisher) { return; }

        if (reloadCoroutine != null || Time.time - lastAttackTime < weaponStats[(int)weaponType].fireRate) return;

        if (currentAmmo == 0)
        {
            Reload();
            return;
        }

        lastAttackTime = Time.time;

        switch (weaponType)
        {
            case WeaponType.Rifle:
            case WeaponType.SMG:
                FireRaycast();
                currentAmmo--;
                break;
            case WeaponType.Shotgun:
                FireProjectile();
                currentAmmo--;
                break;
            case WeaponType.Melee:
                holdFiring = true;
                StartCoroutine(ChargeMeleeAttack());
                return;
        }

        impulseSource.GenerateImpulseWithForce(weaponStats[(int)weaponType].recoil);
        UpdateAmmoUI();
    }

    public void HoldAttack()
    {
        if (holdFiring) return;
        if (currentAmmo <= 0)
        {
            Reload();
            return;
        }
        holdFireCoroutine = StartCoroutine(HoldFireRoutine());
    }

    private IEnumerator HoldFireRoutine()
    {
        holdFiring = true;
        while (currentAmmo > 0 && reloadCoroutine == null)
        {
            Attack();
            yield return new WaitForSeconds(weaponStats[(int)weaponType].fireRate);
        }
    }

    private IEnumerator ChargeMeleeAttack()
    {
        float elapsedTime = 0f;

        while (holdFiring && elapsedTime < maxMeleeChargeTime)
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime > 0.2)
            {
                progressWheel.SetActive(true);
                float progress = Mathf.Clamp01(elapsedTime / maxMeleeChargeTime);
                hudManager.SetProgressWheel(progress);
            }

            yield return null; // Wait for the next frame
        }
    }

    public void StopHoldAttack()
    {
        if (weaponType == WeaponType.Melee && holdFiring) // Unarmed charge attack
        {
            StopCoroutine(ChargeMeleeAttack());
            ExecuteMeleeAttack();
        }
        else if (holdFireCoroutine != null)
        {
            StopCoroutine(holdFireCoroutine);
            holdFiring = false;
        }
    }

    private void ExecuteMeleeAttack()
    {
        holdFiring = false;
        progressWheel.SetActive(false);
        float chargeDuration = Mathf.Clamp(Time.time - lastAttackTime, 0, maxMeleeChargeTime);
        float chargeFactor = chargeDuration / maxMeleeChargeTime;
        float scaledDamage = Mathf.Lerp(2, weaponStats[0].damage, chargeFactor);
        StartCoroutine(MeleeAttack((int)scaledDamage, chargeFactor));
    }

    private IEnumerator MeleeAttack(int scaledDamage, float chargeFactor)
    {
        animator.SetTrigger(isAlternateAttack ? "Kick" : "Punch");
        isAlternateAttack = !isAlternateAttack;

        if (chargeFactor == 1)
        {
            meleeAttacking = true;
            Vector2 startPosition = transform.position;
            Vector2 targetPosition = startPosition + (Vector2)transform.right * 5;

            // Check if there's anything in the way of the attack
            RaycastHit2D hit = Physics2D.Raycast(startPosition, transform.right, 5, LayerMask.GetMask("Default"));
            if (hit.collider != null)
            {
                targetPosition = hit.point; // Make the attack stop at the collision point
            }

            float elapsedTime = 0f;

            while (elapsedTime < 0.3f)
            {
                transform.position = Vector2.Lerp(startPosition, targetPosition, elapsedTime / 0.3f);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            transform.position = targetPosition; // Ensure final position is set
            meleeAttacking = false;
        }

        impulseSource.GenerateImpulseWithForce(chargeFactor / 3);
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(firePoint.position, 0.3f, LayerMask.GetMask("Default"));

        foreach (var enemyCollider in hitEnemies)
        {
            if (enemyCollider.TryGetComponent<Enemy>(out var enemy))
            {
                enemy.TakeDamage(scaledDamage);
                ScoreSystem.Instance.RegisterHit(weaponType, weaponStats[(int)weaponType].fireRate);
            }
        }
    }

    public void Reload()    
    {
        if (reloadCoroutine != null || reloadCoroutine!=null || performingFinisher || currentAmmo == weaponStats[(int)weaponType].ammoPerClip || weaponStats[(int)weaponType].reserveAmmo == 0) return;
        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        progressWheel.SetActive(true);
        float elapsedTime = 0f;

        while (elapsedTime < weaponStats[(int)weaponType].reloadSpeed)
        {
            if (performingFinisher) 
            {
                CancelReload();
                yield break;
            }

            elapsedTime += Time.deltaTime;

            // Calculate progress as a value between 0 and 1
            float progress = Mathf.Clamp01(elapsedTime / weaponStats[(int)weaponType].reloadSpeed);

            hudManager.SetProgressWheel(progress);
            yield return null; // Wait for the next frame
        }

        // Transfer ammo from reserve to current ammo
        int ammoNeeded = weaponStats[(int)weaponType].ammoPerClip - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, weaponStats[(int)weaponType].reserveAmmo);
        currentAmmo += ammoToLoad;
        weaponStats[(int)weaponType].reserveAmmo -= ammoToLoad;
        UpdateAmmoUI();

        progressWheel.SetActive(false);
        reloadCoroutine = null;

        if (holdFiring)
        {
            holdFireCoroutine = StartCoroutine(HoldFireRoutine());
        }
    }

    private void CancelReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
            progressWheel.SetActive(false);
        }
    }

    private void UpdateAmmoUI()
    {
        hudManager.SetTransparent(currentAmmo == 0);
        ammoText.text = weaponType == WeaponType.Melee ? "" : $"{currentAmmo}/{weaponStats[(int)weaponType].reserveAmmo}";
    }

    private void FireRaycast()
    {
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, firePoint.right, 25, LayerMask.GetMask("Default"));
        Vector2 targetPosition = hit.collider ? hit.point : (Vector2)firePoint.position + (Vector2)(firePoint.right * 25);
        Debug.DrawLine(firePoint.position, targetPosition, Color.red);
        GameObject trail = ObjectPooler.Instance.GetFromPool("Bullet Trail", firePoint.position, Quaternion.identity);
        StartCoroutine(MoveTrail(trail, targetPosition));

        if (hit.collider != null)
        {
            if (hit.collider.TryGetComponent<Enemy>(out var enemy))
            {
                enemy.TakeDamage(weaponStats[(int)weaponType].damage);
                ScoreSystem.Instance.RegisterHit(weaponType, weaponStats[(int)weaponType].fireRate);
            }
            else if (hit.collider.TryGetComponent<DestructibleObject>(out var destructibleObject))
            {
                destructibleObject.TakeDamage(weaponStats[(int)(weaponType)].damage);
            }
        }

    }

    private IEnumerator MoveTrail(GameObject trail, Vector2 targetPosition)
    {
        while ((Vector2)trail.transform.position != targetPosition)
        {
            float step = 40 * Time.deltaTime; // 60 units per frame
            trail.transform.position = Vector2.MoveTowards(trail.transform.position, targetPosition, step); // Move towards target
            yield return null; // Wait for the next frame
        }

        trail.SetActive(false);
    }

    private void FireProjectile()
    {
        if (weaponType == WeaponType.Shotgun)
        {
            for (int i = 0; i < shotgunPelletCount; i++)
            {
                Quaternion pelletRotation = firePoint.rotation * Quaternion.Euler(0, 0, startAngle + (angleStep * i));
                GameObject bullet = ObjectPooler.Instance.GetFromPool("Bullet", firePoint.position, pelletRotation);
                Bullet bulletScript = bullet.GetComponent<Bullet>();
                bulletScript.Shooter = gameObject;
                bulletScript.damage = weaponStats[(int)weaponType].damage;
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
                bulletRb.AddForce(bullet.transform.right * 20f, ForceMode2D.Impulse);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (performingFinisher) { return; } // Invincible when performing a finisher

        if (armour > 0)
        {
            // Subtract damage from armor first
            int damageToArmour = Mathf.Min(damage, armour);
            armour -= damageToArmour;
            damage -= damageToArmour;
        }

        if (damage > 0)
        {
            health -= damage;
        }

        if (health <= 0)
        {
            collider2d.enabled = false;
            spriteRenderer.sortingOrder = 0;
            animator.SetTrigger("Death");
            playerController.SetMovementLocked(true);
            StartCoroutine(GameManager.Instance.OnPlayerDeath());
        }

        damageFlash.CallDamageFlash();
        hudManager.UpdateHealthArmour(health, armour);
    }

    public void AddHealth(int healthToAdd)
    {
        health += healthToAdd;
        health = Mathf.Min(health, maxHealth);
    }

    public void ReplenishArmour(int numOfBars)
    {
        int barSize = maxArmour / 4;

        if (armour == 0)
        {
            armour = Mathf.Min(barSize * numOfBars, maxArmour);
        }
        else
        {
            int remainingInBar = barSize - (armour % barSize);
            armour += remainingInBar;
            armour = Mathf.Min(armour, maxArmour);
            // e.g. 375 armour % 250 leaves 175/250 in the current plate, 75 to add on
        }

        hudManager.UpdateHealthArmour(health, armour);
    }

    public void AddReserveAmmo(int type, int amount)
    {
        weaponStats[type].reserveAmmo += amount;
        UpdateAmmoUI();
        if (type == (int)weaponType && ammoGainedCoroutine == null)
        {
            ammoGainedCoroutine = StartCoroutine(AnimateAmmoText());
        }
    }

    IEnumerator AnimateAmmoText()
    {
        float elapsedTime = 0f;
        Vector3 originalScale = ammoText.transform.localScale;
        Vector3 targetScale = originalScale * 1.3f;

        while (elapsedTime < .25f)
        {
            ammoText.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / .25f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < .25f)
        {
            ammoText.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsedTime / .25f);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        ammoText.transform.localScale = originalScale;
        ammoGainedCoroutine = null;
    }

    public float EvaluatePerformance()
    {
        return initialHealthArmour - (health + armour); // How much damage was taken completing the current room
        // Will be negative if player replenished an armor plate and took no damage
    }

    public void ProceedToNextRoom()
    {
        initialHealthArmour = health + armour;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, .3f);
    }
}