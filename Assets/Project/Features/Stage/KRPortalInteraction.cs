using System.Collections;
using UnityEngine;

namespace KillRitual.StagePortal
{
    public class KRPortalInteraction : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private string playerTag = "Player";

        [Header("UI References")]
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private KRStageMapUI stageMapUI;

        [Header("UX Timing - Seconds")]
        [Min(0f)]
        [SerializeField] private float promptFadeSeconds = 0.15f;

        [Min(0f)]
        [SerializeField] private float interactionCooldownSeconds = 0.2f;

        private bool playerInRange;
        private bool canInteract = true;
        private Coroutine promptRoutine;

        private void Awake()
        {
            // 시작 시 프롬프트는 숨김.
            SetPromptImmediate(false);
        }

        private void Update()
        {
            if (!playerInRange)
                return;

            if (!canInteract)
                return;

            if (stageMapUI != null && stageMapUI.IsOpen)
                return;

            if (Input.GetKeyDown(interactKey))
            {
                OpenStageMap();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag))
                return;

            playerInRange = true;
            ShowPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag))
                return;

            playerInRange = false;
            ShowPrompt(false);
        }

        private void OpenStageMap()
        {
            if (stageMapUI == null)
            {
                Debug.LogWarning($"{nameof(KRPortalInteraction)}: StageMapUI가 연결되지 않았습니다.", this);
                return;
            }

            StartCoroutine(InteractionCooldownRoutine());

            ShowPrompt(false);
            stageMapUI.Open();
        }

        private IEnumerator InteractionCooldownRoutine()
        {
            canInteract = false;

            if (interactionCooldownSeconds > 0f)
                yield return new WaitForSeconds(interactionCooldownSeconds);

            canInteract = true;
        }

        private void ShowPrompt(bool show)
        {
            if (promptCanvasGroup == null)
                return;

            if (promptRoutine != null)
                StopCoroutine(promptRoutine);

            promptRoutine = StartCoroutine(FadeCanvasGroup(
                promptCanvasGroup,
                show ? 1f : 0f,
                promptFadeSeconds
            ));
        }

        private void SetPromptImmediate(bool show)
        {
            if (promptCanvasGroup == null)
                return;

            promptCanvasGroup.alpha = show ? 1f : 0f;
            promptCanvasGroup.interactable = show;
            promptCanvasGroup.blocksRaycasts = show;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
        {
            float startAlpha = canvasGroup.alpha;

            canvasGroup.interactable = targetAlpha > 0f;
            canvasGroup.blocksRaycasts = targetAlpha > 0f;

            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }
    }
}