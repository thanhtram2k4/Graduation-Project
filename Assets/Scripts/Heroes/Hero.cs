using UnityEngine;

public class Hero : MonoBehaviour
{
    [Header("Hero Stats")]
    // [Phase 2] Commented out — HeroType enum removed (was in Core/Enums.cs).
    // Phase 3 will replace this with UnitData ScriptableObject reference.
    // public HeroType heroType;
    public int maxHP = 100;
    public float attackRate = 1f;
    public int attackDamage = 10;
    public int cost = 50;

    private int currentHP;
    private float attackTimer;

    private void Start()
    {
        currentHP = maxHP;
        attackTimer = 0f;
    }

    private void Update()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer >= attackRate && HasEnemyInRange())
        {
            Attack();
            attackTimer = 0f;
        }
    }

    private bool HasEnemyInRange()
    {
        // Raycast sang phải để tìm quân địch trên cùng lane
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, Vector2.right, 20f, LayerMask.GetMask("Enemy"));
        return hit.collider != null;
    }

    protected virtual void Attack()
    {
        // Override trong các lớp con để tạo đạn hoặc hiệu ứng
        Debug.Log($"{gameObject.name} đang tấn công!"); // [Phase 2] Replaced heroType with gameObject.name
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
        Destroy(gameObject);
    }
}
