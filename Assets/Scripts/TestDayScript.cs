using UnityEngine;

public class TestDayScript : MonoBehaviour
{
    [SerializeField] private bool isWizardOfOzTest = false;
    public static bool IsWizardOfOzTest => instance.isWizardOfOzTest;
    private int holdButNoSound = 0;

    public static TestDayScript instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    private void NoTriggerHoldSound()
    {
        holdButNoSound++;
        print($"Hold touched at {Time.time} with no sound. No. of times: {holdButNoSound}");
    }
    private void NoFinishSound()
    {
        print($"Finished climb, no sound at time: {Time.time}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            if (IsWizardOfOzTest)
                AppEvents.RaisePotentialHandholdContact();
            else NoTriggerHoldSound();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (IsWizardOfOzTest)
                AppEvents.RaisePotentialHighestHandholdContact();
            else NoFinishSound();
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            if(IsWizardOfOzTest)
                AppEvents.RaisePotentialFallDetected();
        }
    }
}
