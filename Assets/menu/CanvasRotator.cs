using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasRotator : MonoBehaviour
{
    [System.Serializable]
    public struct CanvasScreen
    {
        public CanvasGroup canvasGroup;
        public float fadeInDuration;
        public float visibleDuration;
        public float fadeOutDuration;
    }

    [Header("Настройки экранов")]
    [SerializeField] private List<CanvasScreen> screens;
    [SerializeField] private bool loop = true;

    private void Start()
    {
        // Инициализация: скрываем все экраны на старте
        foreach (var screen in screens)
        {
            if (screen.canvasGroup != null)
            {
                screen.canvasGroup.alpha = 0f;
                screen.canvasGroup.interactable = false;
                screen.canvasGroup.blocksRaycasts = false;
            }
        }

        if (screens.Count > 0)
        {
            StartCoroutine(RotateScreensRoutine());
        }
    }

    private IEnumerator RotateScreensRoutine()
    {
        do
        {
            for (int i = 0; i < screens.Count; i++)
            {
                CanvasScreen currentScreen = screens[i];

                // Запускаем появление экрана
                yield return StartCoroutine(FadeInScreen(currentScreen));

                // Проверка: если длительность 0, останавливаем авто-ротацию на этом экране навсегда
                if (currentScreen.visibleDuration <= 0f)
                {
                    // Зависаем в этой точке. Экран останется гореть вечно,
                    // пока кнопка не вызовет метод LoadNextSceneWithFade()
                    while (true)
                    {
                        yield return null;
                    }
                }

                // Если время больше 0, ждем и гасим экран как обычно
                yield return new WaitForSeconds(currentScreen.visibleDuration);
                yield return StartCoroutine(FadeOutScreen(currentScreen));
            }
        }
        while (loop);
    }

    private IEnumerator FadeInScreen(CanvasScreen screen)
    {
        if (screen.canvasGroup == null) yield break;

        CanvasGroup cg = screen.canvasGroup;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        float counter = 0f;
        while (counter < screen.fadeInDuration)
        {
            counter += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, counter / screen.fadeInDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private IEnumerator FadeOutScreen(CanvasScreen screen)
    {
        if (screen.canvasGroup == null) yield break;

        CanvasGroup cg = screen.canvasGroup;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float counter = 0f;
        while (counter < screen.fadeOutDuration)
        {
            counter += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, counter / screen.fadeOutDuration);
            yield return null;
        }
        cg.alpha = 0f;
    }
    [Header("Настройки перехода на сцену")]
    [SerializeField] private string nextSceneName; // Имя сцены для загрузки

    // Этот метод мы привяжем к кнопке в инспекторе
    public void LoadNextSceneWithFade()
    {
        // Останавливаем автоматическое чередование, чтобы оно не мешало
        StopAllCoroutines();

        // Запускаем процесс красивого выхода
        StartCoroutine(LoadSceneRoutine());
    }

    private IEnumerator LoadSceneRoutine()
    {
        // Ищем, какой экран сейчас активен (у которого alpha > 0)
        CanvasGroup activeGroup = null;
        foreach (var screen in screens)
        {
            if (screen.canvasGroup != null && screen.canvasGroup.alpha > 0)
            {
                activeGroup = screen.canvasGroup;
                break;
            }
        }

        // Если нашли активный экран, плавно гасим его
        if (activeGroup != null)
        {
            activeGroup.interactable = false;
            activeGroup.blocksRaycasts = false;

            float counter = 0f;
            float fadeOutDuration = 1f; // Время затухания перед сменой сцены

            while (counter < fadeOutDuration)
            {
                counter += Time.deltaTime;
                activeGroup.alpha = Mathf.Lerp(1f, 0f, counter / fadeOutDuration);
                yield return null;
            }
            activeGroup.alpha = 0f;
        }

        // Экран погас — загружаем новую сцену
        SceneManager.LoadScene(nextSceneName);
    }
}