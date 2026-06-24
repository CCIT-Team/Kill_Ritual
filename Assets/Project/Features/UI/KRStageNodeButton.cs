using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillRitual.StagePortal
{
    [RequireComponent(typeof(Button))]
    public class KRStageNodeButton : MonoBehaviour
    {
        [Header("Stage")]
        [SerializeField] private KRStageData stageData;

        [Header("UI")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject lockedObject;

        private Button button;
        private KRStageMapUI stageMapUI;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);

            RefreshVisual();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClicked);
        }

        public void Initialize(KRStageMapUI mapUI)
        {
            stageMapUI = mapUI;
        }

        private void OnClicked()
        {
            if (stageMapUI == null)
            {
                Debug.LogWarning($"{nameof(KRStageNodeButton)}: StageMapUI가 연결되지 않았습니다.", this);
                return;
            }

            if (stageData == null)
            {
                Debug.LogWarning($"{nameof(KRStageNodeButton)}: StageData가 없습니다.", this);
                return;
            }

            stageMapUI.SelectStage(stageData);
        }

        private void RefreshVisual()
        {
            if (stageData == null)
                return;

            if (labelText != null)
                labelText.text = stageData.DisplayName;

            if (iconImage != null)
            {
                iconImage.sprite = stageData.StageIcon;
                iconImage.enabled = stageData.StageIcon != null;
            }

            if (lockedObject != null)
                lockedObject.SetActive(!stageData.Unlocked);

            if (button != null)
                button.interactable = true;
        }
    }
}