using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LastSignal.Core
{
    /// <summary>
    /// HUD tài nguyên tối giản, diegetic. Subscribe ResourceSystem.OnResourcesChanged.
    /// Gán Image fill (type Filled) hoặc TMP_Text cho từng tài nguyên — cái nào trống thì bỏ qua.
    /// Giữ kiềm chế để không phá immersion (xem GDD 'resources' > HUD).
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Source")]
        public ResourceSystem resources;

        [Header("Fuel")]
        public Image fuelFill;
        public TMP_Text fuelText;

        [Header("Oxygen")]
        public Image oxygenFill;
        public TMP_Text oxygenText;

        [Header("Hull")]
        public Image hullFill;
        public TMP_Text hullText;

        void Start()
        {
            if (resources == null && GameState.Instance != null)
                resources = GameState.Instance.resources;

            if (resources != null)
            {
                resources.OnResourcesChanged += Redraw;
                Redraw();
            }
        }

        void OnDestroy()
        {
            if (resources != null) resources.OnResourcesChanged -= Redraw;
        }

        void Redraw()
        {
            Set(fuelFill, fuelText, resources.fuel, "FUEL");
            Set(oxygenFill, oxygenText, resources.oxygen, "O₂");
            Set(hullFill, hullText, resources.hull, "HULL");
        }

        void Set(Image fill, TMP_Text text, ResourceSystem.Resource r, string label)
        {
            if (fill != null) fill.fillAmount = r.Normalized;
            if (text != null) text.text = $"{label} {Mathf.RoundToInt(r.current)}";
        }
    }
}
