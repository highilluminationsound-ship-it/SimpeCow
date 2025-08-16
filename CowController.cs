using UnityEngine;

public class CowController : MonoBehaviour
{
    public enum CowState
    {
        Idle,
        Walking,
        Eating,
        Running,
        Dead
    }

    [Header("Параметры коровы")]
    public float moveSpeed = 1.5f;
    public float runSpeed = 5f;
    public float rotationSpeed = 5f;
    public float health = 100f;

    [Header("AI")]
    public Transform[] waypoints; // Массив точек, по которым корова будет ходить
    [Tooltip("Минимальное и максимальное время в состоянии покоя (Idle)")]
    public Vector2 idleTimeRange = new Vector2(3f, 7f);
    [Tooltip("Время, которое корова ест")]
    public float eatDuration = 4f;
    [Tooltip("Шанс (0-1), что корова начнет есть по прибытии к точке")]
    [Range(0, 1)]
    public float chanceToEat = 0.3f;

    [Header("Ссылки на компоненты и префабы")]
    public GameObject meatPrefab; // Префаб мяса, который появляется после смерти

    private CowState currentState;
    private Animator animator;
    private Rigidbody rb;
    private float currentHealth;
    private int targetWaypointIndex;
    private float stateTimer; // Таймер для управления сменой состояний (для Idle и Eating)

    void Start()
    {
        animator = GetComponent<Animator>(); // Получаем ссылки на компоненты
        rb = GetComponent<Rigidbody>();

        currentHealth = health; // Устанавливаем начальные значения
        currentState = CowState.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y); // Устанавливаем таймер для первого состояния Idle

        SelectNewWaypoint(); // Выбираем первую точку маршрута
    }

    void Update()
    {
        if (currentState == CowState.Dead) return; // Если корова мертва, прекращаем обработку

        switch (currentState) // Машина состояний
        {
            case CowState.Idle:
                HandleIdleState();
                break;
            case CowState.Walking:
                HandleWalkingState();
                break;
            case CowState.Eating:
                HandleEatingState();
                break;
            case CowState.Running:
                HandleRunningState();
                break;
        }
    }

    private void SwitchState(CowState newState)
    {
        if (currentState == newState || currentState == CowState.Dead) return; // Предотвращаем смену состояния на то же самое или если корова мертва

        currentState = newState;

        animator.SetBool("IsWalking", false); // Сбрасываем анимации перед установкой новой
        animator.SetBool("IsRunning", false);
        animator.SetBool("IsEating", false);

        switch (currentState) // Логика при входе в новое состояние
        {
            case CowState.Idle:
                rb.velocity = Vector3.zero; // Останавливаем движение
                stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
                break;
            case CowState.Walking:
                animator.SetBool("IsWalking", true);
                SelectNewWaypoint();
                break;
            case CowState.Eating:
                rb.velocity = Vector3.zero;
                animator.SetBool("IsEating", true);
                stateTimer = eatDuration;
                break;
            case CowState.Running:
                animator.SetBool("IsRunning", true);
                SelectNewWaypoint(); // При переходе в бег, выбираем новую точку, чтобы убежать
                break;
            case CowState.Dead: // Эта логика обрабатывается в методе Die()
                break;
        }
    }

    private void HandleIdleState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            SwitchState(CowState.Walking);
        }
    }

    private void HandleWalkingState()
    {
        if (waypoints.Length == 0)
        {
            SwitchState(CowState.Idle); // Если нет точек, просто стоим
            return;
        }

        Transform waypoint = waypoints[targetWaypointIndex];
        Vector3 targetPos = new Vector3(waypoint.position.x, transform.position.y, waypoint.position.z);

        MoveAndRotate(targetPos, moveSpeed); // Движение и поворот

        if (Vector3.Distance(transform.position, targetPos) < 1.5f) // Проверка дистанции до цели
        {
            if (Random.value < chanceToEat) // Решаем, будем есть или просто стоять
            {
                SwitchState(CowState.Eating);
            }
            else
            {
                SwitchState(CowState.Idle);
            }
        }
    }

    private void HandleEatingState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            SwitchState(CowState.Idle);
        }
    }

    private void HandleRunningState()
    {
        if (waypoints.Length == 0)
        {
            SwitchState(CowState.Idle);
            return;
        }

        Transform waypoint = waypoints[targetWaypointIndex];
        Vector3 targetPos = new Vector3(waypoint.position.x, transform.position.y, waypoint.position.z);

        MoveAndRotate(targetPos, runSpeed);

        if (Vector3.Distance(transform.position, targetPos) < 2f) // Когда добежали до точки, "успокаиваемся" и переходим в Idle
        {
            SwitchState(CowState.Idle);
        }
    }

    private void MoveAndRotate(Vector3 targetPosition, float speed)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;

        if (direction != Vector3.zero) // Поворот
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        rb.velocity = new Vector3(direction.x * speed, rb.velocity.y, direction.z * speed); // Движение через Rigidbody
    }

    private void SelectNewWaypoint()
    {
        if (waypoints.Length == 0) return;

        int newIndex = Random.Range(0, waypoints.Length); // Простой выбор случайного индекса. Можно усложнить, чтобы не выбирать ту же точку подряд.
        if (waypoints.Length > 1 && newIndex == targetWaypointIndex)
        {
            newIndex = (newIndex + 1) % waypoints.Length;
        }
        targetWaypointIndex = newIndex;
    }

    public void TakeDamage(float damageAmount)
    {
        if (currentState == CowState.Dead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        if(animator != null) animator.SetTrigger("TakeDamage"); // Триггер анимации получения урона

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            SwitchState(CowState.Running);
        }
    }

    private void Die()
    {
        currentState = CowState.Dead; // Мы используем SwitchState, чтобы централизовать логику выхода из состояний, но также напрямую задаем Dead, чтобы избежать повторного входа.

        if(animator != null) animator.SetTrigger("Die");

        if(rb != null) rb.isKinematic = true; // Отключаем физику и коллайдер, чтобы мертвая корова не реагировала на окружение

        Collider col = GetComponent<Collider>();
        if(col != null) col.enabled = false;

        if (meatPrefab != null) // Создаем "мясо"
        {
            Instantiate(meatPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        Destroy(gameObject, 5f); // Уничтожаем GameObject коровы через 5 секунд
    }
}
