using UnityEngine;

namespace NomaiSky;

public class WarpController : MonoBehaviour {
    PromptManager promptManager;
    ReferenceFrame targetReferenceFrame;
    ScreenPrompt travelPrompt;
    public ScreenPrompt fuelPrompt;
    public Vector3 currentOffset;

    void Awake() {
        promptManager = Locator.GetPromptManager();
        travelPrompt = new ScreenPrompt(InputLibrary.markEntryOnHUD, "Warp to star system");
        fuelPrompt = new ScreenPrompt("Not enough fuel");
        GlobalMessenger<ReferenceFrame>.AddListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.AddListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
    }
    void OnDestroy() {
        GlobalMessenger<ReferenceFrame>.RemoveListener("TargetReferenceFrame", OnTargetReferenceFrame);
        GlobalMessenger.RemoveListener("UntargetReferenceFrame", OnUntargetReferenceFrame);
        GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
        GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
    }
    void Update() {
        if(PlayerState.InMapView()) {
            if(targetReferenceFrame != null) {
                NomaiSky.Instance.MapExploration(targetReferenceFrame, travelPrompt);
            } else {
                travelPrompt.SetVisibility(false);
            }
        } else if(Locator.GetCenterOfTheUniverse() != null) {
            Vector3 currentSystemCubePosition = Locator.GetCenterOfTheUniverse().GetOffsetPosition() - currentOffset;
            if(currentSystemCubePosition.magnitude > NomaiSky.systemRadius) {
                NomaiSky.Instance.SpaceExploration(currentSystemCubePosition);
            }
        }
    }
    void OnTargetReferenceFrame(ReferenceFrame referenceFrame) { targetReferenceFrame = referenceFrame; }
    void OnUntargetReferenceFrame() { targetReferenceFrame = null; }
    void OnEnterMapView() {
        promptManager.AddScreenPrompt(travelPrompt, PromptPosition.BottomCenter);
        promptManager.AddScreenPrompt(fuelPrompt, PromptPosition.Center);
    }
    void OnExitMapView() {
        promptManager.RemoveScreenPrompt(travelPrompt, PromptPosition.BottomCenter);
        promptManager.RemoveScreenPrompt(fuelPrompt, PromptPosition.Center);
    }
}