using System.Collections;
using UnityEngine;

/// <summary>Una casilla verde de diez: premio x10 o pérdida de esta bola.</summary>
public class BallGambler : BallSpecial
{
    private static BallGambler active;
    private float angle;
    private bool showing;
    private int result;
    public static double RewardFactor(int sector) => sector == 0 ? 10d : 0d;
    public override IEnumerator Resolve(BallController controller, System.Action<double> done)
    {
        while (active != null) yield return null;
        active = this; showing = true;
        result = Random.Range(0, 10);
        float target = 1440f + (360f - result * 36f);
        for (float elapsed = 0; elapsed < 2.5f; elapsed += Time.deltaTime)
        { float t = Mathf.Clamp01(elapsed / 2.5f); angle = Mathf.Lerp(0, target, 1 - Mathf.Pow(1 - t, 3)); yield return null; }
        angle = target;
        yield return new WaitForSeconds(.8f);
        showing = false; active = null;
        done(RewardFactor(result));
    }
    private void OnDestroy() { if (active == this) active = null; }
    private void OnGUI()
    {
        if (!showing) return;
        Rect panel = new Rect((Screen.width - 340) / 2f, (Screen.height - 360) / 2f, 340, 360);
        GUI.Box(panel, "APUESTA — verde x10 / rojo o negro: 0");
        Vector2 center = new Vector2(panel.center.x, panel.y + 180);
        GUI.Label(new Rect(center.x - 8, center.y - 135, 30, 25), "▼");
        Color previous = GUI.backgroundColor;
        for (int i = 0; i < 10; i++)
        {
            float radians = (i * 36 + angle - 90) * Mathf.Deg2Rad;
            GUI.backgroundColor = i == 0 ? Color.green : i % 2 == 0 ? Color.red : Color.black;
            GUI.Box(new Rect(center.x + Mathf.Cos(radians) * 95 - 22, center.y + Mathf.Sin(radians) * 95 - 18, 44, 36), i == 0 ? "x10" : "0");
        }
        GUI.backgroundColor = previous;
        GUI.Label(new Rect(panel.x + 25, panel.yMax - 45, 300, 30), "10 % de ganar · Solo se apuesta esta bola");
    }
}
