using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Vista del Canvas enlazada en escena; una resolución por bola, en orden.</summary>
public class GamblingPanel : MonoBehaviour
{
    public static GamblingPanel Instance { get; private set; }
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform wheel;
    [SerializeField] private TMP_Text resultLabel;
    [SerializeField] private float spinDuration = 2.5f, resultDuration = .8f;
    private BallSpecial owner;
    private void Awake()
    {
        Instance = this;
        var background = root.GetComponent<UnityEngine.UI.Image>();
        background.sprite = GaltonTheme.Card;
        background.type = UnityEngine.UI.Image.Type.Sliced;
        root.SetActive(false);
    }
    private void Update()
    {
        // Una bola cancelada al reiniciar no deja la vista ni la cola bloqueadas.
        if (owner == null && root.activeSelf) root.SetActive(false);
    }
    public IEnumerator Spin(BallSpecial ball, Action<double> done)
    {
        while (owner != null) yield return null;
        owner = ball;
        root.SetActive(true); root.transform.SetAsLastSibling();
        resultLabel.text = "Girando…"; resultLabel.color = GaltonTheme.Ink;
        int sector = UnityEngine.Random.Range(0, RouletteWheel.Multipliers.Length);
        float target = 1440 + sector * 36;
        for (float elapsed = 0; elapsed < spinDuration; elapsed += Time.deltaTime)
        {
            float t = elapsed / spinDuration;
            wheel.localRotation = Quaternion.Euler(0, 0, target * (1 - Mathf.Pow(1 - t, 3)));
            yield return null;
        }
        wheel.localRotation = Quaternion.Euler(0, 0, target);
        int factor = RouletteWheel.Multipliers[sector];
        resultLabel.color = factor == 1 ? GaltonTheme.Ink : RouletteWheel.SectorColor(factor);
        resultLabel.text = factor == 0 ? "Sin premio · ×0" : "Premio · ×" + factor;
        yield return new WaitForSeconds(resultDuration);
        root.SetActive(false); owner = null;
        done(factor);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
