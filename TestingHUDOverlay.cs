using UnityEngine;
using UnityEngine.UI;

internal class TestingHUDOverlay : MonoBehaviour
{
    public static TestingHUDOverlay? Instance { get; private set; }

#pragma warning disable CS8618
    [SerializeField] GameObject toggle1Obj;
    [SerializeField] Text toggle1Label;
    [SerializeField] Toggle toggle1;

    [SerializeField] GameObject toggle2Obj;
    [SerializeField] Text toggle2Label;
    [SerializeField] Toggle toggle2;


    [SerializeField] Text label1;
    [SerializeField] Text label2;
    [SerializeField] Text label3;
#pragma warning restore CS8618

    public static void Init(GameObject prefab)
    {
        Instance = Instantiate(prefab).GetComponent<TestingHUDOverlay>();
    }

    public void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Update()
    {
        toggle1Obj.SetActive(toggle1Label.text != "");
        toggle2Obj.SetActive(toggle2Label.text != "");
    }

    public void SetToggle1(string label, bool value)
    {
        toggle1Label.text = label;
        toggle1.isOn = value;
    }

    public void SetToggle2(string label, bool value)
    {
        toggle2Label.text = label;
        toggle2.isOn = value;
    }

    public void SetLabel1(string value) => label1.text = value;
    public void SetLabel2(string value) => label2.text = value;
    public void SetLabel3(string value) => label3.text = value;
}
