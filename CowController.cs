/*
 * CowController.cs - Простой скрипт для управления поведением коровы.
 *
 * --- ИНСТРУКЦИЯ ПО НАСТРОЙКЕ ---
 * 1. Повесьте этот скрипт на ваш GameObject коровы.
 *
 * 2. Убедитесь, что на объекте коровы есть следующие компоненты:
 *    - Rigidbody (для физики, не ставьте isKinematic)
 *    - CapsuleCollider (или другой коллайдер для столкновений)
 *    - Animator (для проигрывания анимаций)
 *
 * 3. Создайте и настройте Animator Controller для коровы. Он должен содержать следующие параметры:
 *    - IsWalking (тип Bool)
 *    - IsRunning (тип Bool)
 *    - IsEating (тип Bool)
 *    - TakeDamage (тип Trigger)
 *    - Die (тип Trigger)
 *    Настройте переходы между анимациями (например, из Idle в Walk при IsWalking = true).
 *
 * 4. Настройте публичные поля в Инспекторе Unity, выбрав объект с этим скриптом:
 *    - Задайте скорость, здоровье и другие параметры в секции "Параметры коровы".
 *    - В секции "AI" создайте на сцене несколько пустых GameObject, которые будут служить точками маршрута,
 *      и перетащите их в массив "Waypoints".
 *    - В секции "Ссылки на компоненты и префабы" создайте префаб "мяса" и перетащите его в поле "Meat Prefab".
 *
 * 5. Чтобы нанести урон корове из другого скрипта, получите доступ к этому компоненту и вызовите метод TakeDamage().
 *    Пример кода для скрипта, наносящего урон (например, скрипт пули):
 *
 *    void OnCollisionEnter(Collision collision)
 *    {
 *        CowController cow = collision.gameObject.GetComponent<CowController>();
 *        if (cow != null)
 *        {
 *            cow.TakeDamage(25f); // Наносим 25 урона
 *        }
 *    }
 */

using UnityEngine;

public class CowController : MonoBehaviour
{
    // Перечисление для всех состояний коровы
    public enum CowState
    {
        Idle,
        Walking,
        Eating,
        Running,
        Dead
    }

    public bool animate;
    [Header("Параметры коровы")]
    public float moveSpeed = 1.5f;
    public float runSpeed = 5f;
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

    // --- Приватные переменные ---
    private CowState currentState;
    private Animator animator;
    private Rigidbody rb;

    private float currentHealth;
    private int targetWaypointIndex;
    private float stateTimer; // Таймер для управления сменой состояний (для Idle и Eating)

    void Start()
    {
        // Получаем ссылки на компоненты
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // Устанавливаем начальные значения
        currentHealth = health;
        currentState = CowState.Idle;
        // Устанавливаем таймер для первого состояния Idle
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        // Выбираем первую точку маршрута
        SelectNewWaypoint();
    }

    void FixedUpdate()
    {
        // Если корова мертва, прекращаем обработку
        if (currentState == CowState.Dead) return;

        // Машина состояний
        switch (currentState)
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
        // Предотвращаем смену состояния на то же самое или если корова мертва
        if (currentState == newState || currentState == CowState.Dead) return;

        currentState = newState;

        // Сбрасываем анимации перед установкой новой
        animator.SetBool("IsWalking", false);
        animator.SetBool("IsRunning", false);
        animator.SetBool("IsEating", false);

        // Логика при входе в новое состояние
        switch (currentState)
        {
            case CowState.Idle:
                rb.linearVelocity = Vector3.zero; // Останавливаем движение
                stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
                break;
            case CowState.Walking:
                animator.SetBool("IsWalking", true);
                SelectNewWaypoint();
                break;
            case CowState.Eating:
                rb.linearVelocity = Vector3.zero;
                animator.SetBool("IsEating", true);
                stateTimer = eatDuration;
                break;
            case CowState.Running:
                animator.SetBool("IsRunning", true);
                // При переходе в бег, выбираем новую точку, чтобы убежать
                SelectNewWaypoint();
                break;
            case CowState.Dead:
                // Эта логика обрабатывается в методе Die()
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

        // Движение и поворот
        MoveAndRotate(targetPos, moveSpeed);

        // Проверка дистанции до цели
        if (Vector3.Distance(transform.position, targetPos) < 1.5f)
        {
            // Решаем, будем есть или просто стоять
            if (Random.value < chanceToEat)
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

        // Когда добежали до точки, "успокаиваемся" и переходим в Idle
        if (Vector3.Distance(transform.position, targetPos) < 2f)
        {
            SwitchState(CowState.Idle);
        }
    }
    public LayerMask obstacleMask = ~0;
    private void MoveAndRotate(Vector3 targetPosition, float speed)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;

        
        bool hit = Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, 4, obstacleMask, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(transform.position, transform.forward * 4, hit ? Color.red : Color.green);
        Quaternion wall = hit ? Quaternion.AngleAxis(75f, Vector3.up) : Quaternion.identity;
        // Поворот
            Quaternion targetRotation = Quaternion.LookRotation(direction);
           // transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 0.55f);
           // transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 1.155f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation * wall,  Time.deltaTime * 300f); 
         //   transform.rotation = targetRotation;
        

        // Движение через Rigidbody
        //rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);
        if (stopped == false)
        {
            transform.position += transform.forward * speed2 * Time.deltaTime;
        }
        
    }

    public float speed2 = 1;
    public bool stopped = false;
    
    /// <summary>
    /// Выбирает новую случайную точку из массива waypoints.
    /// </summary>
    private void SelectNewWaypoint()
    {
        if (waypoints.Length == 0) return;

        // Простой выбор случайного индекса. Можно усложнить, чтобы не выбирать ту же точку подряд.
        int newIndex = Random.Range(0, waypoints.Length);
        if (waypoints.Length > 1 && newIndex == targetWaypointIndex)
        {
            newIndex = (newIndex + 1) % waypoints.Length;
        }
        targetWaypointIndex = newIndex;
    }

    /// <summary>
    /// Публичный метод для нанесения урона корове.
    /// </summary>
    /// <param name="damageAmount">Количество урона.</param>
    public void TakeDamage(float damageAmount)
    {
        if (currentState == CowState.Dead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0);

        // Триггер анимации получения урона
        if(animator != null) animator.SetTrigger("TakeDamage");

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
        // Мы используем SwitchState, чтобы централизовать логику выхода из состояний,
        // но также напрямую задаем Dead, чтобы избежать повторного входа.
        currentState = CowState.Dead;

        if(animator != null) animator.SetTrigger("Die");

        // Отключаем физику и коллайдер, чтобы мертвая корова не реагировала на окружение
        if(rb != null) rb.isKinematic = true;

        Collider col = GetComponent<Collider>();
        if(col != null) col.enabled = false;

        // Создаем "мясо"
        if (meatPrefab != null)
        {
            Instantiate(meatPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // Уничтожаем GameObject коровы через 5 секунд
        Destroy(gameObject, 5f);
    }
}
