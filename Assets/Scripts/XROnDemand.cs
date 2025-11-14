using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

public class XROnDemand : MonoBehaviour
{
    IEnumerator Start()
    {
        var mgr = XRGeneralSettings.Instance?.Manager;
        if (mgr == null) yield break;

        if (!mgr.isInitializationComplete)
        {
            yield return mgr.InitializeLoader();
            if (mgr.activeLoader != null)
                mgr.StartSubsystems();
        }
    }

    void OnDestroy()
    {
        var mgr = XRGeneralSettings.Instance?.Manager;
        if (mgr != null && mgr.isInitializationComplete)
            mgr.StopSubsystems();
    }
}
