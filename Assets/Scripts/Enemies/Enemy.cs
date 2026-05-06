using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    // [Phase 2] Commented out — EnemyType enum removed (was in Core/Enums.cs).
    // Phase 3 will replace this with UnitData ScriptableObject reference.
    // public EnemyType enemyType;
    public int maxHP = 50;
    public float moveSpeed = 1f;
    public int attackDamage = 10;
    public float attackRate = 1f;

    private int currentHP;
    private float attackTimer;
    private Hero targetHero;

    private void Start()
    {
        currentHP = maxHP;
    }

    private void Update()
    {
        if (targetHero == null)
        {
            // Di chuyển sang trái
            transform.Translate(Vector2.left * moveSpeed * Time.deltaTime);
        }
        else
        {
            // Tấn công hero
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackRate)
            {
                AttackHero();
                attackTimer = 0f;
            }
        }

        // Nếu đi quá xa bên trái => Game Over
        if (transform.position.x < -10f)
        {
            GameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }

    private void AttackHero()
    {
        if (targetHero != null)
        {
            targetHero.TakeDamage(attackDamage);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        GameManager.Instance.AddGold(10);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hero"))
        {
            targetHero = other.GetComponent<Hero>();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Hero"))
        {
            targetHero = null;
        }
    }
}
