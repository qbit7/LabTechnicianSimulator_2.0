using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FancyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [System.Serializable]
    public struct ButtonState
    {
        public Image spriteImage;
        public float fadeInDuration;
        public float fadeOutDuration;
        [Tooltip("Если включено, эта картинка никогда не будет скрываться (Fade Out игнорируется)")]
        public bool keepAlwaysVisible;
    }

    [System.Serializable]
    public struct ScaleStep
    {
        [Range(0f, 3f)]
        public float scalePercent;
        public float duration;
        [Tooltip("Сколько секунд кнопка удерживает этот размер (используется только для шагов ПОСЛЕ отпускания кнопки)")]
        public float holdDuration;
    }

    [Header("Элементы UI")]
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Состояния (Настройки Fade)")]
    [SerializeField] private ButtonState defaultState;
    [SerializeField] private ButtonState hoverState;
    [SerializeField] private ButtonState pressedState;

    [Header("Тайминги клика")]
    [SerializeField]
    [Tooltip("Сколько времени кнопка удерживает состояние Pressed после ОТПУСКАНИЯ клика")]
    private float pressedVisibleDuration = 2.0f;

    [Header("Настройки упругости (Масштаб)")]
    [SerializeField]
    [Tooltip("Element 0 — это размер пока кнопка ЗАЖАТА. Остальные элементы — анимация ОТСКОКА после отпускания.")]
    private List<ScaleStep> pressScaleSteps = new List<ScaleStep>()
    {
        new ScaleStep { scalePercent = 0.75f, duration = 0.1f, holdDuration = 0f }, // Element 0: Зажатие (держится пока не отпустим)
        new ScaleStep { scalePercent = 1.1f, duration = 0.15f, holdDuration = 0f }, // Element 1: Отскок при отпускании
        new ScaleStep { scalePercent = 1.0f, duration = 0.1f, holdDuration = 0f }   // Element 2: Возврат в 100%
    };

    [Header("Цвета текста (HEX)")]
    [SerializeField] private Color textColorDefault = new Color32(0x07, 0x51, 0x83, 0xFF);
    [SerializeField] private Color textColorHover = new Color32(0x07, 0x51, 0x83, 0xFF);
    [SerializeField] private Color textColorPressed = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    [Header("Событие клика")]
    public UnityEngine.Events.UnityEvent onClick;

    private bool isHovered = false;
    private bool isPressed = false;
    private bool isHoldingPressedState = false;

    private Coroutine defaultFadeCoroutine;
    private Coroutine hoverFadeCoroutine;
    private Coroutine pressedFadeCoroutine;
    private Coroutine textFadeCoroutine;
    private Coroutine pressedSequenceCoroutine;
    private Coroutine scaleCoroutine;

    private Vector3 originalScale;

    private void Start()
    {
        originalScale = transform.localScale;

        SetImageAlpha(defaultState.spriteImage, 1f);
        SetImageAlpha(hoverState.spriteImage, 0f);
        SetImageAlpha(pressedState.spriteImage, 0f);

        if (buttonText != null)
            buttonText.color = textColorDefault;
    }

    // --- ОБРАБОТКА СОБЫТИЙ МЫШИ ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (isHoldingPressedState || isPressed) return;

        ChangeState(ref hoverState, ref hoverFadeCoroutine);
        SetTextColor(textColorHover, hoverState.fadeInDuration);
        FadeOutState(ref defaultState, ref defaultFadeCoroutine);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (isHoldingPressedState || isPressed) return;

        ChangeState(ref defaultState, ref defaultFadeCoroutine);
        SetTextColor(textColorDefault, defaultState.fadeInDuration);
        FadeOutState(ref hoverState, ref hoverFadeCoroutine);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isHoldingPressedState) return;

        isPressed = true;

        if (pressedSequenceCoroutine != null) StopCoroutine(pressedSequenceCoroutine);

        ChangeState(ref pressedState, ref pressedFadeCoroutine);
        SetTextColor(textColorPressed, pressedState.fadeInDuration);

        FadeOutState(ref hoverState, ref hoverFadeCoroutine);
        FadeOutState(ref defaultState, ref defaultFadeCoroutine);

        // 1. При нажатии запускаем ТОЛЬКО первый шаг (Element 0)
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        if (pressScaleSteps.Count > 0)
        {
            scaleCoroutine = StartCoroutine(AnimateSingleStepRoutine(pressScaleSteps[0]));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPressed || isHoldingPressedState) return;
        isPressed = false;

        // 2. При отпускании запускаем анимацию отскока (все шаги начиная с Element 1)
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateRemainingStepsRoutine());

        pressedSequenceCoroutine = StartCoroutine(PressedHoldSequence());
    }

    // --- ЛОГИКА УДЕРЖАНИЯ НАЖАТОГО СОСТОЯНИЯ ---

    private IEnumerator PressedHoldSequence()
    {
        isHoldingPressedState = true;

        if (buttonText != null) buttonText.color = textColorPressed;

        yield return new WaitForSeconds(pressedVisibleDuration);

        isHoldingPressedState = false;

        if (isHovered)
        {
            ChangeState(ref hoverState, ref hoverFadeCoroutine);
            SetTextColor(textColorHover, hoverState.fadeInDuration);
            FadeOutState(ref pressedState, ref pressedFadeCoroutine);
        }
        else
        {
            ChangeState(ref defaultState, ref defaultFadeCoroutine);
            SetTextColor(textColorDefault, defaultState.fadeInDuration);
            FadeOutState(ref pressedState, ref pressedFadeCoroutine);
            FadeOutState(ref hoverState, ref hoverFadeCoroutine);
        }

        onClick.Invoke();
    }

    // --- УПРАВЛЕНИЕ ЦВЕТОМ ТЕКСТА ---
    private void SetTextColor(Color targetColor, float duration)
    {
        if (buttonText == null) return;

        if (textFadeCoroutine != null) StopCoroutine(textFadeCoroutine);
        textFadeCoroutine = StartCoroutine(FadeTextColorRoutine(targetColor, duration));
    }

    private IEnumerator FadeTextColorRoutine(Color targetColor, float duration)
    {
        Color startColor = buttonText.color;
        float counter = 0f;

        if (duration <= 0f)
        {
            buttonText.color = targetColor;
            yield break;
        }

        while (counter < duration)
        {
            counter += Time.deltaTime;
            buttonText.color = Color.Lerp(startColor, targetColor, counter / duration);
            yield return null;
        }
        buttonText.color = targetColor;
    }

    // --- КОРУТИНА ДЛЯ ОДНОГО ШАГА (ЗАЖАТИЕ) ---
    private IEnumerator AnimateSingleStepRoutine(ScaleStep step)
    {
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * step.scalePercent;
        float counter = 0f;

        if (step.duration > 0f)
        {
            while (counter < step.duration)
            {
                counter += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, counter / step.duration);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
        }
        transform.localScale = targetScale;
    }

    // --- КОРУТИНА ДЛЯ ОСТАВШИХСЯ ШАГОВ (ОТПУСКАНИЕ) ---
    private IEnumerator AnimateRemainingStepsRoutine()
    {
        // Пропускаем Element 0, идем со следующего
        for (int i = 1; i < pressScaleSteps.Count; i++)
        {
            ScaleStep step = pressScaleSteps[i];
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = originalScale * step.scalePercent;
            float counter = 0f;

            if (step.duration > 0f)
            {
                while (counter < step.duration)
                {
                    counter += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, counter / step.duration);
                    transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                    yield return null;
                }
            }
            transform.localScale = targetScale;

            if (step.holdDuration > 0f)
            {
                yield return new WaitForSeconds(step.holdDuration);
            }
        }
    }

    // --- ЛОГИКА АНИМАЦИИ КАРТИНОК (FADE) ---
    private void ChangeState(ref ButtonState activeState, ref Coroutine stateCoroutine)
    {
        if (activeState.spriteImage == null) return;

        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(FadeRoutine(activeState.spriteImage, 1f, activeState.fadeInDuration));
    }

    private void FadeOutState(ref ButtonState inactiveState, ref Coroutine stateCoroutine)
    {
        if (inactiveState.spriteImage == null) return;

        if (inactiveState.keepAlwaysVisible)
        {
            if (stateCoroutine != null) StopCoroutine(stateCoroutine);
            stateCoroutine = StartCoroutine(FadeRoutine(inactiveState.spriteImage, 1f, 0f));
            return;
        }

        if (stateCoroutine != null) StopCoroutine(stateCoroutine);
        stateCoroutine = StartCoroutine(FadeRoutine(inactiveState.spriteImage, 0f, inactiveState.fadeOutDuration));
    }

    private IEnumerator FadeRoutine(Image image, float targetAlpha, float duration)
    {
        if (image == null) yield break;
        float startAlpha = image.color.a;

        if (duration <= 0f)
        {
            SetImageAlpha(image, targetAlpha);
            yield break;
        }

        float counter = 0f;
        while (counter < duration)
        {
            counter += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, counter / duration);
            SetImageAlpha(image, currentAlpha);
            yield return null;
        }
        SetImageAlpha(image, targetAlpha);
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}