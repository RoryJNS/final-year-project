using UnityEngine;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    public enum WeaponType { Melee, Rifle, SMG, Shotgun, RPG };
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
    [SerializeField] private ObjectPooler pooler;

    [SerializeField] private WeaponStats[] weaponStats; // Array to store stats for each weapon type
    [SerializeField] private int shotgunPelletCount, maxMeleeDamage;
    [SerializeField] private float shotgunSpreadAngle, maxMeleeChargeTime;

    private Coroutine holdFireCoroutine;
    public float lastAttackTime;
    private float angleStep, startAngle;
    [SerializeField] private bool holdFiring, isAlternateAttack, isReloading;

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
    }

    public void SetWeapon(WeaponType type, int ammo)
    {
        weaponType = type;
        currentAmmo = ammo;
        hudManager.UpdateReticle((int)type);
        UpdateAmmoUI();
        progressWheel.SetActive(false);
        holdFiring = false;
        animator.SetFloat("Weapon Type", (int)type);
    }

    public void Attack()
    {
        if (performingFinisher) { return; }

        if (isReloading || Time.time - lastAttackTime < weaponStats[(int)weaponType].fireRate) return;

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
            case WeaponType.RPG:
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
        if (weaponType == WeaponType.Melee || holdFiring || currentAmmo <= 0) return;
        holdFireCoroutine = StartCoroutine(HoldFireRoutine());
    }

    private IEnumerator HoldFireRoutine()
    {
        holdFiring = true;
        while (currentAmmo > 0)
        {
            Attack();
            yield return new WaitForSeconds(weaponStats[(int)weaponType].fireRate);
        }
    }

    private IEnumerator ChargeMeleeAttack()
    {
        progressWheel.SetActive(true);
        float elapsedTime = 0f;

        while (holdFiring && elapsedTime < maxMeleeChargeTime)
        {
            elapsedTime += Time.deltaTime;

            // Update progress wheel value between 0 and 1
            float progress = Mathf.Clamp01(elapsedTime / maxMeleeChargeTime);
            hudManager.SetProgressWheel(progress);

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
        Debug.Log((int)scaledDamage);
        StartCoroutine(MeleeAttack((int)scaledDamage, chargeFactor));
    }

    private IEnumerator MeleeAttack(int damage, float chargeFactor)
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
                enemy.TakeDamage(weaponStats[(int)weaponType].damage);
                ScoreSystem.Instance.RegisterHit(weaponType, weaponStats[(int)weaponType].fireRate);
            }
        }
    }

    public void Reload()
    {
        if (isReloading || currentAmmo == weaponStats[(int)weaponType].ammoPerClip || weaponStats[(int)weaponType].reserveAmmo == 0) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        progressWheel.SetActive(true);
        float elapsedTime = 0f;

        while (elapsedTime < weaponStats[(int)weaponType].reloadSpeed)
        {
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
        isReloading = false;

        if (holdFiring)
        {
            holdFireCoroutine = StartCoroutine(HoldFireRoutine());
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
        Debug.DrawRay(firePoint.position, firePoint.right, Color.red);
        Vector2 targetPosition = hit.collider ? hit.point : (Vector2)firePoint.position + (Vector2)(firePoint.right * 25);
        GameObject trail = pooler.GetFromPool("Bullet Trail", firePoint.position, Quaternion.identity);
        StartCoroutine(MoveTrail(trail, targetPosition));

        if (hit.collider != null && hit.collider.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.TakeDamage(weaponStats[(int)weaponType].damage);
            ScoreSystem.Instance.RegisterHit(weaponType, weaponStats[(int)weaponType].fireRate);
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
            for (int i = 0; i < shotgunPelletCount; i++)
            {
                Quaternion pelletRotation = firePoint.rotation * Quaternion.Euler(0, 0, startAngle + (angleStep * i));
                GameObject bullet = pooler.GetFromPool("Bullet", firePoint.position, pelletRotation);
                bullet.GetComponent<Bullet>().Shooter = gameObject;
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
            health -= damage; // Subtract remaining damage from health
        }

        if (health <= 0)
        {
            // Death, level failed etc.            
        }

        damageFlash.CallDamageFlash();
        hudManager.UpdateHealthArmour(health, armour);
    }

    public void AddHealth(int health)
    {
        this.health += health;
        if (this.health > maxHealth) { this.health = maxHealth; }
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
            // e.g. 375 armour % 250 leaves 175/250 in the current plate, 75 to add on
        }

        hudManager.UpdateHealthArmour(health, armour);
    }

    public void AddReserveAmmo(int type, int amount)
    {
        weaponStats[type].reserveAmmo += amount;
        UpdateAmmoUI();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, .3f);
    }
}