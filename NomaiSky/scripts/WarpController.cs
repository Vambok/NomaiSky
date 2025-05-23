using UnityEngine;

namespace NomaiSky;

public class WarpController : MonoBehaviour
{
    ReferenceFrame targetReferenceFrame;
    ScreenPrompt travelPrompt;
    void Awake()
    {
        travelPrompt = new ScreenPrompt(InputLibrary.markEntryOnHUD, "Warp to star system");
        GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.AddListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
    }
    void OnDestroy()
    {
        GlobalMessenger<ReferenceFrame>.RemoveListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.RemoveListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
    }
    void Update()
    {
        if (PlayerState.InMapView())
        {
            if (targetReferenceFrame != null)
            {
                NomaiSky.Instance.MapExploration(targetReferenceFrame, travelPrompt);
            }
            else
            {
                travelPrompt.SetVisibility(false);
            }
        }
    }
    void OnTargetReferenceFrame(ReferenceFrame referenceFrame) { targetReferenceFrame = referenceFrame; }
    void OnUntargetReferenceFrame() { targetReferenceFrame = null; }
    void OnEnterMapView() { Locator.GetPromptManager().AddScreenPrompt(travelPrompt, PromptPosition.BottomCenter); }
    void OnExitMapView() { Locator.GetPromptManager().RemoveScreenPrompt(travelPrompt, PromptPosition.BottomCenter); }
}