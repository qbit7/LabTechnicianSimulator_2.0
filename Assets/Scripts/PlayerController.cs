using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Передвижение")]
    public float walkSpeed = 5f;

    [Header("Вращение камеры")]
    public float lookSensitivity = 0.1f;

    [Header("Взаимодействие")]
    public float interactionDistance = 2.5f;

    [Header("Гравитация")]
    public float gravityMultiplier = 2f;        // множитель силы падения

    private float _verticalVelocity;            // текущая скорость падения

    private CharacterController _controller;
    private Camera _playerCamera;
    private PlayerControls _input;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private float _cameraPitch = 0f;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();
        _input = new PlayerControls();

        // Прячем и блокируем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Move.performed += OnMove;
        _input.Player.Move.canceled += OnMove;
        _input.Player.Look.performed += OnLook;
        _input.Player.Look.canceled += OnLook;
        _input.Player.Interact.performed += OnInteract;
        _input.Player.Quit.performed += OnQuit;
    }

    void OnDisable()
    {
        _input.Player.Move.performed -= OnMove;
        _input.Player.Move.canceled -= OnMove;
        _input.Player.Look.performed -= OnLook;
        _input.Player.Look.canceled -= OnLook;
        _input.Player.Interact.performed -= OnInteract;
        _input.Player.Quit.performed -= OnQuit;
        _input.Player.Disable();
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        ApplyGravity();
    }

    // ── Обработчики ввода ──

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        _lookInput = ctx.ReadValue<Vector2>();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) TryInteract();
    }

    private void OnQuit(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // ── Движение ──

    void HandleMovement()
    {
        Vector3 forward = _playerCamera.transform.forward;
        Vector3 right = _playerCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * _moveInput.y + right * _moveInput.x).normalized;

        // Двигаем только по горизонтали (вертикаль теперь в ApplyGravity)
        Vector3 horizontalMove = moveDir * (walkSpeed * Time.deltaTime);
        _controller.Move(horizontalMove);
    }

    // ── Взгляд ──

    void HandleMouseLook()
    {
        float yaw = _lookInput.x * lookSensitivity;
        transform.Rotate(Vector3.up * yaw);

        _cameraPitch -= _lookInput.y * lookSensitivity;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);
        _playerCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    // ── Взаимодействие ──

    void TryInteract()
    {
        Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            //Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            //interactable?.Interact();
            Debug.Log("Взаимодействие с " + hit.collider.name + " (скрипт Interactable отсутствует)");
        }
    }

    // ── Гравитация ──

    void ApplyGravity()
    {
        // Если персонаж на земле и падает (вертикальная скорость отрицательная),
        // сбрасываем скорость до маленького значения, чтобы прижимать к поверхности
        if (_controller.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f; // небольшой прижим
        }
        else
        {
            // Добавляем ускорение свободного падения
            _verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }

        // Двигаем персонажа только по вертикали
        _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
    }
}