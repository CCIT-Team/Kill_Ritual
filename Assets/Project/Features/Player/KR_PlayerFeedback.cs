using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class PlayerFeedback : MonoBehaviour
{
    [Header("=== 피격 이펙트 UI ===")]

    [SerializeField] 
    private Image damageVignette;

    [SerializeField] 
    private Image armorVignette;

    [SerializeField] 
    private Image lowHPVignette;

    [SerializeField] 
    private TextMeshProUGUI armorBrokenText;

    [Header("=== 체력 피해 설정 ===")]

    [SerializeField] 
    private Color damageColor = new Color(1f, 0f, 0f, 0.6f);

    [SerializeField] 
    private float damageFadeSpeed = 3f;

    [Header("=== 방어도 피해 설정 ===")]

    [SerializeField] 
    private Color armorColor = new Color(0f, 0.5f, 1f, 0.6f);

    [SerializeField] 
    private float armorFadeSpeed = 3f;

    [Header("=== 방어도 파괴 텍스트 설정 ===")]

    [SerializeField] 
    private float armorBrokenDisplayTime = 1.5f;

    [Header("=== 위험 체력 설정 ===")]

    [SerializeField] 
    private float lowHPThreshold = 0.3f;

    [SerializeField] 
    private float lowHPPulseSpeed = 2f;

    [SerializeField] 
    private Color lowHPColor = new Color(1f, 0f, 0f, 0.4f);

    private bool isLowHP = false;         // 현재 체력 위험 상태인지
    private float currentHP;             // 현재 체력 저장용
    private float maxHP;                 // 최대 체력 저장용
    private PlayerStats playerStats;     // PlayerStats 스크립트 참조

    private void Start()
    {
        playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("[PlayerFeedback] PlayerStats 컴포넌트를 찾을 수 없습니다!");
            return; // 더 이상 실행하지 않고 종료
        }

        playerStats.OnHPChanged.AddListener(OnHPChanged);
        playerStats.OnArmorChanged.AddListener(OnArmorChanged);
        playerStats.OnArmorBroken.AddListener(OnArmorBroken);
        playerStats.OnPlayerDied.AddListener(OnPlayerDied);

        // 모든 UI를 처음엔 투명하게 초기화
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (damageVignette != null)
            SetAlpha(damageVignette, 0f);   // 투명하게

        if (armorVignette != null)
            SetAlpha(armorVignette, 0f);

        if (lowHPVignette != null)
            SetAlpha(lowHPVignette, 0f);

        if (armorBrokenText != null)
            armorBrokenText.gameObject.SetActive(false);
        // SetActive(false): 오브젝트를 비활성화 (안 보이고 동작도 안 함)
    }

    private void Update()
    {
        if (damageVignette != null && damageVignette.color.a > 0f)
        {
            float newAlpha = Mathf.MoveTowards(
                damageVignette.color.a, 0f, damageFadeSpeed * Time.deltaTime);
            SetAlpha(damageVignette, newAlpha);
        }

        if (armorVignette != null && armorVignette.color.a > 0f)
        {
            float newAlpha = Mathf.MoveTowards(
                armorVignette.color.a, 0f, armorFadeSpeed * Time.deltaTime);
            SetAlpha(armorVignette, newAlpha);
        }

        if (isLowHP && lowHPVignette != null)
        {
            float pulse = Mathf.PingPong(Time.time * lowHPPulseSpeed, 1f);
            Color c = lowHPColor;
            c.a = pulse * lowHPColor.a; // 투명도를 맥박에 맞춰 변화
            lowHPVignette.color = c;
        }
    }

    private void OnHPChanged(float hp, float maxHp)
    {
        currentHP = hp;
        maxHP = maxHp;

        if (damageVignette != null)
        {
            damageVignette.color = damageColor; 
        }

        float ratio = maxHp > 0f ? hp / maxHp : 0f;

        isLowHP = ratio <= lowHPThreshold && hp > 0f;

        if (!isLowHP && lowHPVignette != null)
        {
            SetAlpha(lowHPVignette, 0f); // 위험 상태 아니면 맥박 끔
        }
    }

    private void OnArmorChanged(float armor, float maxArmor)
    {
        if (armorVignette != null)
        {
            armorVignette.color = armorColor; // 파란 플래시 표시
        }
    }

    private void OnArmorBroken()
    {
        if (armorBrokenText != null)
        {
            // 이미 실행 중인 코루틴이 있으면 중지하고 다시 시작
            StopCoroutine("ShowArmorBrokenText");
            StartCoroutine("ShowArmorBrokenText");
            // 코루틴: 시간을 기다리면서 실행하는 특별한 함수
        }
    }

    private IEnumerator ShowArmorBrokenText()
    {
        if (armorBrokenText == null) yield break;
        // yield break: 코루틴을 즉시 종료

        armorBrokenText.gameObject.SetActive(true);  // 텍스트 오브젝트 활성화
        armorBrokenText.text = "방어도 파괴!";
        armorBrokenText.color = new Color(0.2f, 0.6f, 1f, 1f); // 파란색 텍스트

        yield return new WaitForSeconds(armorBrokenDisplayTime);
        // WaitForSeconds: 지정한 초만큼 기다림 (이 사이에 다른 코드는 정상 실행)

        armorBrokenText.gameObject.SetActive(false); // 텍스트 숨김
    }

    private void OnPlayerDied()
    {
        isLowHP = false;
        StartCoroutine(DeathFade()); // 화면 암전 코루틴 시작
    }

    private IEnumerator DeathFade()
    {
        if (damageVignette == null) yield break;

        float elapsed = 0f;       // 경과 시간
        float duration = 1.5f;   // 암전까지 걸리는 시간

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // 매 프레임 경과 시간 누적
            float alpha = Mathf.Clamp01(elapsed / duration);
            Color deathColor = new Color(0.3f, 0f, 0f, alpha);
            damageVignette.color = deathColor;

            yield return null; // 한 프레임 기다리고 while 반복
        }
    }

    private void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;  // 현재 색상 가져오기
        c.a = alpha;            // 투명도(a)만 변경
        image.color = c;        // 다시 적용
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHPChanged.RemoveListener(OnHPChanged);
            playerStats.OnArmorChanged.RemoveListener(OnArmorChanged);
            playerStats.OnArmorBroken.RemoveListener(OnArmorBroken);
            playerStats.OnPlayerDied.RemoveListener(OnPlayerDied);
        }
    }
}